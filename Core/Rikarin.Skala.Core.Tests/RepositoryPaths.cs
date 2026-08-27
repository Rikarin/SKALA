using System.Reflection;

namespace Rikarin.Skala.Core.Tests;

/// <summary>
/// The real repository, because the real <c>editor_config_template</c> is the fixture for all of
/// M0 (docs/plan/15 § M0: the definition of done is stated over the actual export, not a toy one).
/// </summary>
public static class RepositoryPaths {
    public static string Root { get; } =
        Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "SkalaRepositoryRoot")?.Value
        ?? throw new InvalidOperationException("SkalaRepositoryRoot was not stamped into the test assembly.");

    /// <summary>The Rider export, unmodified. Never written to.</summary>
    public static string Template { get; } = Path.Combine(Root, "editor_config_template");

    /// <summary>The repository's own .editorconfig: the export with `root = true` (ADR-015).</summary>
    public static string EditorConfig { get; } = Path.Combine(Root, ".editorconfig");

    public static string SampleSourceFile { get; } = Path.Combine(Root, "Core", "Rikarin.Skala.Core", "Sample.cs");
}
