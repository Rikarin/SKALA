using System.Diagnostics;
using System.Reflection;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     Every committed path can be checked out on Windows.
/// </summary>
/// <remarks>
///     ⚠ This is not hygiene, it is a platform leg. A fixture named <c>nul.cs</c> sat in
///     <c>fixtures/SK1026/positive/</c> and <c>actions/checkout</c> died on it with
///     <c>invalid path … git.exe failed with exit code 128</c> — so <c>test (windows-latest)</c> never
///     ran a single test, while the workflow's own header claimed the suite runs on all three. The
///     checkout fails before any test does, which is why nothing in the suite could report it.
///     <para>
///         Renaming that one file closes the instance. This closes the class: the reserved DOS device
///         names (<c>con prn aux nul com1</c>–<c>com9</c> <c>lpt1</c>–<c>lpt9</c>, with or without an
///         extension), the characters Win32 forbids in a name, and a component ending in a dot or a
///         space — which Win32 silently strips, so two paths collide rather than one failing.
///     </para>
///     <para>
///         ⚠ It reads <c>git ls-files</c> rather than the working tree, because the question is what a
///         clean checkout has to write, not what happens to be on this disk. An empty listing is
///         treated as a broken instrument and fails: a zero from a check that did not run and a zero
///         from a clean tree are the same zero.
///     </para>
/// </remarks>
public sealed class RepositoryPathTests {
    /// <summary>The device names Win32 reserves in every directory, at any extension.</summary>
    static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase) {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>The characters Win32 forbids in a path component.</summary>
    const string ForbiddenCharacters = "<>:\"|?*\\";

    static string RepositoryRoot { get; } =
        Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(static attribute => attribute.Key == "SkalaRepositoryRoot")
        .Value!;

    [Fact]
    public void EveryCommittedPath_CanBeCheckedOutOnWindows() {
        var paths = CommittedPaths();
        Assert.True(
            paths.Count > 100,
            $"`git ls-files` in {RepositoryRoot} listed {paths.Count} paths. "
            + "That is the instrument failing, not the repository being clean."
        );

        var offences = new List<string>();
        foreach (var path in paths) {
            foreach (var component in path.Split('/')) {
                foreach (var reason in Reasons(component)) {
                    offences.Add($"{path}: {reason}");
                }
            }
        }

        Assert.True(
            offences.Count == 0,
            "These committed paths cannot be written by git on Windows, so `actions/checkout` fails "
            + "with exit code 128 and the Windows leg runs no tests at all:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offences.Order(StringComparer.Ordinal))
        );
    }

    static IEnumerable<string> Reasons(string component) {
        if (component.Length == 0) {
            yield break;
        }

        var stem = component.Split('.')[0];
        if (ReservedDeviceNames.Contains(stem)) {
            yield return $"`{component}` uses the reserved device name `{stem}`";
        }

        foreach (var c in ForbiddenCharacters) {
            if (component.Contains(c, StringComparison.Ordinal)) {
                yield return $"`{component}` contains `{c}`, which Win32 forbids in a name";
            }
        }

        if (component.Any(static c => char.IsControl(c))) {
            yield return $"`{component}` contains a control character";
        }

        var last = component[^1];
        if (last is '.' or ' ') {
            yield return $"`{component}` ends in a `{(last == ' ' ? "space" : ".")}`, which Win32 strips";
        }
    }

    /// <summary>Committed paths, from git rather than from the disk this test happens to run on.</summary>
    static List<string> CommittedPaths() {
        var start = new ProcessStartInfo("git") {
            WorkingDirectory = RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        // -z, because a path git considers unusual is quoted and escaped in the default listing —
        // which would hide exactly the characters this test is looking for.
        foreach (var argument in new[] { "ls-files", "-z" }) {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"`git ls-files` exited {process.ExitCode}.");

        return [..output.Split('\0', StringSplitOptions.RemoveEmptyEntries)];
    }
}
