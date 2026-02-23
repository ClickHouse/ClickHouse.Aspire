// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ClickHouse.Tests;

public class ClickHousePublicApiTests
{
    [Fact]
    public void AddClickHouseShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        const string name = "ClickHouse";
        int? port = null;

        var action = () => builder.AddClickHouse(name, port);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddClickHouseShouldThrowWhenNameIsNullOrEmpty(bool isNull)
    {
        var builder = DistributedApplication.CreateBuilder();
        var name = isNull ? null! : string.Empty;
        int? port = null;

        var action = () => builder.AddClickHouse(name, port);

        var exception = isNull
            ? Assert.Throws<ArgumentNullException>(action)
            : Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void AddClickHouseWithParametersShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        const string name = "ClickHouse";
        IResourceBuilder<ParameterResource>? userName = null;
        IResourceBuilder<ParameterResource>? password = null;

        var action = () => builder.AddClickHouse(name, userName: userName, password: password);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddClickHouseWithParametersShouldThrowWhenNameIsNullOrEmpty(bool isNull)
    {
        var builder = DistributedApplication.CreateBuilder();
        var name = isNull ? null! : string.Empty;
        IResourceBuilder<ParameterResource>? userName = null;
        IResourceBuilder<ParameterResource>? password = null;

        var action = () => builder.AddClickHouse(name, userName: userName, password: password);

        var exception = isNull
            ? Assert.Throws<ArgumentNullException>(action)
            : Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void AddDatabaseShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<ClickHouseServerResource> builder = null!;
        const string name = "db";

        var action = () => builder.AddDatabase(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddDatabaseShouldThrowWhenNameIsNullOrEmpty(bool isNull)
    {
        var builder = DistributedApplication.CreateBuilder()
            .AddClickHouse("ClickHouse");
        var name = isNull ? null! : string.Empty;

        var action = () => builder.AddDatabase(name);

        var exception = isNull
            ? Assert.Throws<ArgumentNullException>(action)
            : Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void WithDataVolumeShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<ClickHouseServerResource> builder = null!;

        var action = () => builder.WithDataVolume();

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithDataBindMountShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<ClickHouseServerResource> builder = null!;
        const string source = "/ClickHouse/storage";

        var action = () => builder.WithDataBindMount(source);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithDataBindMountShouldThrowWhenSourceIsNullOrEmpty(bool isNull)
    {
        var builder = DistributedApplication.CreateBuilder()
            .AddClickHouse("ClickHouse");
        var source = isNull ? null! : string.Empty;

        var action = () => builder.WithDataBindMount(source);

        var exception = isNull
            ? Assert.Throws<ArgumentNullException>(action)
            : Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(source), exception.ParamName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CtorClickHouseDatabaseResourceShouldThrowWhenNameIsNullOrEmpty(bool isNull)
    {
        var name = isNull ? null! : string.Empty;
        const string databaseName = "db";
        var parent = new ClickHouseServerResource("clickhouse");

        var action = () => new ClickHouseDatabaseResource(name, databaseName, parent);

        var exception = isNull
            ? Assert.Throws<ArgumentNullException>(action)
            : Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CtorClickHouseDatabaseResourceShouldThrowWhenDatabaseNameIsNullOrEmpty(bool isNull)
    {
        const string name = "clickhouse";
        var databaseName = isNull ? null! : string.Empty;
        var parent = new ClickHouseServerResource(name);

        var action = () => new ClickHouseDatabaseResource(name, databaseName, parent);

        var exception = isNull
            ? Assert.Throws<ArgumentNullException>(action)
            : Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(databaseName), exception.ParamName);
    }

    [Fact]
    public void CtorClickHouseDatabaseResourceShouldThrowWhenParentIsNull()
    {
        const string name = "clickhouse";
        const string databaseName = "db";
        ClickHouseServerResource parent = null!;

        var action = () => new ClickHouseDatabaseResource(name, databaseName, parent);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(parent), exception.ParamName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CtorClickHouseServerResourceShouldThrowWhenNameIsNullOrEmpty(bool isNull)
    {
        var name = isNull ? null! : string.Empty;

        var action = () => new ClickHouseServerResource(name);

        var exception = isNull
            ? Assert.Throws<ArgumentNullException>(action)
            : Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }
}
