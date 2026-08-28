using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Rikarin.Skala.Server;

/// <summary>
///     Which build of Skala a daemon is serving, and whether that build is still the one on disk.
/// </summary>
/// <remarks>
///     ⚠ <b>The defect this exists for.</b> <see cref="Protocol.DaemonProtocol.Version" /> is a
///     <em>wire</em> version — "bumped whenever <c>DaemonRequest</c> or <c>DaemonResponse</c> changes" —
///     and it was the only compatibility check a daemon made. Rebuild the formatter, leave the daemon
///     up, and every <c>skala format</c> keeps producing the bytes the old build produced, for ever:
///     the idle timer is thirty minutes but every request refreshes it, so an actively used stale
///     daemon never dies. It cost two agents about forty minutes each on one day — one "fixed" a
///     defect, measured it still reproducing and reported the fix incomplete (the fix was fine, the
///     daemon was old), the other concluded a correct implementation was dead code. Both recovered by
///     trying <c>--no-daemon</c> on a hunch.
///     <para>
///         <c>DaemonProtocol</c>'s own remarks make the argument: an older protocol "is a source of
///         formatting differences between two developers on one repository — which is the failure the
///         whole tool exists to prevent". A stale <em>build</em> is the same failure with more surface,
///         and there was no check for it at all.
///     </para>
///     <para>
///         ⚠ <b>Two fingerprints, because one of them is wrong on its own.</b> Measured on the reference
///         machine over the twelve Skala assemblies of a Debug layout (2.6 MB):
///         <list type="bullet">
///             <item>enumerating the directory — <b>0.046 ms</b></item>
///             <item>length and last-write time of each file — <b>0.025 ms</b></item>
///             <item>the module MVID of each file — <b>0.35 ms</b></item>
///             <item>SHA-256 of each file — <b>1.67 ms</b></item>
///         </list>
///         The stamp (length and mtime) is the cheap one and it is <i>not reliable</i>: a copy bumps the
///         mtime of a byte-identical assembly, so on its own it throws the warm cache away after builds
///         that changed nothing. The MVID is the reliable one — the C# compiler is deterministic by
///         default, so a rebuild of unchanged sources produces the same MVID and a rebuild of changed
///         sources cannot produce the same one — and at 0.35 ms it is fourteen times the stamp. A whole-
///         file hash is both slower than the MVID and no more reliable, so it is refuted outright.
///     </para>
///     <para>
///         So the stamp is the gate and the MVID is the verdict: every request pays <b>0.072 ms</b>
///         (enumerate + stat), and only a request that arrives after files have actually been rewritten
///         pays the further 0.35 ms to find out whether the rewrite changed anything.
///     </para>
///     <para>
///         ⚠ <b>Measured on the path that matters, not only in isolation.</b> A warm format round trip
///         over the socket — connect, send, cache hit, answer — is <b>0.12 ms</b> without the check and
///         <b>0.22 ms</b> with it, medians of 400. That 0.10 ms is <b>0.25 %</b> of docs/plan/13's 40 ms
///         warm budget and 1.2 % of the 8.65 ms the whole warm operation measures end to end. ⚠ And the
///         thin client pays <em>nothing</em>: the check is entirely daemon-side, no request or response
///         field moved, so <c>Tools/Rikarin.Skala.Client</c> has not a byte of new work on it.
///     </para>
///     <para>
///         ⚠ <b>It never fires without a baseline.</b> If the directory holds nothing recognisable, or a
///         file cannot be read as a PE image — which is what a rebuild <i>in progress</i> looks like —
///         the answer is "cannot tell", which is "not stale". Being wrong in that direction costs a
///         stale answer for one more request; being wrong in the other direction kills a healthy daemon
///         in the middle of somebody's build.
///     </para>
/// </remarks>
public sealed class BuildIdentity {
    /// <summary>⚠ The value both fingerprints take when nothing could be read.</summary>
    public const string Unknown = "unknown";

    readonly Lock _gate = new();
    readonly string _directory;

    /// <summary>The cheap fingerprint as of the last check that agreed with <see cref="Loaded" />.</summary>
    string _stamp;

    public BuildIdentity(string directory) {
        _directory = directory;
        Loaded = Content(directory) ?? Unknown;
        _stamp = Stamp(directory);
    }

    /// <summary>
    ///     The daemon host's own directory. ⚠ Read from disk at construction rather than from the
    ///     loaded assemblies, because the assemblies are loaded lazily — <c>Formatting.CSharp</c> is not
    ///     in the process until the first format — and a baseline that grows as the daemon warms up is a
    ///     baseline that cannot detect anything loaded after the rebuild.
    /// </summary>
    public static BuildIdentity Current { get; } = new(AppContext.BaseDirectory);

    /// <summary>What the daemon is serving: the MVID fingerprint taken when it started.</summary>
    public string Loaded { get; }

    /// <summary>Whether a baseline was readable at all. When false, <see cref="HasChanged" /> never fires.</summary>
    public bool Known => !string.Equals(Loaded, Unknown, StringComparison.Ordinal);

