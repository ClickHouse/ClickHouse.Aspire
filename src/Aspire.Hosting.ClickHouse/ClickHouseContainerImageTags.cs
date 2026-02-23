// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ClickHouse;

internal static class ClickHouseContainerImageTags
{
    /// <remarks>docker.io</remarks>
    public const string Registry = "docker.io";

    /// <remarks>clickhouse/clickhouse-server</remarks>
    public const string Image = "clickhouse/clickhouse-server";

    /// <remarks>latest</remarks>
    public const string Tag = "latest";
}
