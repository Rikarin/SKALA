namespace Rikarin.Skala.Sdk;

/// <summary>
///     This package is three dependencies, two starter files and one message. The assembly exists
///     because a .csproj compiles one, and it is not included in the package
///     (<c>IncludeBuildOutput=false</c>).
/// </summary>
static class PackageMarker {
    /// <summary>Where the starter files live inside the package, for anything that goes looking.</summary>
    public const string StarterPath = "content/skala.editorconfig";
}
