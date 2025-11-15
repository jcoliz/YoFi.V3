// Copyright (C) 2025 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved

namespace YoFi.V3.Entities.Options;

/// <summary>
/// Application-wide configuration options
/// </summary>
/// <remarks>
/// Not absolutely sure where these should be defined. They are needed by the
/// Controllers project to provide version info, but they are also used
/// during startup configuration in the Web project.
/// </remarks>
public record ApplicationOptions
{
    public static readonly string Section = "Application";

    /// <summary>
    /// What environment the application is running in
    /// </summary>
    public EnvironmentType Environment { get; init; }

    /// <summary>
    /// Application version
    /// </summary>
    /// <remarks>
    /// Injected by the build system
    /// </remarks>
    public string? Version { get; init; }
}

public enum EnvironmentType
{
    Production = 0,
    Container,
    Local
}
