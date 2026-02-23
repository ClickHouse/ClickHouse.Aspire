// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a ClickHouse container.
/// </summary>
/// <param name="name">The name of the resource.</param>
public class ClickHouseServerResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";
    private const string DefaultUserName = "default";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Initializes a resource that represents a ClickHouse container.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    /// <param name="userNameParameter">A parameter that contains the ClickHouse server user name, or <see langword="null"/> to use a default value.</param>
    /// <param name="passwordParameter">A parameter that contains the ClickHouse server password, or <see langword="null"/> for no password.</param>
    public ClickHouseServerResource(string name, ParameterResource? userNameParameter, ParameterResource? passwordParameter) : this(name)
    {
        UserNameParameter = userNameParameter;
        PasswordParameter = passwordParameter;
    }

    /// <summary>
    /// Gets the primary endpoint for the ClickHouse server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the host expression for this resource.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port expression for this resource.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the parameter that contains the ClickHouse server password.
    /// </summary>
    public ParameterResource? PasswordParameter { get; }

    /// <summary>
    /// Gets the parameter that contains the ClickHouse server username.
    /// </summary>
    public ParameterResource? UserNameParameter { get; }

    /// <summary>
    /// Gets a reference to the user name for the ClickHouse server.
    /// </summary>
    /// <remarks>
    /// Returns the user name parameter if specified, otherwise returns the default user name "default".
    /// </remarks>
    public ReferenceExpression UserNameReference =>
        UserNameParameter is not null ?
            ReferenceExpression.Create($"{UserNameParameter}") :
            ReferenceExpression.Create($"{DefaultUserName}");

    /// <summary>
    /// Gets the connection string expression for the ClickHouse server.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => BuildConnectionString();

    internal ReferenceExpression BuildConnectionString(string? databaseName = null)
    {
        var builder = new ReferenceExpressionBuilder();
        builder.Append($"Host={Host};Port={Port}");

        if (UserNameParameter is not null)
        {
            builder.Append($";Username={UserNameParameter}");
        }
        else
        {
            builder.AppendLiteral($";Username={DefaultUserName}");
        }

        if (PasswordParameter is not null)
        {
            builder.Append($";Password={PasswordParameter}");
        }

        if (databaseName is not null)
        {
            builder.Append($";Database={databaseName}");
        }

        return builder.Build();
    }

    private readonly Dictionary<string, string> _databases = new Dictionary<string, string>(StringComparers.ResourceName);

    /// <summary>
    /// A dictionary where the key is the resource name and the value is the database name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Databases => _databases;

    internal void AddDatabase(string name, string databaseName)
    {
        _databases.TryAdd(name, databaseName);
    }

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Username", UserNameReference);

        if (PasswordParameter is not null)
        {
            yield return new("Password", ReferenceExpression.Create($"{PasswordParameter}"));
        }
    }
}
