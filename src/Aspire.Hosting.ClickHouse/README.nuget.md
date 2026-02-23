# Aspire.Hosting.ClickHouse

ClickHouse hosting integration for [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/). Adds ClickHouse container resources, databases, data volumes, and health checks to the Aspire app model.

## Usage

In your AppHost project, add a ClickHouse server and database:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var clickhouse = builder.AddClickHouse("clickhouse")
                        .WithDataVolume();

var db = clickhouse.AddDatabase("clickhousedb");

builder.AddProject<Projects.MyApi>("api")
       .WithReference(db)
       .WaitFor(db);

builder.Build().Run();
```

## Features

- Runs `clickhouse/clickhouse-server` as a container resource
- Automatic secure password generation
- Automatic database creation on startup via `AddDatabase()`
- Health checks via the ClickHouse HTTP `/ping` endpoint
- Data persistence with `WithDataVolume()` and `WithDataBindMount()`
- Custom credentials via Aspire parameters

## Configuration

Pass custom credentials:

```csharp
var password = builder.AddParameter("clickhouse-pass", secret: true);
var clickhouse = builder.AddClickHouse("clickhouse", password: password);
```

## Related packages

Use [Aspire.ClickHouse.Driver](https://www.nuget.org/packages/Aspire.ClickHouse.Driver) in your service projects to register `ClickHouseDataSource` with health checks, tracing, and configuration binding.

## More information

- [GitHub repository](https://github.com/ClickHouse/ClickHouse.Aspire)
- [ClickHouse documentation](https://clickhouse.com/docs)
- [.NET Aspire documentation](https://learn.microsoft.com/dotnet/aspire/)
