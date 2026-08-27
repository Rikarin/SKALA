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

    /// <summary>The distribution package's source directory: one payload, two carriers.</summary>
    public static string CanonicalDirectory { get; } = Path.Combine(Root, "Distribution", "Rikarin.Skala.Canonical");

    public static string CanonicalPayload { get; } = Path.Combine(CanonicalDirectory, "canonical.editorconfig");

    public static string CanonicalManifest { get; } = Path.Combine(CanonicalDirectory, "canonical.json");

    /// <summary>
    /// Vixen's real <c>.editorconfig</c>, vendored — as a **stress case, not a specification**.
    /// </summary>
    /// <remarks>
    /// ⚠ That file was not designed. It accumulated: written by AI agents on the fly, 56
    /// path-scoped sections deep, never reviewed as a whole. Its overrides are not decisions and
    /// must not be read as precedent — Skala's rules are judged on their merits and Vixen conforms
    /// to them, rather than the reverse.
    /// <para>
    /// It earns its place here anyway, and for a better reason than the one first written down.
    /// The sync must preserve a local block **verbatim** precisely because nobody can tell a
    /// reasoned override from an accidental one by looking, so a mechanism that quietly dropped
    /// either would be unsafe on every repository rather than only this one. Preservation is what
    /// makes `sync` runnable on a config nobody has audited; the audit is `SK9013`'s override
    /// report, which is a separate act by a person.
    /// </para>
    /// </remarks>
    public static string VixenEditorConfig { get; } =
        Path.Combine(Root, "Core", "Rikarin.Skala.Core.Tests", "Fixtures", "vixen.editorconfig");

    /// <summary>
    /// A hand-annotated configuration: the case the real export cannot exercise.
    /// </summary>
    /// <remarks>
    /// ⚠ Every comment in <c>editor_config_template</c> is a section banner, so <c>distill</c>
    /// leaving a comment behind after dropping the key beneath it was invisible there — the
    /// orphaned text still read as a heading. In a configuration somebody annotated, the same bug
    /// leaves a paragraph describing a setting that is no longer in the file, which is the one
    /// thing a command whose purpose is "produce a file a human can read" must not do. Each comment
    /// in this fixture says which case it is.
    /// </remarks>
    public static string AnnotatedEditorConfig { get; } =
        Path.Combine(Root, "Core", "Rikarin.Skala.Core.Tests", "Fixtures", "annotated.editorconfig");
}
