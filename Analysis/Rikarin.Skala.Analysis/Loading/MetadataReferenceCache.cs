using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Rikarin.Skala.Analysis.Loading;

/// <summary>
/// Process-wide cache of <see cref="MetadataReference"/> by <c>(path, mtime, size)</c>.
/// </summary>
/// <remarks>
/// ⚠ docs/plan/07 § binlog: "a large solution references the same 300 assemblies from every project
/// and re-reading them is the single biggest avoidable cost." Every compilation in a solution
/// references much the same framework set; without this the loader reads the same hundreds of
/// megabytes of metadata once per project.
/// <para>
/// ⚠ The key carries mtime and size rather than the path alone. A process that stays alive across a
/// rebuild — the daemon — would otherwise hold the previous build's metadata and answer questions
/// about a program that no longer exists.
/// </para>
/// </remarks>
public static class MetadataReferenceCache {
    static readonly ConcurrentDictionary<string, MetadataReference> Cache = new(StringComparer.Ordinal);

    static long _hits;
    static long _misses;

    public static long Hits => Interlocked.Read(ref _hits);

    public static long Misses => Interlocked.Read(ref _misses);

    public static MetadataReference? Get(string path, IReadOnlyList<string>? aliases = null) {
        FileInfo info;
        try {
            info = new FileInfo(path);
            if (!info.Exists) {
                return null;
            }
        } catch (IOException) {
            return null;
        } catch (UnauthorizedAccessException) {
            return null;
        }

        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"{path}|{info.LastWriteTimeUtc.Ticks}|{info.Length}|{(aliases is { Count: > 0 } ? string.Join(",", aliases) : string.Empty)}"
        );

        if (Cache.TryGetValue(key, out var cached)) {
            Interlocked.Increment(ref _hits);
            return cached;
        }

        Interlocked.Increment(ref _misses);
        try {
            var reference = MetadataReference.CreateFromFile(path);
            var result = aliases is { Count: > 0 }
                ? reference.WithAliases(aliases)
                : (MetadataReference)reference;
            Cache[key] = result;
            return result;
        } catch (IOException) {
            return null;
        } catch (BadImageFormatException) {
            // A native DLL on a `/reference:` line is a build oddity, not a reason to fail the run.
            return null;
        }
    }

    public static void Clear() {
        Cache.Clear();
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
    }
}
