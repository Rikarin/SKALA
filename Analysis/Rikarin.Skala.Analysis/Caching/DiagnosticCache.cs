using System.Collections.Immutable;
using System.Globalization;
using System.IO.Hashing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Analysis.Caching;

/// <summary>
/// The identity of one file's diagnostics: everything the answer depends on, hashed.
/// </summary>
/// <remarks>
/// docs/plan/07 § "The incremental cache". Invalidation is by key mismatch only — no timestamps, no
/// watchers, no partial states.
/// </remarks>
public static class CacheKey {
    public static string For(
        string filePath,
        ReadOnlySpan<byte> content,
        string compilationFingerprint,
        string ruleSetFingerprint,
        string editorConfigFingerprint
    ) {
        var hash = new XxHash128();
        hash.Append(Encoding.UTF8.GetBytes(filePath));
        hash.Append(content);
        hash.Append(Encoding.UTF8.GetBytes(compilationFingerprint));
        hash.Append(Encoding.UTF8.GetBytes(ruleSetFingerprint));
        hash.Append(Encoding.UTF8.GetBytes(editorConfigFingerprint));
        hash.Append(Encoding.UTF8.GetBytes(SkalaVersion.Value));

        // ⚠ The shape of a cached entry, in the key. `CachedFinding` gained the fingerprint's
        // enclosing-symbol and snippet terms in M6, and an entry written before that deserialises
        // happily with both empty — a stale hit that is wrong rather than absent, which is the
        // failure mode a cache must never have. Bumping this discards them instead.
        hash.Append(Encoding.UTF8.GetBytes("cache/v2"));
        return Convert.ToHexStringLower(hash.GetCurrentHash());
    }

