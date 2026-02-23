// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.TestUtilities;

/// <summary>
/// Marks a test as requiring a specific feature (e.g., Docker).
/// Use <see cref="IsFeatureSupported"/> for programmatic feature checks.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequiresFeatureAttribute(TestFeature feature) : Attribute
{
    public TestFeature Feature { get; } = feature;

    /// <summary>
    /// Helper method to check if a specific feature is supported.
    /// </summary>
    public static bool IsFeatureSupported(TestFeature feature)
    {
        if ((feature & TestFeature.Docker) == TestFeature.Docker && !IsDockerSupported())
        {
            return false;
        }
        return true;
    }

    private static bool IsDockerSupported()
    {
        // On Linux (local and CI) docker is always expected.
        // On non-Linux, assume available only for local runs (not CI).
        return OperatingSystem.IsLinux()
            || Environment.GetEnvironmentVariable("BUILD_BUILDID") is null
               && Environment.GetEnvironmentVariable("HELIX_WORKITEM_ROOT") is null
               && Environment.GetEnvironmentVariable("GITHUB_JOB") is null;
    }
}
