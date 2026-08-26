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
            + "It is a developer-machine and nightly dependency only; the day-to-day test run reads the committed fixtures (ADR-011).");

    static IEnumerable<string> Candidates() {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(home, ".dotnet", "tools", "jb");
        yield return Path.Combine(home, ".dotnet", "tools", "jb.exe");
        yield return "/usr/local/bin/jb";
    }

    /// <summary>Formats a directory of <c>.cs</c> files in place and returns what changed.</summary>
    public IReadOnlyDictionary<string, string> Format(IReadOnlyList<CorpusFile> files, string editorConfigPath) {
        var scratch = Directory.CreateTempSubdirectory("skala-oracle-");
        try {
            File.Copy(editorConfigPath, Path.Combine(scratch.FullName, ".editorconfig"));
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

            Run(scratch.FullName, "cleanupcode", "--no-build", "--profile=" + Profile,
                "--settings=" + settings, "--verbosity=WARN", Path.Combine(scratch.FullName, "Oracle.sln"));

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
