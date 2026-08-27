using System.Diagnostics;
using System.Security.Cryptography;

namespace Rikarin.Skala.Release;

public sealed record ToolRun(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
///     One <c>skala</c> build, driven as a process.
/// </summary>
/// <remarks>
///     ⚠ Two things here are correctness rather than convenience.
///     <list type="bullet">
///         <item>
///             <c>SKALA_NO_DAEMON=1</c> on every invocation. The format daemon is per-repository and version
///             stamped, and two tool versions racing for one daemon is precisely the situation doc 11
///             § "Distribution" calls a merge-conflict generator. A detector whose two halves shared a daemon
///             would measure the daemon.
///         </item>
///         <item>
///             <see cref="Fingerprint" /> is the SHA-256 of the binary itself, printed in the release notes.
///             It is what makes "the baseline and the candidate were different builds" a checkable claim rather
///             than an assumption — see <c>OutputSurface.Run</c>, which refuses two tools with the same one.
///         </item>
///     </list>
/// </remarks>
public sealed class SkalaTool {
    SkalaTool(string path, bool managed) {
        Path = path;
        Managed = managed;
        Fingerprint = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    }

    public string Path { get; }

    /// <summary>A framework-dependent <c>.dll</c>, which is launched through <c>dotnet</c>.</summary>
    public bool Managed { get; }

    public string Fingerprint { get; }

    public static SkalaTool At(string path) {
        var full = System.IO.Path.GetFullPath(path);
        if (!File.Exists(full)) {
            throw new FileNotFoundException($"No skala binary at '{full}'.", full);
        }

        return new SkalaTool(full, full.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
    }

    public ToolRun Run(string workingDirectory, params string[] arguments) {
        var start = new ProcessStartInfo(Managed ? "dotnet" : Path) {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory
        };

        if (Managed) {
            start.ArgumentList.Add(Path);
        }

        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }

        // See the class remarks: the daemon is shared per repository and stamped with a version.
        start.Environment["SKALA_NO_DAEMON"] = "1";

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"'{Path}' did not start.");

        // Read both pipes before waiting: a tool that fills one of them while we wait deadlocks.
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new ToolRun(process.ExitCode, output.Result, error.Result);
    }
}
