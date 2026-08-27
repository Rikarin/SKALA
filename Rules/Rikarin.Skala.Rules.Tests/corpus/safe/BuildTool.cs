using System.Diagnostics;
using System.IO;

namespace Corpus.Safe;

/// <summary>
/// ⚠ The shape almost every developer tool has, including Skala. argv and a file the user pointed
/// at are supplied by the principal the process runs as; there is no boundary being crossed, so a
/// report here would assert a vulnerability that does not exist.
/// </summary>
public static class BuildTool {
    public static void Main(string[] args) {
        Process.Start("dotnet", "build " + args[0]);

        var configured = File.ReadAllText(args[1]).Trim();
        Process.Start(new ProcessStartInfo(configured, "--version"));
    }
}
