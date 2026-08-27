using System.Xml.Linq;

namespace Rikarin.Skala.Release;

/// <summary>
/// Where the version lives. <c>Directory.Build.props</c>, and nowhere else.
/// </summary>
/// <remarks>
/// ⚠ docs/plan/18 § "One source of truth". <c>VersionPrefix</c> and <c>VersionSuffix</c> in
/// <c>Directory.Build.props</c> are the version of all five packages; MSBuild composes them into
/// <c>Version</c>, and the release workflow overrides <c>Version</c> from the computed number rather
/// than editing anything. Two files that both carry a number are two versions, and the second one
/// drifts — <c>VersionSourcesTests</c> in <c>Rikarin.Skala.Core.Tests</c> is what keeps them from
/// appearing.
/// <para>
/// ⚠ <c>Distribution/Rikarin.Skala.Canonical/canonical.json</c> is <b>not</b> one of these, on
/// purpose and permanently. It carries the version of the canonical <i>payload</i>, which moves when
/// somebody re-exports from Rider, and a repository must be able to take a tool bug fix without
/// taking a repository-wide reformat. Nothing in this file reads it and nothing in the pipeline
/// derives one from the other; <c>./build.sh Canonical --canonical-version</c> is the only thing
/// that sets it.
/// </para>
/// </remarks>
public static class VersionSources {
    public static string PropsPath(string repositoryRoot) => Path.Combine(repositoryRoot, "Directory.Build.props");

    /// <summary>The version <c>Directory.Build.props</c> declares.</summary>
    public static SemanticVersion Declared(string repositoryRoot) {
        var path = PropsPath(repositoryRoot);
        var document = XDocument.Load(path);

        var prefix = Value(document, "VersionPrefix")
            ?? throw new InvalidOperationException($"'{path}' has no <VersionPrefix>.");
        var suffix = Value(document, "VersionSuffix");

        var text = string.IsNullOrEmpty(suffix) ? prefix : prefix + "-" + suffix;
        return SemanticVersion.TryParse(text, out var version)
            ? version
            : throw new InvalidOperationException($"'{path}' declares '{text}', which is not a semantic version.");
    }

    static string? Value(XDocument document, string name) =>
        document.Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, name, StringComparison.Ordinal))
                ?.Value.Trim();
}
