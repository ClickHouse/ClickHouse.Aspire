// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ClickHouse.Driver.ADO;
using Microsoft.DotNet.XUnitExtensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Aspire.ClickHouse.Driver.Tests;

public class CloudIntegrationTests
{
    private static string? GetCloudConnectionString()
        => Environment.GetEnvironmentVariable("CLOUD_CONNSTRING");

    private static void SkipIfNoCloudConnection()
    {
        if (string.IsNullOrEmpty(GetCloudConnectionString()))
            throw new SkipTestException("CLOUD_CONNSTRING environment variable is not set");
    }

    [ConditionalFact]
    public async Task CanPingCloudInstance()
    {
        SkipIfNoCloudConnection();

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:clickhouse", GetCloudConnectionString())
        ]);

        builder.AddClickHouseDataSource("clickhouse");

        using var host = builder.Build();
        var dataSource = host.Services.GetRequiredService<ClickHouseDataSource>();
        var client = dataSource.GetClient();

        var result = await client.PingAsync(cancellationToken: CancellationToken.None);
        Assert.True(result);
    }

    [ConditionalFact]
    public async Task CanExecuteQueryOnCloudInstance()
    {
        SkipIfNoCloudConnection();

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:clickhouse", GetCloudConnectionString())
        ]);

        builder.AddClickHouseDataSource("clickhouse");

        using var host = builder.Build();
        var dataSource = host.Services.GetRequiredService<ClickHouseDataSource>();
        var client = dataSource.GetClient();

        var result = await client.ExecuteScalarAsync("SELECT 1", cancellationToken: CancellationToken.None);
        Assert.Equal((byte)1, result);
    }

    [ConditionalFact]
    public async Task HealthCheckReportsHealthyForCloudInstance()
    {
        SkipIfNoCloudConnection();

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:clickhouse", GetCloudConnectionString())
        ]);

        builder.AddClickHouseDataSource("clickhouse");

        using var host = builder.Build();
        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync();

        Assert.Contains(report.Entries, x => x.Key == "ClickHouse");
        Assert.Equal(HealthStatus.Healthy, report.Entries["ClickHouse"].Status);
    }
}
