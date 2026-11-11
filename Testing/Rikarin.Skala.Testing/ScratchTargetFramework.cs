namespace Rikarin.Skala.Testing;

/// <summary>
///     The target framework a test writes into a scratch project it loads without restoring it.
/// </summary>
/// <remarks>
///     ⚠ <b>It must be the installed SDK's own bundled targeting pack, and nothing older.</b> A scratch
///     project naming any other framework needs <c>Microsoft.NETCore.App.Ref</c> from NuGet, and only
///     <c>dotnet restore</c> fetches it. <c>MSBuildWorkspace</c>'s design-time build does not restore,
///     does not report the missing pack (the SDK turns <c>GenerateErrorForMissingTargetingPacks</c>
///     off for design-time builds), and hands back a compilation with no references at all. Measured
///     when the tool profile moved to <c>net11.0</c> with SDK 11.0.100 the only SDK installed: every
///     scratch project still naming <c>net10.0</c> loaded with <b>zero</b> workspace diagnostics and
///     produced <c>CS0246 The type or namespace name 'System' could not be found</c> — and a fix test
///     that expected a revert saw the fix applied, because nothing bound before or after it.
///     <para>
///         So this moves with <c>Directory.Build.props</c>'s tool profile and with
///         <c>global.json</c>'s SDK. A test that means a specific older framework (a multi-target or
///         a reference-set test) names it literally and restores first.
///     </para>
///     <para>
///         It is linked as source into <c>Rikarin.Skala.Analysis.Tests</c>, which does not reference
///         this assembly.
///     </para>
/// </remarks>
public static class ScratchTargetFramework {
    public const string Current = "net11.0";
}
