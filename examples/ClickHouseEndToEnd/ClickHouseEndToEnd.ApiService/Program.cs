using ClickHouse.Driver.ADO;
using ClickHouse.Driver.ADO.Parameters;
using ClickHouse.Driver.Utility;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddClickHouseDataSource("clickhousedb");

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapPost("/init", async (ClickHouseDataSource dataSource) =>
{
    await using var connection = dataSource.CreateConnection();
    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE IF NOT EXISTS hits (
            timestamp DateTime64(3) DEFAULT now64(3),
            url String
        ) ENGINE = MergeTree()
        ORDER BY timestamp
        """;
    await command.ExecuteNonQueryAsync();
    return Results.Ok("Table 'hits' created.");
});

app.MapPost("/hits", async (HitRequest request, ClickHouseDataSource dataSource) =>
{
    var client = dataSource.GetClient();
    var parameters = new ClickHouseParameterCollection();
    parameters.AddParameter("url", request.Url);
    await client.ExecuteNonQueryAsync("INSERT INTO hits (url) VALUES ({url:String})", parameters);

    return Results.Ok("Hit recorded.");
});

app.MapGet("/hits", async (ClickHouseDataSource dataSource) =>
{
    await using var connection = dataSource.CreateConnection();
    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT timestamp, url FROM hits ORDER BY timestamp";
    await using var reader = await command.ExecuteReaderAsync();

    var hits = new List<HitResponse>();
    while (await reader.ReadAsync())
    {
        hits.Add(new HitResponse(reader.GetDateTime(0), reader.GetString(1)));
    }
    return Results.Ok(hits);
});

app.Run();

record HitRequest(string Url);
record HitResponse(DateTime Timestamp, string Url);