    /// <summary>
    ///     The MVID fingerprint of what is on disk <em>now</em>, without touching any cached state.
    /// </summary>
    /// <remarks>
    ///     ⚠ For <c>daemon status</c>, which is a pure observer: reporting a stale daemon must not be
    ///     the thing that kills it, or the one command a person runs to see the problem is the one
    ///     command that hides it.
    /// </remarks>
    public string OnDisk() => Content(_directory) ?? Unknown;

    /// <summary>
    ///     Whether the build on disk is no longer the build this daemon is serving.
    /// </summary>
    /// <remarks>
    ///     ⚠ Called from every connection's handler, so it takes the lock; the stamp it caches is
    ///     shared state and two concurrent formats would otherwise race to refresh it.
    /// </remarks>
    public bool HasChanged() {
        if (!Known) {
            return false;
        }

        lock (_gate) {
            var stamp = Stamp(_directory);
            if (string.Equals(stamp, _stamp, StringComparison.Ordinal)) {
                return false;
            }

            var content = Content(_directory);
            if (content is null) {
                // ⚠ A file that will not open as a PE image is a build in flight. Do not fire, and do
                // not accept the new stamp either, so the next request looks again.
                return false;
            }

            if (string.Equals(content, Loaded, StringComparison.Ordinal)) {
                // Touched, not changed — a no-op rebuild, or a copy of identical bytes. This is the
                // case the stamp alone gets wrong, and accepting the new stamp is what keeps the
                // 0.35 ms MVID pass off every subsequent request.
                _stamp = stamp;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    ///     The assemblies whose content defines an answer: everything Skala ships into the host's
    ///     directory, plus the host itself.
    /// </summary>
    /// <remarks>
    ///     ⚠ The files rather than the loaded assemblies, and deliberately: <c>Formatting.CSharp.dll</c>
    ///     is the one that moved in the reproduction and <c>skala-tool.dll</c> was untouched, so any
    ///     identity that watches only the entry point misses the defect entirely. Roslyn and the other
    ///     third-party assemblies are not included — they cannot change without a Skala rebuild that
    ///     moves at least one of these, because the build copies the whole closure together.
    /// </remarks>
    static List<string> Assemblies(string directory) {
        var files = new SortedSet<string>(StringComparer.Ordinal);
        try {
            foreach (var file in Directory.EnumerateFiles(directory, "Rikarin.Skala.*.dll")) {
                files.Add(file);
            }

            // The host is `skala-tool.dll`, which the pattern above does not match. Named at runtime
            // rather than spelled out, so a development build (where it is still `skala`) works too.
            var entry = Assembly.GetEntryAssembly()?.Location;
            if (entry is { Length: > 0 }
                && string.Equals(
                    Path.GetDirectoryName(entry),
                    directory.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.Ordinal
                )
                && File.Exists(entry)) {
                files.Add(entry);
            }
        } catch (IOException) {
            return [];
        } catch (UnauthorizedAccessException) {
            return [];
        }

        return [.. files];
    }

    static string Stamp(string directory) {
        var hash = Fnv.Seed;
        foreach (var file in Assemblies(directory)) {
            try {
                var info = new FileInfo(file);
                hash = Fnv.Mix(hash, Path.GetFileName(file));
                hash = Fnv.Mix(hash, (ulong)info.Length);
                hash = Fnv.Mix(hash, (ulong)info.LastWriteTimeUtc.Ticks);
            } catch (IOException) {
                return Unknown;
            } catch (UnauthorizedAccessException) {
                return Unknown;
            }
        }

        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }

    /// <summary>The MVID fingerprint, or null when any of the files could not be read as an image.</summary>
    static string? Content(string directory) {
        var files = Assemblies(directory);
        if (files.Count == 0) {
            return null;
        }

        var hash = Fnv.Seed;
        foreach (var file in files) {
            Guid mvid;
            try {
                using var stream = File.OpenRead(file);
                using var reader = new PEReader(stream);
                var metadata = reader.GetMetadataReader();
                mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
            } catch (IOException) {
                return null;
            } catch (UnauthorizedAccessException) {
                return null;
            } catch (BadImageFormatException) {
                return null;
            } catch (InvalidOperationException) {
                // PEReader on a file with no metadata at all.
                return null;
            }

            hash = Fnv.Mix(hash, Path.GetFileName(file));
            foreach (var b in mvid.ToByteArray()) {
                hash = Fnv.Mix(hash, b);
            }
        }

        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }

    /// <summary>FNV-1a, matching <c>DaemonProtocol.Hash</c>. Not a security boundary — a short name.</summary>
    static class Fnv {
        public const ulong Seed = 14695981039346656037UL;

        public static ulong Mix(ulong hash, ulong value) => (hash ^ value) * 1099511628211UL;

        public static ulong Mix(ulong hash, string value) {
            foreach (var c in value) {
                hash = Mix(hash, c);
            }

            return hash;
        }
    }
}
