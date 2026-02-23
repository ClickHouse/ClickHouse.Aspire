var builder = DistributedApplication.CreateBuilder(args);

var clickhouse = builder.AddClickHouse("clickhouse")
                        .WithDataVolume();

var db = clickhouse.AddDatabase("clickhousedb");

builder.AddProject<Projects.ClickHouseEndToEnd_ApiService>("apiservice")
       .WithExternalHttpEndpoints()
       .WithReference(db)
       .WaitFor(db);

builder.Build().Run();
