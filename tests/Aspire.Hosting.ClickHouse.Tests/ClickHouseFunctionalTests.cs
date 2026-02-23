// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.TestUtilities;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Aspire.Hosting.Utils;
using ClickHouse.Driver.ADO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Polly;

namespace Aspire.Hosting.ClickHouse.Tests;

public class ClickHouseFunctionalTests
{
    [Fact]
    [RequiresFeature(TestFeature.Docker)]
    public async Task VerifyWaitForOnClickHouseBlocksDependentResources()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        using var appBuilder = DistributedApplicationTestingBuilder.Create();

        var healthCheckTcs = new TaskCompletionSource<HealthCheckResult>();
        appBuilder.Services.AddHealthChecks().AddAsyncCheck("blocking_check", () =>
        {
            return healthCheckTcs.Task;
        });

        var resource = appBuilder.AddClickHouse("resource")
                           .WithHealthCheck("blocking_check");

        var dependentResource = appBuilder.AddClickHouse("dependentresource")
                                       .WaitFor(resource);

        await using var app = await appBuilder.BuildAsync();

        var pendingStart = app.StartAsync(cts.Token);

        await app.ResourceNotifications.WaitForResourceAsync(resource.Resource.Name, KnownResourceStates.Running, cts.Token);

        await app.ResourceNotifications.WaitForResourceAsync(dependentResource.Resource.Name, KnownResourceStates.Waiting, cts.Token);

        healthCheckTcs.SetResult(HealthCheckResult.Healthy());

        await app.ResourceNotifications.WaitForResourceHealthyAsync(resource.Resource.Name, cts.Token);

        await app.ResourceNotifications.WaitForResourceAsync(dependentResource.Resource.Name, KnownResourceStates.Running, cts.Token);

