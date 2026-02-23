// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Sockets;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.ClickHouse.Tests;

public class AddClickHouseTests
{
    [Fact]
    public void AddClickHouseContainerWithDefaultsAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddClickHouse("clickhouse");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<ClickHouseServerResource>());
        Assert.Equal("clickhouse", containerResource.Name);

        var endpoint = Assert.Single(containerResource.Annotations.OfType<EndpointAnnotation>());
        Assert.Equal(8123, endpoint.TargetPort);
        Assert.False(endpoint.IsExternal);
        Assert.Equal("http", endpoint.Name);
        Assert.Null(endpoint.Port);
        Assert.Equal(ProtocolType.Tcp, endpoint.Protocol);
        Assert.Equal("http", endpoint.Transport);
        Assert.Equal("http", endpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(ClickHouseContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(ClickHouseContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(ClickHouseContainerImageTags.Registry, containerAnnotation.Registry);
    }

    [Fact]
    public void AddClickHouseContainerAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddClickHouse("clickhouse", 9813);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<ClickHouseServerResource>());
        Assert.Equal("clickhouse", containerResource.Name);

        var endpoint = Assert.Single(containerResource.Annotations.OfType<EndpointAnnotation>());
        Assert.Equal(8123, endpoint.TargetPort);
        Assert.False(endpoint.IsExternal);
        Assert.Equal("http", endpoint.Name);
        Assert.Equal(9813, endpoint.Port);
        Assert.Equal(ProtocolType.Tcp, endpoint.Protocol);
        Assert.Equal("http", endpoint.Transport);
        Assert.Equal("http", endpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(ClickHouseContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(ClickHouseContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(ClickHouseContainerImageTags.Registry, containerAnnotation.Registry);
    }

    [Fact]
    public void AddClickHouseAddsHealthCheckAnnotationToResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var clickhouse = builder.AddClickHouse("clickhouse");

        Assert.Single(clickhouse.Resource.Annotations, a => a is HealthCheckAnnotation);
    }

    [Fact]
    public async Task ClickHouseCreatesConnectionString()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder
            .AddClickHouse("clickhouse")
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 8123))
            .AddDatabase("mydatabase");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbResource = Assert.Single(appModel.Resources.OfType<ClickHouseDatabaseResource>());
        var serverResource = dbResource.Parent as IResourceWithConnectionString;
        var connectionStringResource = dbResource as IResourceWithConnectionString;
        Assert.NotNull(connectionStringResource);
        var connectionString = await connectionStringResource.GetConnectionStringAsync();

        // Default password is always generated, so check the expression format and that the resolved string contains a password
        Assert.Equal("Host={clickhouse.bindings.http.host};Port={clickhouse.bindings.http.port};Username=default;Password={clickhouse-password.value}", serverResource!.ConnectionStringExpression.ValueExpression);
        var serverConnectionString = await serverResource.GetConnectionStringAsync();
        Assert.StartsWith("Host=localhost;Port=8123;Username=default;Password=", serverConnectionString);

        Assert.Equal("Host={clickhouse.bindings.http.host};Port={clickhouse.bindings.http.port};Username=default;Password={clickhouse-password.value};Database=mydatabase", connectionStringResource.ConnectionStringExpression.ValueExpression);
        Assert.StartsWith("Host=localhost;Port=8123;Username=default;Password=", connectionString);
        Assert.EndsWith(";Database=mydatabase", connectionString);
    }

    [Fact]
    public async Task ClickHouseCreatesConnectionStringWithPassword()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var password = appBuilder.AddParameter("password", "p@ssw0rd1", secret: true);
        appBuilder
            .AddClickHouse("clickhouse", password: password)
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 8123))
            .AddDatabase("mydatabase");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var dbResource = Assert.Single(appModel.Resources.OfType<ClickHouseDatabaseResource>());
        var serverResource = dbResource.Parent as IResourceWithConnectionString;
        var connectionStringResource = dbResource as IResourceWithConnectionString;
        Assert.NotNull(connectionStringResource);
        var connectionString = await connectionStringResource.GetConnectionStringAsync();

        Assert.Equal("Host=localhost;Port=8123;Username=default;Password=p@ssw0rd1", await serverResource!.GetConnectionStringAsync());
        Assert.Equal("Host={clickhouse.bindings.http.host};Port={clickhouse.bindings.http.port};Username=default;Password={password.value}", serverResource.ConnectionStringExpression.ValueExpression);
        Assert.Equal("Host=localhost;Port=8123;Username=default;Password=p@ssw0rd1;Database=mydatabase", connectionString);
        Assert.Equal("Host={clickhouse.bindings.http.host};Port={clickhouse.bindings.http.port};Username=default;Password={password.value};Database=mydatabase", connectionStringResource.ConnectionStringExpression.ValueExpression);
    }

    [Fact]
    public async Task VerifyManifest()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var clickhouse = appBuilder.AddClickHouse("clickhouse");
        var db = clickhouse.AddDatabase("mydb");

        var clickhouseManifest = await ManifestUtils.GetManifest(clickhouse.Resource);
        var dbManifest = await ManifestUtils.GetManifest(db.Resource);

        var expectedManifest = $$"""
            {
              "type": "container.v0",
              "connectionString": "Host={clickhouse.bindings.http.host};Port={clickhouse.bindings.http.port};Username=default;Password={clickhouse-password.value}",
              "image": "{{ClickHouseContainerImageTags.Registry}}/{{ClickHouseContainerImageTags.Image}}:{{ClickHouseContainerImageTags.Tag}}",
              "env": {
                "CLICKHOUSE_USER": "default",
                "CLICKHOUSE_PASSWORD": "{clickhouse-password.value}"
              },
              "bindings": {
                "http": {
                  "scheme": "http",
                  "protocol": "tcp",
                  "transport": "http",
                  "targetPort": 8123
                }
              }
            }
            """;
        Assert.Equal(expectedManifest, clickhouseManifest.ToString());

        expectedManifest = """
            {
              "type": "value.v0",
              "connectionString": "Host={clickhouse.bindings.http.host};Port={clickhouse.bindings.http.port};Username=default;Password={clickhouse-password.value};Database=mydb"
            }
            """;
        Assert.Equal(expectedManifest, dbManifest.ToString());
    }

    [Fact]
    public async Task VerifyManifestWithPassword()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var password = appBuilder.AddParameter("password", secret: true);
        var clickhouse = appBuilder.AddClickHouse("clickhouse", password: password);
        var db = clickhouse.AddDatabase("mydb");

        var clickhouseManifest = await ManifestUtils.GetManifest(clickhouse.Resource);
        var dbManifest = await ManifestUtils.GetManifest(db.Resource);

        var expectedManifest = $$"""
            {
              "type": "container.v0",
              "connectionString": "Host={clickhouse.bindings.http.host};Port={clickhouse.bindings.http.port};Username=default;Password={password.value}",
              "image": "{{ClickHouseContainerImageTags.Registry}}/{{ClickHouseContainerImageTags.Image}}:{{ClickHouseContainerImageTags.Tag}}",
              "env": {
                "CLICKHOUSE_USER": "default",
                "CLICKHOUSE_PASSWORD": "{password.value}"
              },
              "bindings": {
                "http": {
                  "scheme": "http",
                  "protocol": "tcp",
                  "transport": "http",
                  "targetPort": 8123
                }
              }
            }
            """;
        Assert.Equal(expectedManifest, clickhouseManifest.ToString());

        expectedManifest = """
            {
              "type": "value.v0",
              "connectionString": "Host={clickhouse.bindings.http.host};Port={clickhouse.bindings.http.port};Username=default;Password={password.value};Database=mydb"
            }
            """;
        Assert.Equal(expectedManifest, dbManifest.ToString());
    }

    [Fact]
    public void AddClickHouseWithDefaultsGeneratesPasswordParameter()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var clickhouse = appBuilder.AddClickHouse("clickhouse");

        using var app = appBuilder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var serverResource = Assert.Single(appModel.Resources.OfType<ClickHouseServerResource>());
        Assert.NotNull(serverResource.PasswordParameter);
        Assert.True(serverResource.PasswordParameter.Secret);
    }

    [Fact]
    public void AddClickHouseWithExplicitPasswordUsesProvidedPassword()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var password = appBuilder.AddParameter("mypassword", "explicit", secret: true);
        var clickhouse = appBuilder.AddClickHouse("clickhouse", password: password);

        Assert.Equal("mypassword", clickhouse.Resource.PasswordParameter!.Name);
    }

    [Fact]
    public void ThrowsWithIdenticalChildResourceNames()
    {
        var builder = DistributedApplication.CreateBuilder();

        var db = builder.AddClickHouse("clickhouse1");
        db.AddDatabase("db");

        Assert.Throws<DistributedApplicationException>(() => db.AddDatabase("db"));
    }

    [Fact]
    public void ThrowsWithIdenticalChildResourceNamesDifferentParents()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddClickHouse("clickhouse1")
            .AddDatabase("db");

        var db = builder.AddClickHouse("clickhouse2");
        Assert.Throws<DistributedApplicationException>(() => db.AddDatabase("db"));
    }

    [Fact]
    public void CanAddDatabasesWithDifferentNamesOnSingleServer()
    {
        var builder = DistributedApplication.CreateBuilder();

        var clickhouse1 = builder.AddClickHouse("clickhouse1");

        var db1 = clickhouse1.AddDatabase("db1", "customers1");
        var db2 = clickhouse1.AddDatabase("db2", "customers2");

        Assert.Equal("customers1", db1.Resource.DatabaseName);
        Assert.Equal("customers2", db2.Resource.DatabaseName);

        Assert.Equal("Host={clickhouse1.bindings.http.host};Port={clickhouse1.bindings.http.port};Username=default;Password={clickhouse1-password.value};Database=customers1", db1.Resource.ConnectionStringExpression.ValueExpression);
        Assert.Equal("Host={clickhouse1.bindings.http.host};Port={clickhouse1.bindings.http.port};Username=default;Password={clickhouse1-password.value};Database=customers2", db2.Resource.ConnectionStringExpression.ValueExpression);
    }

    [Fact]
    public void CanAddDatabasesWithTheSameNameOnMultipleServers()
    {
        var builder = DistributedApplication.CreateBuilder();

        var db1 = builder.AddClickHouse("clickhouse1")
            .AddDatabase("db1", "imports");

        var db2 = builder.AddClickHouse("clickhouse2")
            .AddDatabase("db2", "imports");

        Assert.Equal("imports", db1.Resource.DatabaseName);
        Assert.Equal("imports", db2.Resource.DatabaseName);

        Assert.Equal("Host={clickhouse1.bindings.http.host};Port={clickhouse1.bindings.http.port};Username=default;Password={clickhouse1-password.value};Database=imports", db1.Resource.ConnectionStringExpression.ValueExpression);
        Assert.Equal("Host={clickhouse2.bindings.http.host};Port={clickhouse2.bindings.http.port};Username=default;Password={clickhouse2-password.value};Database=imports", db2.Resource.ConnectionStringExpression.ValueExpression);
    }
}
