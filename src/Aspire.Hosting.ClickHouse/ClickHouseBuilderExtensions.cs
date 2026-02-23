// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Data.Common;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ClickHouse;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding ClickHouse resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class ClickHouseBuilderExtensions
{
    // Internal port is always 8123 (HTTP interface).
    private const int DefaultContainerPort = 8123;

    private const string UserEnvVarName = "CLICKHOUSE_USER";
    private const string PasswordEnvVarName = "CLICKHOUSE_PASSWORD";

    /// <summary>
    /// Adds a ClickHouse resource to the application model. A container is used for local development.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="ClickHouseContainerImageTags.Tag"/> tag of the <inheritdoc cref="ClickHouseContainerImageTags.Image"/> container image.
    /// </remarks>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port for ClickHouse.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ClickHouseServerResource> AddClickHouse(this IDistributedApplicationBuilder builder, [ResourceName] string name, int? port)
    {
        return AddClickHouse(builder, name, port, null, null);
    }

    /// <summary>
    /// <inheritdoc cref="AddClickHouse(IDistributedApplicationBuilder, string, int?)"/>
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port for ClickHouse.</param>
    /// <param name="userName">A parameter that contains the ClickHouse server user name, or <see langword="null"/> to use a default value.</param>
    /// <param name="password">A parameter that contains the ClickHouse server password, or <see langword="null"/> for no password.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ClickHouseServerResource> AddClickHouse(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? port = null,
        IResourceBuilder<ParameterResource>? userName = null,
        IResourceBuilder<ParameterResource>? password = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var passwordParameter = password?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password");
        var clickHouseContainer = new ClickHouseServerResource(name, userName?.Resource, passwordParameter);

        string? connectionString = null;

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(clickHouseContainer, async (@event, ct) =>
        {
            connectionString = await clickHouseContainer.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);

            if (connectionString is null)
            {
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{clickHouseContainer.Name}' resource but the connection string was null.");
            }
        });

        builder.Eventing.Subscribe<ResourceReadyEvent>(clickHouseContainer, async (@event, ct) =>
        {
            if (connectionString is null)
            {
                throw new DistributedApplicationException($"ResourceReadyEvent was published for the '{clickHouseContainer.Name}' resource but the connection string was null.");
            }

            var csb = new DbConnectionStringBuilder { ConnectionString = connectionString };
            var host = csb["Host"]?.ToString();
            var port = csb["Port"]?.ToString();
            var user = csb.ContainsKey("Username") ? csb["Username"]?.ToString() : "default";
            var pass = csb.ContainsKey("Password") ? csb["Password"]?.ToString() : null;

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("X-ClickHouse-User", user);
            if (pass is not null)
            {
                httpClient.DefaultRequestHeaders.Add("X-ClickHouse-Key", pass);
            }

            var baseUrl = $"http://{host}:{port}";
            var logger = @event.Services.GetRequiredService<ResourceLoggerService>().GetLogger(clickHouseContainer);

            foreach (var (dbResourceName, _) in clickHouseContainer.Databases)
            {
                if (builder.Resources.FirstOrDefault(r => string.Equals(r.Name, dbResourceName, StringComparisons.ResourceName)) is ClickHouseDatabaseResource database)
                {
                    await CreateDatabaseAsync(httpClient, baseUrl, database, logger, ct).ConfigureAwait(false);
                }
            }
        });

        return builder
            .AddResource(clickHouseContainer)
            .WithEndpoint(port: port, targetPort: DefaultContainerPort, name: ClickHouseServerResource.PrimaryEndpointName, scheme: "http")
            .WithImage(ClickHouseContainerImageTags.Image, ClickHouseContainerImageTags.Tag)
            .WithImageRegistry(ClickHouseContainerImageTags.Registry)
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables[UserEnvVarName] = clickHouseContainer.UserNameReference;

                if (clickHouseContainer.PasswordParameter is not null)
                {
                    context.EnvironmentVariables[PasswordEnvVarName] = clickHouseContainer.PasswordParameter;
                }
            })
            .WithHttpHealthCheck("/ping");
    }

    /// <summary>
    /// Adds a ClickHouse database to the application model.
    /// </summary>
    /// <param name="builder">The ClickHouse server resource builder.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="databaseName">The name of the database. If not provided, this defaults to the same value as <paramref name="name"/>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ClickHouseDatabaseResource> AddDatabase(this IResourceBuilder<ClickHouseServerResource> builder, [ResourceName] string name, string? databaseName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        // Use the resource name as the database name if it's not provided
        databaseName ??= name;

        builder.Resource.AddDatabase(name, databaseName);
        var clickHouseDatabase = new ClickHouseDatabaseResource(name, databaseName, builder.Resource);

        return builder.ApplicationBuilder
            .AddResource(clickHouseDatabase);
    }

    /// <summary>
    /// Adds a named volume for the data folder to a ClickHouse container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ClickHouseServerResource> WithDataVolume(this IResourceBuilder<ClickHouseServerResource> builder, string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/var/lib/clickhouse", isReadOnly);
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a ClickHouse container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only mount.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ClickHouseServerResource> WithDataBindMount(this IResourceBuilder<ClickHouseServerResource> builder, string source, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(source);

        return builder.WithBindMount(source, "/var/lib/clickhouse", isReadOnly);
    }

    private static async Task CreateDatabaseAsync(HttpClient httpClient, string baseUrl, ClickHouseDatabaseResource database, ILogger logger, CancellationToken ct)
    {
        logger.LogDebug("Creating database '{DatabaseName}'", database.DatabaseName);

        try
        {
            // ClickHouse identifiers use backtick quoting
            var sql = $"CREATE DATABASE IF NOT EXISTS `{database.DatabaseName}`";
            var response = await httpClient.PostAsync(baseUrl, new StringContent(sql), ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            logger.LogDebug("Database '{DatabaseName}' created successfully", database.DatabaseName);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create database '{DatabaseName}'", database.DatabaseName);
        }
    }
}
