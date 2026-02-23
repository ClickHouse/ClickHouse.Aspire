// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a ClickHouse database. This is a child resource of a <see cref="ClickHouseServerResource"/>.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="databaseName">The database name.</param>
/// <param name="parent">The ClickHouse server resource associated with this database.</param>
[DebuggerDisplay("Type = {GetType().Name,nq}, Name = {Name}, Database = {DatabaseName}")]
public class ClickHouseDatabaseResource(string name, string databaseName, ClickHouseServerResource parent)
    : Resource(name), IResourceWithParent<ClickHouseServerResource>, IResourceWithConnectionString
{
    /// <summary>
    /// Gets the connection string expression for the ClickHouse database.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => Parent.BuildConnectionString(DatabaseName);

    /// <summary>
    /// Gets the parent ClickHouse container resource.
    /// </summary>
    public ClickHouseServerResource Parent { get; } = parent ?? throw new ArgumentNullException(nameof(parent));

    /// <summary>
    /// Gets the database name.
    /// </summary>
    public string DatabaseName { get; } = ThrowIfNullOrEmpty(databaseName);

    private static string ThrowIfNullOrEmpty([NotNull] string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(argument, paramName);
        return argument;
    }

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties() =>
        Parent.CombineProperties([
            new("DatabaseName", ReferenceExpression.Create($"{DatabaseName}")),
        ]);
}
