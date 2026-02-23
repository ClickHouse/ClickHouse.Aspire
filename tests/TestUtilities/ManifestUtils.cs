// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aspire.Hosting.Utils;

public sealed class ManifestUtils
{
    public static async Task<JsonNode> GetManifest(IResource resource, string? manifestDirectory = null)
    {
        var node = await GetManifestOrNull(resource, manifestDirectory);
        Assert.NotNull(node);
        return node;
    }

    public static async Task<JsonNode?> GetManifestOrNull(IResource resource, string? manifestDirectory = null)
    {
        manifestDirectory ??= Environment.CurrentDirectory;

        using var ms = new MemoryStream();
        var writer = new Utf8JsonWriter(ms);

        var serviceCollection = new ServiceCollection();
        var options = new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Publish);
        var executionContext = new DistributedApplicationExecutionContext(options);
        serviceCollection.AddSingleton(executionContext);
        options.ServiceProvider = serviceCollection.BuildServiceProvider();

        writer.WriteStartObject();
        var context = new ManifestPublishingContext(executionContext, Path.Combine(manifestDirectory, "manifest.json"), writer);
        await WriteResourceAsync(context, resource);
        writer.WriteEndObject();
        writer.Flush();
        ms.Position = 0;
        var obj = JsonNode.Parse(ms);
        Assert.NotNull(obj);
        var resourceNode = obj![resource.Name];
        return resourceNode;
    }

    private static async Task WriteResourceAsync(ManifestPublishingContext context, IResource resource)
    {
        // Check for ManifestPublishingCallbackAnnotation first (this is what the internal WriteResourceAsync does)
        if (resource.TryGetLastAnnotation<ManifestPublishingCallbackAnnotation>(out var callbackAnnotation) && callbackAnnotation.Callback is not null)
        {
            context.Writer.WriteStartObject(resource.Name);
            await callbackAnnotation.Callback(context);
            context.Writer.WriteEndObject();
        }
        else if (resource is ContainerResource container)
        {
            context.Writer.WriteStartObject(resource.Name);
            await context.WriteContainerAsync(container);
            context.Writer.WriteEndObject();
        }
        else if (resource is IResourceWithConnectionString)
        {
            // value.v0 resources
            context.Writer.WriteStartObject(resource.Name);
            context.Writer.WriteString("type", "value.v0");
            context.WriteConnectionString(resource);
            context.Writer.WriteEndObject();
        }
    }
}