    /// <summary>Reference MVIDs, parse options and preprocessor symbols — the compilation's identity.</summary>
    public static string CompilationFingerprint(CompilationUnit unit) {
        var builder = new StringBuilder();
        builder.Append(unit.TargetFramework).Append('|');
        foreach (var symbol in unit.PreprocessorSymbols.Sort(StringComparer.Ordinal)) {
            builder.Append(symbol).Append(',');
        }

        builder.Append('|');
        var parseOptions = unit.Compilation.SyntaxTrees.FirstOrDefault()?.Options;
        if (parseOptions is not null) {
            builder.Append(parseOptions.Language)
                .Append(':')
                .Append(parseOptions.DocumentationMode)
                .Append('|');
        }

        // ⚠ MVIDs, not paths: a rebuilt dependency at the same path is a different program, and a
        // cache that cannot see that is a cache that serves findings about the previous build.
        var mvids = new List<string>();
        foreach (var reference in unit.Compilation.References) {
            if (unit.Compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly) {
                mvids.Add(assembly.Identity.GetDisplayName());
            } else if (reference is PortableExecutableReference { FilePath: { } path }) {
                mvids.Add(path);
            }
        }

        mvids.Sort(StringComparer.Ordinal);
        foreach (var mvid in mvids) {
            builder.Append(mvid).Append(';');
        }

        return Convert.ToHexStringLower(XxHash128.Hash(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    /// <summary>Rule ids, their effective severities, and the analyzer assemblies' identities.</summary>
    public static string RuleSetFingerprint(
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer> analyzers
    ) {
        var builder = new StringBuilder();
        foreach (var analyzer in analyzers.OrderBy(
                static analyzer => analyzer.GetType().FullName,
                StringComparer.Ordinal
            )) {
            var type = analyzer.GetType();
            builder.Append(type.FullName).Append('@').Append(type.Assembly.ManifestModule.ModuleVersionId).Append(';');
            foreach (var descriptor in analyzer.SupportedDiagnostics) {
                builder.Append(descriptor.Id)
                    .Append('=')
                    .Append(descriptor.DefaultSeverity)
                    .Append(descriptor.IsEnabledByDefault ? '+' : '-')
                    .Append(',');
            }
        }

        return Convert.ToHexStringLower(XxHash128.Hash(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}

/// <summary>One file's cached findings.</summary>
public sealed record CacheEntry {
    public required string Key { get; init; }

    public required string Path { get; init; }

    public required ImmutableArray<CachedFinding> Findings { get; init; }
}

/// <summary>A finding as it survives a process boundary.</summary>
public sealed record CachedFinding(
    string RuleId,
    int Severity,
    string Message,
    int Line,
    int Column,
    int EndLine,
    int EndColumn,
    int Start,
    int Length,
    bool FixIsSafe,
    string[] FixStarts,
    string[] FixLengths,
    string[] FixTexts,
    string[] TargetFrameworks,
    int Suppression,

    // ⚠ The fingerprint's third and second terms, and they are not optional.
    // `Fingerprints.V2` hashes the enclosing symbol and the normalised snippet, so a rehydrated
    // finding that lost them hashes differently from the same finding computed cold — and a
    // baseline written by a cold run then matches nothing on a warm one. That is the exact failure
    // docs/plan/09 § "The fingerprint" exists to prevent, reintroduced by the cache rather than by
    // a line number. Measured before the fix: 686 accepted findings, 686 reported "fixed" and 686
    // reported "new", on a tree where nothing had changed.
    string EnclosingSymbol,
    string Snippet);

/// <summary>
/// The per-file diagnostic cache, and the one condition that makes it correct.
/// </summary>
/// <remarks>
/// The budget is "warm analysis of changed files in under 5 s" on a 4 691-file tree
/// (docs/plan/13), which requires not re-running analyzers over unchanged files.
/// <para>
/// ⚠ <b>The correctness condition is that a rule's output for a file depends only on the key's
/// inputs, and that is false for whole-compilation rules.</b> A "this public member is never used"
/// rule reads every file, so its answer for <c>A.cs</c> changes when <c>B.cs</c> changes and the key
/// for <c>A.cs</c> does not move. Rule metadata therefore carries a <see cref="RuleScope"/>, and
/// <see cref="RuleScope.Compilation"/> rules are excluded from per-file caching entirely: their
/// findings are never stored and never served, and they re-run whenever any file changes.
/// </para>
/// <para>
/// ⚠ Getting this wrong produces stale findings, which is the failure mode that destroys trust in a
/// cache permanently — and it does it silently, because a stale finding looks exactly like a real
/// one and a missing finding looks exactly like a clean file.
/// </para>
/// <para>
/// Cache corruption is never a failure: a bad read discards the cache and re-runs.
/// </para>
/// </remarks>
public sealed class DiagnosticCache {
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = false
    };

    readonly string _path;
    readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    bool _dirty;

    public DiagnosticCache(string repositoryRoot, string compilationName) {
        var directory = Path.Combine(repositoryRoot, ".skala", "cache");
        _path = Path.Combine(directory, Sanitise(compilationName) + ".diagnostics.json");
    }

    public int Hits { get; private set; }

    public int Misses { get; private set; }

    public int Held => _entries.Count;

    /// <summary>
    /// ⚠ The set of rule ids that may never be cached per file. Read from the catalogue, not
    /// hard-coded, so that adding a compilation-scoped rule cannot forget this.
    /// </summary>
    public static ImmutableHashSet<string> Uncacheable { get; } = BuildUncacheable();

    static ImmutableHashSet<string> BuildUncacheable() {
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (var rule in RuleCatalog.All) {
            if (!rule.IsCacheable) {
                builder.Add(rule.Id);
            }
        }

        return builder.ToImmutable();
    }

    public void Load() {
        try {
            if (!File.Exists(_path)) {
                return;
            }

            var entries = JsonSerializer.Deserialize<List<CacheEntry>>(File.ReadAllText(_path), Json);
            if (entries is null) {
                return;
            }

            foreach (var entry in entries) {
                _entries[entry.Key] = entry;
            }
        } catch (Exception exception) when (exception is IOException
            or JsonException
            or UnauthorizedAccessException
            or NotSupportedException) {
            // ⚠ Corruption is never a failure. Discard and re-run.
            _entries.Clear();
        }
    }

    public void Save() {
        if (!_dirty) {
            return;
        }

        try {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_entries.Values.ToList(), Json));
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            // A read-only tree does not fail a check.
        }
    }

    public bool TryGet(string key, out ImmutableArray<Finding> findings, string path) {
        if (_entries.TryGetValue(key, out var entry)) {
            Hits++;
            findings = [.. entry.Findings.Select(finding => Rehydrate(finding, path))];
            return true;
        }

        Misses++;
        findings = [];
        return false;
    }

    /// <summary>
    /// Stores one file's findings.
    /// </summary>
    /// <remarks>
    /// ⚠ Compilation-scoped rules are filtered out here rather than at read time, so that a cache
    /// written by a build with the rule enabled cannot serve it back to one without.
    /// </remarks>
    public void Put(string key, string path, ImmutableArray<Finding> findings) {
        _entries[key] = new CacheEntry {
            Key = key,
            Path = path,
            Findings = [
                .. findings
                    .Where(static finding => !Uncacheable.Contains(finding.RuleId))
                    .Select(Dehydrate)
            ]
        };

        _dirty = true;
    }

    public static void Clear(string repositoryRoot) {
        var directory = Path.Combine(repositoryRoot, ".skala", "cache");
        try {
            if (Directory.Exists(directory)) {
                Directory.Delete(directory, recursive: true);
            }
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    static CachedFinding Dehydrate(Finding finding) =>
        new(
            finding.RuleId,
            (int)finding.Severity,
            finding.Message,
            finding.Line,
            finding.Column,
            finding.EndLine,
            finding.EndColumn,
            finding.Start,
            finding.Length,
            finding.FixIsSafe,
            [.. finding.Fix.Select(static edit => edit.Start.ToString(CultureInfo.InvariantCulture))],
            [.. finding.Fix.Select(static edit => edit.Length.ToString(CultureInfo.InvariantCulture))],
            [.. finding.Fix.Select(static edit => edit.Text)],
            [.. finding.TargetFrameworks],
            (int)finding.Suppression,
            finding.EnclosingSymbol,
            finding.Snippet
        );

    static Finding Rehydrate(CachedFinding cached, string path) {
        var fix = ImmutableArray.CreateBuilder<FixEdit>(cached.FixStarts.Length);
        for (var i = 0; i < cached.FixStarts.Length; i++) {
            fix.Add(
                new FixEdit(
                    path,
                    int.Parse(cached.FixStarts[i], CultureInfo.InvariantCulture),
                    int.Parse(cached.FixLengths[i], CultureInfo.InvariantCulture),
                    cached.FixTexts[i]
                )
            );
        }

        return new Finding {
            RuleId = cached.RuleId,
            Severity = (Core.Diagnostics.SkalaSeverity)cached.Severity,
            Message = cached.Message,
            Path = path,
            Line = cached.Line,
            Column = cached.Column,
            EndLine = cached.EndLine,
            EndColumn = cached.EndColumn,
            Start = cached.Start,
            Length = cached.Length,
            Fix = fix.ToImmutable(),
            FixIsSafe = cached.FixIsSafe,
            TargetFrameworks = [.. cached.TargetFrameworks],
            Suppression = (SuppressionKind)cached.Suppression,
            EnclosingSymbol = cached.EnclosingSymbol,
            Snippet = cached.Snippet
        };
    }

    static string Sanitise(string name) {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name) {
            builder.Append(char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_');
        }

        return builder.ToString();
    }
}
