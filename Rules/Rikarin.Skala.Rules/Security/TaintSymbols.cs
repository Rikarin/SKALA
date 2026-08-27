using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Security;

/// <summary>
/// <c>taint.json</c>'s type and member names, resolved against one compilation.
/// </summary>
/// <remarks>
/// ⚠ Names are matched against a member's containing type <em>and its base types and its
/// interfaces</em>, which is the difference between a table that works and a table that has to
/// name every ADO.NET provider ever written. <c>NpgsqlCommand.CommandText</c> overrides
/// <c>DbCommand.CommandText</c> and implements <c>IDbCommand.CommandText</c>, so the two entries in
/// the table cover Npgsql, SQLite, MySQL, SQL Server and anything else that ever ships.
/// <para>
/// ⚠ It is also why matching is by <em>name</em> rather than by
/// <see cref="SymbolEqualityComparer"/> against a resolved <see cref="INamedTypeSymbol"/>. Half of
/// the declared types — <c>Dapper.SqlMapper</c>, the EF Core extension classes — are not in the
/// framework, so a compilation that does not reference them resolves nothing, and a compilation
/// that does must still match a symbol Skala's own reference set has never seen.
/// </para>
/// <para>
/// ⚠ <b>Unknown is untrusted-free.</b> Every question this type cannot answer resolves to "not a
/// source, not a sink" rather than to a guess. docs/plan/08: "Where Skala cannot prove a flow, it
/// says nothing rather than guessing" — and for a range whose default severity is <c>error</c>,
/// that asymmetry is the whole design.
/// </para>
/// </remarks>
public sealed class TaintSymbols {
    readonly Dictionary<string, HashSet<string>> _sources;
    readonly Dictionary<string, HashSet<string>> _propagators;
    readonly Dictionary<string, HashSet<string>> _sanitizers;
    readonly List<TaintSink> _sinks;

    TaintSymbols(
        Dictionary<string, HashSet<string>> sources,
        Dictionary<string, HashSet<string>> propagators,
        Dictionary<string, HashSet<string>> sanitizers,
        List<TaintSink> sinks
    ) {
        _sources = sources;
        _propagators = propagators;
        _sanitizers = sanitizers;
        _sinks = sinks;
    }

    /// <summary>Whether any sink for a rule could ever match in this compilation.</summary>
    public bool HasSinks => _sinks.Count > 0;

    /// <summary>
    /// The table restricted to one rule's sinks, or <c>null</c> when the compilation cannot produce
    /// a finding for it at all.
    /// </summary>
    /// <remarks>
    /// ⚠ This is the gate that keeps the taint engine off the warm path, and it runs once per
    /// compilation rather than once per method. Two conditions have to hold before a single
    /// control-flow graph is built anywhere: at least one declared <em>source</em> type has to
    /// resolve, because a compilation with no trust boundary cannot produce a tainted value; and at
    /// least one of the rule's <em>sink</em> types has to resolve. Vixen references neither ASP.NET
    /// Core nor any SQL client, so <c>SK5001</c> registers no actions at all there and costs the
    /// price of two <c>GetTypeByMetadataName</c> calls for the whole 4 717-file tree.
    /// </remarks>
    public static TaintSymbols? For(Compilation compilation, string ruleId) {
        var anySource = false;
        foreach (var type in TaintTable.SourceTypes) {
            if (compilation.GetTypeByMetadataName(type) is not null) {
                anySource = true;
                break;
            }
        }

        if (!anySource) {
            return null;
        }

        var sinks = new List<TaintSink>();
        foreach (var sink in TaintTable.Sinks) {
            if (sink.Rule == ruleId && compilation.GetTypeByMetadataName(sink.Type) is not null) {
                sinks.Add(sink);
            }
        }

        if (sinks.Count == 0) {
            return null;
        }

        return new TaintSymbols(
            Index(TaintTable.Sources),
            Index(TaintTable.Propagators),
            Index(TaintTable.Sanitizers),
            sinks
        );
    }

    static Dictionary<string, HashSet<string>> Index(IReadOnlyList<TaintMembers> entries) {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var entry in entries) {
            if (!result.TryGetValue(entry.Type, out var members)) {
                result[entry.Type] = members = new HashSet<string>(StringComparer.Ordinal);
            }

            foreach (var member in entry.Members) {
                members.Add(member);
            }
        }

        return result;
    }

    /// <summary>Whether reading this member reads a value that crossed a trust boundary.</summary>
    public bool IsSource(ISymbol? symbol) => Matches(symbol, _sources);

    /// <summary>Whether this call carries its arguments' taint into its result.</summary>
    public bool IsPropagator(ISymbol? symbol) => Matches(symbol, _propagators);

    /// <summary>Whether this call produces a value that can no longer carry an injection.</summary>
    public bool IsSanitizer(ISymbol? symbol) => Matches(symbol, _sanitizers);

    /// <summary>The sink this member is, or <c>null</c>.</summary>
    public TaintSink? Sink(ISymbol? symbol) {
        if (symbol is null) {
            return null;
        }

        var name = symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } ? ".ctor" : symbol.Name;
        foreach (var sink in _sinks) {
            if ((sink.MatchesAnyMember || string.Equals(sink.Member, name, StringComparison.Ordinal))
                && DeclaresType(symbol, sink.Type)) {
                return sink;
            }
        }

        return null;
    }

    static bool Matches(ISymbol? symbol, Dictionary<string, HashSet<string>> table) {
        if (symbol is null) {
            return false;
        }

        foreach (var type in Hierarchy(symbol.ContainingType)) {
            if (table.TryGetValue(type, out var members) && members.Contains(symbol.Name)) {
                return true;
            }
        }

        return false;
    }

    static bool DeclaresType(ISymbol symbol, string type) {
        foreach (var candidate in Hierarchy(symbol.ContainingType)) {
            if (string.Equals(candidate, type, StringComparison.Ordinal)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A type's own metadata name, then every base type's, then every interface's.
    /// </summary>
    /// <remarks>
    /// ⚠ Constructed generics are erased to their definition — <c>SqlMapper</c>'s methods are
    /// generic and the table names the containing type, not an instantiation of it.
    /// </remarks>
    static IEnumerable<string> Hierarchy(INamedTypeSymbol? type) {
        if (type is null) {
            yield break;
        }

        for (var current = type.OriginalDefinition; current is not null; current =
             current.BaseType?.OriginalDefinition) {
            yield return Name(current);
        }

        // ⚠ `AllInterfaces` rather than `Interfaces`, so that an interface a base type implements —
        // or one an implemented interface itself extends — still matches. `IDbCommand` reaches
        // `SqliteCommand` through `DbCommand`, which is two hops.
        foreach (var contract in type.AllInterfaces) {
            yield return Name(contract.OriginalDefinition);
        }
    }

    static string Name(INamedTypeSymbol type) =>
        type.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
                .WithGenericsOptions(SymbolDisplayGenericsOptions.None)
        );
}
