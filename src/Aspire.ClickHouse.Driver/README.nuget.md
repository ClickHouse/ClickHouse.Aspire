# Aspire.ClickHouse.Driver

ClickHouse client integration for [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) using [ClickHouse.Driver](https://www.nuget.org/packages/ClickHouse.Driver). Registers `ClickHouseDataSource` with health checks, OpenTelemetry tracing, and configuration binding.

## Usage

In your service project:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddClickHouseDataSource("clickhousedb");
```

Then inject via DI:

```csharp
app.MapGet("/data", async (ClickHouseDataSource dataSource) =>
{
    await using var connection = dataSource.CreateConnection();
    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT count() FROM my_table";
    var result = await command.ExecuteScalarAsync();
    return Results.Ok(result);
});
```

## Features

- Registers `ClickHouseDataSource` as a singleton in DI
- Health checks via `PingAsync()`
- OpenTelemetry tracing via the `ClickHouse.Driver` activity source
- Configuration binding from `Aspire:ClickHouse:Driver` section
- Keyed service support for multiple ClickHouse instances

## Configuration

Via `appsettings.json`:

```json
{
  "Aspire": {
    "ClickHouse": {
      "Driver": {
        "DisableHealthChecks": false,
        "DisableTracing": false,
        "HealthCheckTimeout": "00:00:05"
      }
    }
  }
}
```

Or via delegate:

```csharp
builder.AddClickHouseDataSource("clickhousedb", configureSettings: settings =>
{
    settings.DisableHealthChecks = true;
});
```

## Related packages

Use [Aspire.Hosting.ClickHouse](https://www.nuget.org/packages/Aspire.Hosting.ClickHouse) in your AppHost project to orchestrate ClickHouse containers.

## More information

- [GitHub repository](https://github.com/ClickHouse/ClickHouse.Aspire)
- [ClickHouse documentation](https://clickhouse.com/docs)
- [.NET Aspire documentation](https://learn.microsoft.com/dotnet/aspire/)
