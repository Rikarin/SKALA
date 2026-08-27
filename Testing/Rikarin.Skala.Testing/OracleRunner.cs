using System.Diagnostics;
using System.Text;

namespace Rikarin.Skala.Testing;

/// <summary>
/// Runs <c>jb cleanupcode</c> over a corpus set and writes the <c>.expected.cs</c> fixtures.
/// </summary>
/// <remarks>
/// ⚠ Only <c>./build.sh Oracle</c> calls this. It is a deliberate, reviewed action: an oracle that
/// regenerates when it disagrees is not an oracle (docs/plan/12 § "The oracle").
/// <para>
/// <c>cleanupcode</c> rewrites files in place and wants a project, so the harness copies the corpus
/// into a scratch project with a copy of the repository's <c>.editorconfig</c> and a cleanup
/// profile that enables formatting only, runs the tool, and reads the results back out.
/// </para>
/// </remarks>
public sealed class OracleRunner {
    /// <summary>The profile name the settings file below defines.</summary>
    public const string Profile = "SkalaFormatOnly";

    readonly string _executable;

    public OracleRunner(string? executable = null) => _executable = executable ?? FindExecutable();

    public string Version {
        get {
            var output = Run(Path.GetTempPath(), "cleanupcode", "--version");
            foreach (var line in output.Split('\n')) {
                if (line.StartsWith("Version:", StringComparison.Ordinal)) {
                    return line["Version:".Length..].Trim();
                }
            }

            return "unknown";
        }
    }

    public static string? FindExecutableOrNull() {
        foreach (var candidate in Candidates()) {
            if (File.Exists(candidate)) {
                return candidate;
            }
        }

        return null;
    }

    static string FindExecutable() =>
        FindExecutableOrNull()
        ?? throw new InvalidOperationException(
            "jb (JetBrains.ReSharper.GlobalTools) is not installed. `dotnet tool install -g JetBrains.ReSharper.GlobalTools --version 2025.2.6`. "
            + "It is a developer-machine and nightly dependency only; the day-to-day test run reads the committed fixtures (ADR-011)."
        );

    static IEnumerable<string> Candidates() {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(home, ".dotnet", "tools", "jb");
        yield return Path.Combine(home, ".dotnet", "tools", "jb.exe");
        yield return "/usr/local/bin/jb";
    }

    /// <summary>
    /// Formats a directory of <c>.cs</c> files in place and returns what changed.
    /// </summary>
    /// <param name="overrides">
    /// Keys appended to the copied <c>.editorconfig</c>'s <c>[*.cs]</c> section, so that one fixture
    /// set can be regenerated under a configuration other than the repository's. ⚠ Appended rather
    /// than substituted: an .editorconfig's last assignment of a key within a section wins, so this
    /// overrides whatever the export set without having to find it.
    /// </param>
    public IReadOnlyDictionary<string, string> Format(
        IReadOnlyList<CorpusFile> files,
        string editorConfigPath,
        IReadOnlyList<KeyValuePair<string, string>>? overrides = null
    ) {
        var scratch = Directory.CreateTempSubdirectory("skala-oracle-");
        try {
            File.Copy(editorConfigPath, Path.Combine(scratch.FullName, ".editorconfig"));
            if (overrides is { Count: > 0 }) {
                var appended = new StringBuilder();
                appended.AppendLine();
                appended.AppendLine("[*.cs]");
                foreach (var (key, value) in overrides) {
                    appended.Append(key).Append(" = ").AppendLine(value);
                }

                File.AppendAllText(Path.Combine(scratch.FullName, ".editorconfig"), appended.ToString());
            }

            File.WriteAllText(Path.Combine(scratch.FullName, "Oracle.csproj"), ProjectFile);
            File.WriteAllText(Path.Combine(scratch.FullName, "Oracle.sln"), SolutionFile);
            var settings = Path.Combine(scratch.FullName, "Oracle.sln.DotSettings");
            File.WriteAllText(settings, SettingsFile);

            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < files.Count; i++) {
                var name = $"F{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}.cs";
                File.Copy(files[i].Path, Path.Combine(scratch.FullName, name));
                names[name] = files[i].Path;
            }

            Run(
                scratch.FullName,
                "cleanupcode",
                "--no-build",
                "--profile=" + Profile,
                "--settings=" + settings,
                "--verbosity=WARN",
                Path.Combine(scratch.FullName, "Oracle.sln")
            );

            var results = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (name, original) in names) {
                var produced = Path.Combine(scratch.FullName, name);
                if (File.Exists(produced)) {
                    results[original] = File.ReadAllText(produced);
                }
            }

