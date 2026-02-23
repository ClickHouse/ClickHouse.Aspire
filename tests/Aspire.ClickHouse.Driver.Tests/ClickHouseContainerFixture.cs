// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ClickHouse;
using Aspire.TestUtilities;
using Aspire.Components.Common.TestUtilities;
using Testcontainers.ClickHouse;
using Xunit;

namespace Aspire.ClickHouse.Driver.Tests;

public sealed class ClickHouseContainerFixture : IAsyncLifetime
{
    public ClickHouseContainer? Container { get; private set; }

    public string GetConnectionString() => Container?.GetConnectionString() ??
        throw new InvalidOperationException("The test container was not initialized.");

    public async Task InitializeAsync()
    {
        if (RequiresFeatureAttribute.IsFeatureSupported(TestFeature.Docker))
        {
            Container = new ClickHouseBuilder()
                .WithImage($"{ClickHouseContainerImageTags.Registry}/{ClickHouseContainerImageTags.Image}:{ClickHouseContainerImageTags.Tag}")
                .Build();
            await Container.StartAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (Container is not null)
        {
            await Container.DisposeAsync();
        }
    }
}
