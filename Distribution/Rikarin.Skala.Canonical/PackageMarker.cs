namespace Rikarin.Skala.Canonical;

/// <summary>
/// This package carries data, not code. The assembly exists because a .csproj compiles one, and it
/// is not included in the package (<c>IncludeBuildOutput=false</c>).
/// </summary>
static class PackageMarker {
    /// <summary>Where the payload lives inside the package, for anything that goes looking.</summary>
    public const string ContentPath = "content/canonical.editorconfig";
}