            return results;
        } finally {
            try {
                scratch.Delete(recursive: true);
            } catch (IOException) {
                // A scratch directory the tool still holds open is not worth failing a build over.
            }
        }
    }

    /// <summary>
    /// Formats one batch of files where each file carries its own <c>.editorconfig</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ The isolation is a subdirectory per file, and that is what makes the defaults probe answer
    /// a question about one option rather than about a configuration. Batching by value index — one
    /// run with every option at its 1st value, one at its 2nd — is the only affordable shape,
    /// because <c>cleanupcode</c>'s startup dominates; but with one shared configuration every
    /// fixture is moved by whatever else is in the batch, and the first attempt at this came back
    /// with 197 options and zero fixtures unchanged. A directory per file, each with its own
    /// <c>root = true</c> plus one key, gives the batching for free and the isolation with it.
    /// </remarks>
    public IReadOnlyDictionary<string, string> FormatIsolated(IReadOnlyList<(CorpusFile File, string Config)> work) {
        var scratch = Directory.CreateTempSubdirectory("skala-isolated-");
        try {
            File.WriteAllText(Path.Combine(scratch.FullName, "Oracle.csproj"), ProjectFile);
            File.WriteAllText(Path.Combine(scratch.FullName, "Oracle.sln"), SolutionFile);
            var settings = Path.Combine(scratch.FullName, "Oracle.sln.DotSettings");
            File.WriteAllText(settings, SettingsFile);

            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < work.Count; i++) {
                var directory = Path.Combine(scratch.FullName, "d" + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, ".editorconfig"), work[i].Config);
                var produced = Path.Combine(directory, "F.cs");
                File.Copy(work[i].File.Path, produced);
                names[produced] = work[i].File.Path;
            }

            Run(
                scratch.FullName,
                "cleanupcode",
                "--no-build",
                "--profile=" + Profile,
                "--settings=" + settings,
                "--verbosity=WARN",
                Path.Combine(scratch.FullName, "Oracle.sln")
            );

            var results = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (produced, original) in names) {
                if (File.Exists(produced)) {
                    results[original] = File.ReadAllText(produced);
                }
            }

            return results;
        } finally {
            try {
                scratch.Delete(recursive: true);
            } catch (IOException) {
                // A scratch directory the tool still holds open is not worth failing a build over.
            }
        }
    }

    string Run(string workingDirectory, params string[] arguments) {
        var start = new ProcessStartInfo(_executable) {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"{_executable} did not start.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return output + error;
    }

    const string ProjectFile = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <LangVersion>preview</LangVersion>
            <NoWarn>$(NoWarn);CS1591;CS0168;CS0219;CS8321;CS0067;CS0169</NoWarn>
            <EnableDefaultCompileItems>true</EnableDefaultCompileItems>
            <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
            <OutputType>Library</OutputType>
          </PropertyGroup>
        </Project>
        """;

    const string SolutionFile = """
        Microsoft Visual Studio Solution File, Format Version 12.00
        Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Oracle", "Oracle.csproj", "{11111111-1111-1111-1111-111111111111}"
        EndProject
        Global
        	GlobalSection(SolutionConfigurationPlatforms) = preSolution
        		Debug|Any CPU = Debug|Any CPU
        	EndGlobalSection
        	GlobalSection(ProjectConfigurationPlatforms) = postSolution
        		{11111111-1111-1111-1111-111111111111}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        		{11111111-1111-1111-1111-111111111111}.Debug|Any CPU.Build.0 = Debug|Any CPU
        	EndGlobalSection
        EndGlobal
        """;

    /// <summary>
    /// A cleanup profile with the formatting half on and the arrangement half off. ⚠ The two are
    /// compared separately (docs/plan/12): arrangement is a tree rewrite and belongs to milestone 4.
    /// </summary>
    static string SettingsFile { get; } =
        """
        <wpf:ResourceDictionary xml:space="preserve" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:s="clr-namespace:System;assembly=mscorlib" xmlns:ss="urn:shemas-jetbrains-com:settings-storage-xaml" xmlns:wpf="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
        	<s:String x:Key="/Default/CodeStyle/CodeCleanup/Profiles/=PROFILE/@EntryIndexedValue">&lt;?xml version="1.0" encoding="utf-16"?&gt;&lt;Profile name="PROFILE"&gt;&lt;CSReformatCode&gt;True&lt;/CSReformatCode&gt;&lt;CSUpdateFileHeader&gt;False&lt;/CSUpdateFileHeader&gt;&lt;/Profile&gt;</s:String>
        </wpf:ResourceDictionary>
        """.Replace("PROFILE", Profile, StringComparison.Ordinal);
}
