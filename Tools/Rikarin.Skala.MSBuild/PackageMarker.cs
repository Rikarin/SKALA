namespace Rikarin.Skala.MSBuild;

/// <summary>
///     This package carries MSBuild logic, not code. The assembly exists because a .csproj compiles
///     one, and it is not included in the package (<c>IncludeBuildOutput=false</c>).
/// </summary>
/// <remarks>
///     ⚠ There is deliberately no MSBuild <c>Task</c> here — see the header of
///     <c>build/Rikarin.Skala.MSBuild.targets</c>, and docs/plan/02 § "Package boundaries". Everything
///     the integration does is start <c>skala</c> and read an exit code, which <c>Exec</c> already does,
///     and a task assembly would have to load into three different MSBuild hosts to do it.
/// </remarks>
static class PackageMarker {
    /// <summary>The property a repository sets to turn the whole integration off.</summary>
    public const string DisableProperty = "SkalaEnabled";
}
