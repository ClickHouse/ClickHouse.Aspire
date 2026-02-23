// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ClickHouse.Tests;

public class ConnectionPropertiesTests
{
    [Fact]
    public void ClickHouseServerResourceGetConnectionPropertiesReturnsExpectedValues()
    {
        var user = new ParameterResource("user", _ => "clickUser");
        var password = new ParameterResource("password", _ => "p@ssw0rd1", secret: true);
        var resource = new ClickHouseServerResource("clickhouse", user, password);

        var properties = ((IResourceWithConnectionString)resource).GetConnectionProperties().ToArray();

        Assert.Collection(
            properties,
            property =>
            {
                Assert.Equal("Host", property.Key);
                Assert.Equal("{clickhouse.bindings.http.host}", property.Value.ValueExpression);
            },
            property =>
            {
                Assert.Equal("Port", property.Key);
                Assert.Equal("{clickhouse.bindings.http.port}", property.Value.ValueExpression);
            },
            property =>
            {
                Assert.Equal("Username", property.Key);
                Assert.Equal("{user.value}", property.Value.ValueExpression);
            },
            property =>
            {
                Assert.Equal("Password", property.Key);
                Assert.Equal("{password.value}", property.Value.ValueExpression);
            });
    }

    [Fact]
    public void ClickHouseServerResourceWithoutPasswordGetConnectionPropertiesReturnsExpectedValues()
    {
        var resource = new ClickHouseServerResource("clickhouse");

        var properties = ((IResourceWithConnectionString)resource).GetConnectionProperties().ToArray();

        Assert.Collection(
            properties,
            property =>
            {
                Assert.Equal("Host", property.Key);
                Assert.Equal("{clickhouse.bindings.http.host}", property.Value.ValueExpression);
            },
            property =>
            {
                Assert.Equal("Port", property.Key);
                Assert.Equal("{clickhouse.bindings.http.port}", property.Value.ValueExpression);
            },
            property =>
            {
                Assert.Equal("Username", property.Key);
                Assert.Equal("default", property.Value.ValueExpression);
            });
    }

    [Fact]
    public void ClickHouseDatabaseResourceGetConnectionPropertiesIncludesDatabaseSpecificValues()
    {
        var user = new ParameterResource("user", _ => "clickUser");
        var password = new ParameterResource("password", _ => "p@ssw0rd1", secret: true);
        var server = new ClickHouseServerResource("clickhouse", user, password);
        var resource = new ClickHouseDatabaseResource("clickhouseDb", "Products", server);

        var properties = ((IResourceWithConnectionString)resource).GetConnectionProperties().ToArray();

        Assert.Contains(properties, property => property.Key == "Host" && property.Value.ValueExpression == "{clickhouse.bindings.http.host}");
        Assert.Contains(properties, property => property.Key == "Port" && property.Value.ValueExpression == "{clickhouse.bindings.http.port}");
        Assert.Contains(properties, property => property.Key == "Username" && property.Value.ValueExpression == "{user.value}");
        Assert.Contains(properties, property => property.Key == "Password" && property.Value.ValueExpression == "{password.value}");
        Assert.Contains(properties, property => property.Key == "DatabaseName" && property.Value.ValueExpression == "Products");
    }
}
