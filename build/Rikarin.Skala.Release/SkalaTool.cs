using System.Diagnostics;
using System.Security.Cryptography;

namespace Rikarin.Skala.Release;

public sealed record ToolRun(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
///     One <c>skala</c> build, driven as a process.
/// </summary>
/// <remarks>
///     ⚠ <see cref="Fingerprint" /> is correctness rather than convenience: the SHA-256 of the binary
///     itself, printed in the release notes. It is what makes "the baseline and the candidate were
///     different builds" a checkable claim rather than an assumption — see <c>OutputSurface.Run</c>,
///     which refuses two tools with the same one.
///     <para>
///         ⚠ Every invocation also used to set <c>SKALA_NO_DAEMON=1</c>, because a per-repository format
///         daemon shared between the baseline and the candidate would have measured the daemon rather
///         than either tool. The daemon is gone and so is the variable; there is one path now, and it is
///         the one being measured.
///     </para>
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

        return new(full, full.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
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

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"'{Path}' did not start.");

        // Read both pipes before waiting: a tool that fills one of them while we wait deadlocks.
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new(process.ExitCode, output.Result, error.Result);
    }
}