        await pendingStart;
        await app.StopAsync();
    }

    [Fact]
    [RequiresFeature(TestFeature.Docker)]
    public async Task VerifyClickHouseResource()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new() { MaxRetryAttempts = 10, Delay = TimeSpan.FromSeconds(1) })
            .Build();

        using var appBuilder = DistributedApplicationTestingBuilder.Create();

        var clickhouse = appBuilder.AddClickHouse("clickhouse");
        var db = clickhouse.AddDatabase("testdb");
        await using var app = await appBuilder.BuildAsync();

        await app.StartAsync();

        var hb = Host.CreateApplicationBuilder();

        hb.Configuration[$"ConnectionStrings:{db.Resource.Name}"] = await db.Resource.ConnectionStringExpression.GetValueAsync(default);

        hb.AddClickHouseDataSource(db.Resource.Name);

        using var host = hb.Build();

        await host.StartAsync();

        await pipeline.ExecuteAsync(async token =>
        {
            var dataSource = host.Services.GetRequiredService<ClickHouseDataSource>();

            var client = dataSource.GetClient();
            var result = await client.PingAsync(cancellationToken: token);
            Assert.True(result);
        }, cts.Token);
    }

    [Fact]
    [RequiresFeature(TestFeature.Docker)]
    public async Task VerifyClickHouseDatabaseAutoCreation()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new() { MaxRetryAttempts = 10, Delay = TimeSpan.FromSeconds(1) })
            .Build();

        using var appBuilder = DistributedApplicationTestingBuilder.Create();

        var clickhouse = appBuilder.AddClickHouse("clickhouse");
        var db = clickhouse.AddDatabase("autotestdb");
        await using var app = await appBuilder.BuildAsync();

        await app.StartAsync();

        var hb = Host.CreateApplicationBuilder();

        // Connect using the database connection string — this should work because the database
        // was auto-created by the ResourceReadyEvent handler
        hb.Configuration[$"ConnectionStrings:{db.Resource.Name}"] = await db.Resource.ConnectionStringExpression.GetValueAsync(default);

        hb.AddClickHouseDataSource(db.Resource.Name);

        using var host = hb.Build();

        await host.StartAsync();

        await pipeline.ExecuteAsync(async token =>
        {
            var dataSource = host.Services.GetRequiredService<ClickHouseDataSource>();
            await using var connection = dataSource.CreateConnection();
            await connection.OpenAsync(token);

            // Create a table in the auto-created database — this proves the database exists
            using var createCmd = connection.CreateCommand();
            createCmd.CommandText = "CREATE TABLE IF NOT EXISTS test_auto (id UInt32) ENGINE = MergeTree() ORDER BY id";
            await createCmd.ExecuteNonQueryAsync(token);

            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO test_auto (id) VALUES (1)";
            await insertCmd.ExecuteNonQueryAsync(token);

            using var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT count() FROM test_auto";
            var result = await selectCmd.ExecuteScalarAsync(token);
            Assert.Equal((ulong)1, result);
        }, cts.Token);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [RequiresFeature(TestFeature.Docker)]
    public async Task WithDataShouldPersistStateBetweenUsages(bool useVolume)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new() { MaxRetryAttempts = 20, Delay = TimeSpan.FromSeconds(2) })
            .Build();

        string? volumeName = null;
        string? bindMountPath = null;

        try
        {
            using var builder1 = DistributedApplicationTestingBuilder.Create();
            var password1 = builder1.AddParameter("clickhouse-password", "testpassword");
            var clickhouse1 = builder1.AddClickHouse("clickhouse", password: password1);
            var db1 = clickhouse1.AddDatabase("testdb");

            if (useVolume)
            {
                // Use a deterministic volume name to prevent them from exhausting the machines if deletion fails
                volumeName = VolumeNameGenerator.Generate(clickhouse1, nameof(WithDataShouldPersistStateBetweenUsages));

                // if the volume already exists (because of a crashing previous run), delete it
                DockerUtils.AttemptDeleteDockerVolume(volumeName, throwOnFailure: true);
                clickhouse1.WithDataVolume(volumeName);
            }
            else
            {
                bindMountPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

                clickhouse1.WithDataBindMount(bindMountPath);
            }

            await using (var app = await builder1.BuildAsync())
            {
                await app.StartAsync();
                await app.ResourceNotifications.WaitForResourceHealthyAsync(clickhouse1.Resource.Name, cts.Token);
                try
                {
                    // Use the server connection string (without database) to create the database first
                    var hb = Host.CreateApplicationBuilder();

                    hb.Configuration[$"ConnectionStrings:{clickhouse1.Resource.Name}"] = await clickhouse1.Resource.ConnectionStringExpression.GetValueAsync(default);

                    hb.AddClickHouseDataSource(clickhouse1.Resource.Name);

                    using (var host = hb.Build())
                    {
                        await host.StartAsync();

                        await pipeline.ExecuteAsync(async token =>
                        {
                            var dataSource = host.Services.GetRequiredService<ClickHouseDataSource>();
                            await using var connection = dataSource.CreateConnection();
                            await connection.OpenAsync(token);

                            using var createDbCmd = connection.CreateCommand();
                            createDbCmd.CommandText = "CREATE DATABASE IF NOT EXISTS testdb";
                            await createDbCmd.ExecuteNonQueryAsync(token);

                            using var createCmd = connection.CreateCommand();
                            createCmd.CommandText = "CREATE TABLE IF NOT EXISTS testdb.test_table (id UInt32, name String) ENGINE = MergeTree() ORDER BY id";
                            await createCmd.ExecuteNonQueryAsync(token);

                            using var insertCmd = connection.CreateCommand();
                            insertCmd.CommandText = "INSERT INTO testdb.test_table (id, name) VALUES (1, 'test1'), (2, 'test2')";
                            await insertCmd.ExecuteNonQueryAsync(token);
                        }, cts.Token);
                    }
                }
                finally
                {
                    // Stops the container, or the Volume/mount would still be in use
                    await app.StopAsync();
                }
            }

            using var builder2 = DistributedApplicationTestingBuilder.Create();
            var password2 = builder2.AddParameter("clickhouse-password", "testpassword");
            var clickhouse2 = builder2.AddClickHouse("clickhouse", password: password2);
            var db2 = clickhouse2.AddDatabase("testdb");

            if (useVolume)
            {
                clickhouse2.WithDataVolume(volumeName);
            }
            else
            {
                clickhouse2.WithDataBindMount(bindMountPath!);
            }

            await using (var app = await builder2.BuildAsync())
            {
                await app.StartAsync();
                await app.ResourceNotifications.WaitForResourceHealthyAsync(clickhouse2.Resource.Name, cts.Token);
                try
                {
                    var hb = Host.CreateApplicationBuilder();

                    hb.Configuration[$"ConnectionStrings:{clickhouse2.Resource.Name}"] = await clickhouse2.Resource.ConnectionStringExpression.GetValueAsync(default);

                    hb.AddClickHouseDataSource(clickhouse2.Resource.Name);

                    using (var host = hb.Build())
                    {
                        await host.StartAsync();

                        await pipeline.ExecuteAsync(async token =>
                        {
                            var dataSource = host.Services.GetRequiredService<ClickHouseDataSource>();
                            await using var connection = dataSource.CreateConnection();
                            await connection.OpenAsync(token);

                            using var command = connection.CreateCommand();
                            command.CommandText = "SELECT count() FROM testdb.test_table";
                            var result = await command.ExecuteScalarAsync(token);
                            Assert.Equal((ulong)2, result);
                        }, cts.Token);
                    }
                }
                finally
                {
                    // Stops the container, or the Volume/mount would still be in use
                    await app.StopAsync();
                }
            }
        }
        finally
        {
            if (volumeName is not null)
            {
                DockerUtils.AttemptDeleteDockerVolume(volumeName);
            }

            if (bindMountPath is not null)
            {
                try
                {
                    Directory.Delete(bindMountPath, recursive: true);
                }
                catch
                {
                    // Don't fail test if we can't clean the temporary folder
                }
            }
        }
    }
}
