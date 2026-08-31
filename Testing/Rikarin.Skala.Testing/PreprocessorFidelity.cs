using Microsoft.CodeAnalysis;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Reporting;
using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Testing;

/// <summary>
///     SK-DIV-0004, measured: what the fidelity number is once the formatter has the symbols.
/// </summary>
/// <remarks>
///     ⚠ The symbol set is not a list someone typed. <see cref="OracleRunner" /> builds the fixtures in a
///     scratch <c>net10.0</c> project under <c>Debug</c>, so what the oracle saw is exactly what the SDK
///     defines for that project — and the only honest way to know that is to build the same project
///     with <c>-bl</c> and read the symbols back out through the loader <c>skala check</c> uses. That
///     makes this both the fidelity measurement and an end-to-end test of the binlog path.
/// </remarks>
public static class PreprocessorFidelity {
    /// <summary>The project the oracle's fixtures were produced under, as MSBuild sees it.</summary>
    const string ProbeProject = """
                                <Project Sdk="Microsoft.NET.Sdk">
                                  <PropertyGroup>
                                    <TargetFramework>net10.0</TargetFramework>
                                    <Nullable>enable</Nullable>
                                    <ImplicitUsings>enable</ImplicitUsings>
                                    <LangVersion>preview</LangVersion>
                                    <OutputType>Library</OutputType>
                                  </PropertyGroup>
                                </Project>
                                """;

    /// <summary>
    ///     Builds a copy of the oracle's project with a binary log and reads the symbols out of it.
    /// </summary>
    public static IReadOnlyList<string> OracleSymbols(TextWriter log) {
        var scratch = Directory.CreateTempSubdirectory("skala-symbols-");
        try {
            File.WriteAllText(Path.Combine(scratch.FullName, "Probe.csproj"), ProbeProject);
            File.WriteAllText(Path.Combine(scratch.FullName, "Probe.cs"), "public sealed class Probe;\n");

            var binlog = Path.Combine(scratch.FullName, "artifacts", "skala.binlog");
            Directory.CreateDirectory(Path.GetDirectoryName(binlog)!);

            var start = new System.Diagnostics.ProcessStartInfo("dotnet") {
                WorkingDirectory = scratch.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                ArgumentList = {
                    "build",
                    "-c",
                    "Debug",
                    "-bl:" + binlog,
                    "-v",
                    "q",
                    "--nologo"
                }
            };

            using (var process = System.Diagnostics.Process.Start(start)) {
                var output = process!.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) {
                    log.WriteLine("the probe build failed:");
                    log.WriteLine(output);
                    log.WriteLine(error);
                }
            }

            if (!File.Exists(binlog)) {
                log.WriteLine("the probe build produced no binary log; falling back to DEBUG;TRACE");
                return ["DEBUG", "TRACE"];
            }

            var loaded = ProjectLoader.Load(
                new LoadRequest {
                    RepositoryRoot = scratch.FullName,
                    Mode = LoadMode.Binlog,
                    BinlogPath = binlog,
                    AllowFallback = false
                }
            );

            if (loaded.IsEmpty) {
                log.WriteLine("the binary log named no Csc invocation; falling back to DEBUG;TRACE");
                foreach (var diagnostic in loaded.Diagnostics) {
                    log.WriteLine("  " + diagnostic);
                }

                return ["DEBUG", "TRACE"];
            }

            var symbols = loaded.Units[0].PreprocessorSymbols.Sort(StringComparer.Ordinal);
            log.WriteLine(
                $"binlog: {loaded.Summary}, {symbols.Length.ToString(CultureInfo.InvariantCulture)} preprocessor symbol(s)"
            );

            return symbols;
        } finally {
            try {
                scratch.Delete(true);
            } catch (IOException) { }
        }
    }

    /// <summary>
    ///     The number docs/plan/15 § M5 asks for: fidelity with symbols, overall and on the
    ///     <c>#if</c> files.
    /// </summary>
    public static string Measure(IReadOnlyList<string> symbols, string set = Corpus.Real) {
        var files = Corpus.Files(set).Where(static file => file.HasFixture).ToArray();
        var withDirectives = new List<(string File, string Expected, string Actual)>();
        var withoutDirectives = new List<(string File, string Expected, string Actual)>();
        var baselineWith = new List<(string File, string Expected, string Actual)>();
        var baselineWithout = new List<(string File, string Expected, string Actual)>();

        foreach (var file in files) {
            var text = CSharpFormatter.Read(file.Path);
            var options = Rikarin.Skala.Core.Configuration.OptionResolver.Resolve(file.Path).Options;
            var expected = OracleFixture.Read(file);

            var withSymbols = CSharpFormatter.Format(file.Path, text, options, null, symbols).Formatted;
            var without = CSharpFormatter.Format(file.Path, text, options).Formatted;

            if (HasConditional(text.ToString())) {
                withDirectives.Add((file.ToString(), expected, withSymbols));
                baselineWith.Add((file.ToString(), expected, without));
            } else {
                withoutDirectives.Add((file.ToString(), expected, withSymbols));
                baselineWithout.Add((file.ToString(), expected, without));
            }
        }

        var builder = new StringBuilder();
        builder.Append("preprocessor symbols supplied: ").AppendLine(string.Join(" ", symbols));
        builder.AppendLine();
        builder.AppendLine("                              no symbols            with symbols");
        builder.AppendLine("                              line     file         line     file      files");

        Row(builder, "containing a #if", baselineWith, withDirectives);
        Row(builder, "no #if", baselineWithout, withoutDirectives);
        Row(
            builder,
            "overall",
            [.. baselineWith, .. baselineWithout],
            [.. withDirectives, .. withoutDirectives]
        );

        builder.AppendLine();
        builder.AppendLine("what is left in the `#if` files, with symbols supplied:");
        builder.AppendLine();
        builder.Append(Fidelity.Compare(withDirectives).Render(8));
        return builder.ToString();
    }

    static void Row(
        StringBuilder builder,
        string label,
        List<(string File, string Expected, string Actual)> before,
        List<(string File, string Expected, string Actual)> after
    ) {
        var b = Fidelity.Compare(before);
        var a = Fidelity.Compare(after);
        builder.Append("  ")
            .Append(label.PadRight(28))
            .Append((b.LineFidelity * 100).ToString("F2", CultureInfo.InvariantCulture).PadLeft(6))
            .Append(" % ")
            .Append((b.FileFidelity * 100).ToString("F2", CultureInfo.InvariantCulture).PadLeft(6))
            .Append(" %  ")
            .Append((a.LineFidelity * 100).ToString("F2", CultureInfo.InvariantCulture).PadLeft(6))
            .Append(" % ")
            .Append((a.FileFidelity * 100).ToString("F2", CultureInfo.InvariantCulture).PadLeft(6))
            .Append(" %  ")
            .Append(a.Files.ToString(CultureInfo.InvariantCulture).PadLeft(5))
            .AppendLine();
    }

    /// <summary>
    ///     Whether a file contains a conditional directive at all.
    /// </summary>
    /// <remarks>
    ///     ⚠ Lexical rather than through the tree, because the tree is what changes when the symbols
    ///     change and the split has to be the same population in both columns.
    /// </remarks>
    public static bool HasConditional(string text) {
        foreach (var line in text.Split('\n')) {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("#if", StringComparison.Ordinal)) {
                return true;
            }
        }

        return false;
    }
}
