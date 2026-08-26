using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
/// Drops a ready-made regression test when the safety net fires.
/// </summary>
/// <remarks>
/// docs/plan/04 § "The safety net": <c>.skala/crash/&lt;hash&gt;/{input.cs,output.cs,config.snapshot}</c>.
/// The point is that the reproduction exists before anybody thinks to ask for it — a token-stream
/// failure is rare, is a Skala bug, and is nearly impossible to reconstruct from a log line.
/// </remarks>
public static class CrashArtifacts {
    public static string? Write(string? root, string path, string input, string output, in PhaseOneOptions options) {
        if (root is null) {
            return null;
        }

        try {
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)))[..16];
            var directory = Path.Combine(root, "crash", hash);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "input.cs"), input);
            File.WriteAllText(Path.Combine(directory, "output.cs"), output);
            File.WriteAllText(Path.Combine(directory, "config.snapshot"), Snapshot(path, options));
            return directory;
        } catch (IOException) {
            return null;
        } catch (UnauthorizedAccessException) {
            return null;
        }
    }

    static string Snapshot(string path, in PhaseOneOptions options) {
        var builder = new StringBuilder();
        builder.Append("# source: ").AppendLine(path);
        builder.Append("# skala: ").AppendLine(typeof(CrashArtifacts).Assembly.GetName().Version?.ToString() ?? "0.0.0");
        builder.AppendLine();
        builder.Append("indent_size = ").AppendLine(options.IndentSize.ToString(CultureInfo.InvariantCulture));
        builder.Append("max_line_length = ").AppendLine(options.MaxLineLength.ToString(CultureInfo.InvariantCulture));
        builder.Append("keep_blank_lines_in_code = ").AppendLine(
            options.KeepBlankLinesInCode.ToString(CultureInfo.InvariantCulture)
        );
        builder.Append("keep_blank_lines_in_declarations = ").AppendLine(
            options.KeepBlankLinesInDeclarations.ToString(CultureInfo.InvariantCulture)
        );
        builder.Append("new_line_before_open_brace = ").AppendLine(options.NewLineBeforeOpenBrace);
        return builder.ToString();
    }
}
