namespace Rikarin.Skala.Core;

/// <summary>
/// The one place <c>&lt;repo&gt;/.skala/</c> is created, and the reason it does not show up in
/// <c>git status</c>.
/// </summary>
/// <remarks>
/// ⚠ <b>This type exists because the tool was dirtying the repositories it ran on.</b> Six call
/// sites created <c>.skala/</c> — the daemon socket, the diagnostic cache, the clone index, the
/// SARIF report, the crash artefacts and the conformance summary — and not one of them left
/// anything behind to keep git quiet. A run of <c>skala check</c> on somebody else's tree added
/// <c>.skala/cache/</c> to their working copy, and an earlier measurement run left exactly that
/// inside the Vixen checkout. A tool that dirties the host repository's <c>git status</c> is a tool
/// people stop running, and the failure is worse than noisy: it is attributed to whatever the
/// person was actually doing.
/// <para>
/// The fix is that the directory ignores itself. Whenever it is created, a <c>.gitignore</c>
/// containing a single <c>*</c> goes in beside the contents. That pattern also matches the
/// <c>.gitignore</c> itself, so the whole directory is invisible to git without the host repository
/// having to know Skala exists, and without Skala editing a file it does not own. The host's own
/// <c>.gitignore</c> is never touched — writing into a file the user maintains is how a tool earns
/// a merge conflict.
/// </para>
/// <para>
/// ⚠ Nothing here throws. Every caller is on a path where failing to write a hygiene marker is
/// strictly less bad than failing the operation the user asked for — a read-only checkout still
/// gets its analysis, it just does not get the marker.
/// </para>
/// </remarks>
public static class SkalaDirectory {
    /// <summary>The directory's name under the repository root.</summary>
    public const string Name = ".skala";

    /// <summary>What the marker contains: everything here is Skala's, and none of it is the user's.</summary>
    public const string IgnoreContents = "*\n";

    /// <summary>
    /// <c>&lt;repositoryRoot&gt;/.skala/&lt;segments&gt;</c> — computed only, nothing is created.
    /// </summary>
    public static string PathFor(string repositoryRoot, params string[] segments) =>
        Combine(Path.Combine(repositoryRoot, Name), segments);

    static string Combine(string head, string[] segments) {
        var result = head;
        foreach (var segment in segments) {
            result = Path.Combine(result, segment);
        }

        return result;
    }

    /// <summary>
    /// Creates <c>&lt;repositoryRoot&gt;/.skala/&lt;segments&gt;</c> and guarantees the self-ignore marker.
    /// </summary>
    /// <returns>The full path of the created directory.</returns>
    public static string Ensure(string repositoryRoot, params string[] segments) {
        var skala = Path.Combine(repositoryRoot, Name);
        var target = Combine(skala, segments);
        Directory.CreateDirectory(target);
        Mark(skala);
        return target;
    }

    /// <summary>
    /// The same, for callers that already hold the <c>.skala</c> directory rather than the
    /// repository root — the crash-artefact path is handed one and never sees the root.
    /// </summary>
    public static string EnsureAt(string skalaDirectory, params string[] segments) {
        var target = Combine(skalaDirectory, segments);
        Directory.CreateDirectory(target);
        Mark(skalaDirectory);
        return target;
    }

    /// <summary>
    /// Creates the directory <paramref name="filePath"/> will be written into, and marks the
    /// nearest <c>.skala</c> above it. This is the shape every writer already had — the cache, the
    /// clone index, the SARIF report and the daemon socket all hold a full file path and created
    /// its parent — so adopting it is a one-line change per call site rather than a refactor.
    /// </summary>
    /// <remarks>
    /// ⚠ When no <c>.skala</c> component is present the parent directory is still created and no
    /// marker is written. That is the <c>--output</c> case: a report the user redirected somewhere
    /// of their own choosing is theirs, and silently dropping a <c>.gitignore</c> beside it would
    /// be the same discourtesy this type exists to stop.
    /// </remarks>
    public static void EnsureForFile(string filePath) {
        var parent = Path.GetDirectoryName(filePath);
        if (parent is null) {
            return;
        }

        Directory.CreateDirectory(parent);

        for (var directory = parent; directory is not null; directory = Path.GetDirectoryName(directory)) {
            if (string.Equals(Path.GetFileName(directory), Name, StringComparison.Ordinal)) {
                Mark(directory);
                return;
            }
        }
    }

    /// <summary>
    /// Writes the marker if it is absent. ⚠ Idempotent and cheap: this sits under the daemon's
    /// socket bind and under every cache write, so it is one <c>File.Exists</c> on the warm path.
    /// A marker the user has edited is left exactly as they left it.
    /// </summary>
    public static void Mark(string skalaDirectory) {
        try {
            var marker = Path.Combine(skalaDirectory, ".gitignore");
            if (!File.Exists(marker)) {
                File.WriteAllText(marker, IgnoreContents);
            }
        } catch (IOException) {
            // A read-only tree, or a race with another process writing the same marker. Neither is
            // worth failing the caller's actual work over.
        } catch (UnauthorizedAccessException) { }
    }
}
