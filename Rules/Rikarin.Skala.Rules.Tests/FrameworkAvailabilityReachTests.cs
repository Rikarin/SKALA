using Rikarin.Skala.Rules.Metadata;
using System.Text.RegularExpressions;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     Every fix-carrying rule that asks a compilation whether an API exists either asks all of the
///     project's target frameworks, or says here why it does not have to (#351).
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         This is the half of #351 that stops the bug coming back, and without it the audit is a
///         sentence.
///     </b> #343 fixed <c>SK1023</c> and built <c>FrameworkAvailability</c>; a full sweep a
///     release later found <c>SK1023</c> was still the only rule consulting it, because nothing
///     forced or even detected the unguarded shape. The next framework-dependent rule would have
///     landed with the same hole and the same silence.
///     <para>
///         ⚠ <b>The bug.</b> A multi-targeted project is opened as one <c>Compilation</c> per moniker
///         over one set of source files and the findings are unioned, so
///         <c>Compilation.GetTypeByMetadataName</c> answers <b>"some moniker has this"</b> where a
///         rewrite needs <b>"every moniker has it"</b>. The fix is then written to a file all of them
///         compile and the older ones stop building — <c>netstandard2.1</c>, the half that goes into
///         Unity/IL2CPP, in the report that opened #343.
///     </para>
///     <para>
///         ⚠ <b>Scoped to rules with a fix, deliberately.</b> A lookup in a report-only rule can make
///         a finding wrong on a multi-targeted tree but cannot make a file stop compiling, and
///         holding 68 of those to a ledger would bury the seven entries that can. The exemptions
///         below are the fix-carrying remainder, and each says why its lookup is safe.
///     </para>
///     <para>
///         ⚠ <b>The distinction every entry turns on.</b> A <em>recognition</em> lookup asks about a
///         type the analysed source already references — "is this expression a <c>Task</c>", "does
///         this type implement <c>IDisposable</c>". It cannot differ usefully across monikers: if the
///         source names the type, every moniker resolves it or the project does not build at all. An
///         <em>availability</em> lookup asks whether something the fix is about to <b>write</b>
///         exists. That one is per-moniker and belongs behind
///         <see cref="FrameworkAvailability" />. ⚠ The trap is a lookup that looks like the first and
///         is the second: <c>SK2182</c> resolves a <b>string literal</b> from the source, so the
///         literal compiles everywhere while the type it names may exist on one moniker only.
///     </para>
///     <para>
///         ⚠ <b>The language-version axis is not policed here, because it is not a convention.</b>
///         Roughly forty <c>SK1xxx</c> rules gate on <c>SkalaRule.MeetsLanguageVersion</c> and had the
///         identical defect — the SDK's default <c>LangVersion</c> is per target framework, measured
///         at <b>7.3</b> for <c>netstandard2.0</c> against <b>14.0</b> for <c>net10.0</c> in one
///         project. That floor is declarative (<c>rules.json</c>'s <c>languageVersion</c>), so
///         <c>MultiTargetLanguageFloor</c> settles it centrally for the whole catalogue and a new rule
///         is guarded the day it lands. Only the predicates a rule alone can state need a ledger.
///     </para>
/// </remarks>
public sealed class FrameworkAvailabilityReachTests {
    const string Recognition =
        "Recognition: the type is matched against something the analysed source already references, "
        + "so a moniker that cannot resolve it is a moniker where the source does not compile either.";

    /// <summary>
    ///     Every fix-carrying analyzer that looks a type up without consulting the siblings, and why
    ///     that is sound.
    /// </summary>
    static readonly Dictionary<string, string> Exempt = new(StringComparer.Ordinal) {
        ["Async/AsyncIteratorNotEnumeratedAnalyzer.cs"] =
            Recognition + " `IAsyncEnumerable<T>` is only compared against the invocation's own type.",
        ["Async/AsyncOnlyToAwaitAnalyzer.cs"] = Recognition + " Task-family table; the fix removes `async`/`await`.",
        ["Async/AsyncVoidAnalyzer.cs"] = Recognition
            + " `EventArgs` identifies a handler shape; the fix writes `Task`, which any source spelling `async` already has.",
        ["Async/BlockingOnAsyncAnalyzer.cs"] = Recognition
            + " Awaiter table matched against the blocked-on expression; the fix only prepends `await`.",
        ["Async/CancellationTokenForwardingAnalyzer.cs"] =
            Recognition + " The fix forwards a parameter already in scope.",
        ["Async/NullTaskReturnAnalyzer.cs"] = Recognition + " Task/Task<T> match the declared return type.",
        ["Async/RedundantDisposeAnalyzer.cs"] = Recognition + " The fix deletes a statement and writes no type.",
        ["Async/SpinLockInReadonlyFieldAnalyzer.cs"] =
            Recognition + " `SpinLock` is the field's own declared type; the fix removes `readonly`.",
        ["Async/SynchronousAsyncDisposalAnalyzer.cs"] =
            Recognition + " `IAsyncDisposable` types the disposed value in source.",
        ["Async/TaskReturnedFromUsingAnalyzer.cs"] = Recognition + " Task-family table over the returned expression.",
        ["Async/UncancellableAsyncMethodAnalyzer.cs"] = Recognition
            + " `CancellationToken` is netstandard2.0-and-up on every supported moniker regardless.",
        ["Async/UndeclaredDisposeAnalyzer.cs"] =
            "The emitted text is only ever `IDisposable`/`System.IDisposable`, present on every "
            + "moniker; the `IAsyncDisposable` lookup is a negative guard over source types.",
        ["Async/UndisposedLocalAnalyzer.cs"] = Recognition
            + " The C# 8 floor its `using` declaration needs is declarative and centrally guarded.",
        ["Async/UndisposedOwnedFieldAnalyzer.cs"] = Recognition + " `IDisposable` types a field declared in source.",
        ["Async/UsingResourceInitializerAnalyzer.cs"] =
            "The source already has the `using`; the fix only hoists object-initializer assignments "
            + "and emits no new construct.",
        ["Cleanup/RedundantControlFlowAnalyzer.cs"] =
            Recognition + " `FlagsAttribute` on a source enum; every edit is a deletion.",
        ["Correctness/AssemblyLoadedOutsideItsContextAnalyzer.cs"] =
            "`AssemblyLoadContext` only proves the call sits inside an override of its own `Load`, so "
            + "the source subclasses it; the fix emits `LoadFromAssemblyPath`, an inherited member of "
            + "that same type.",
        ["Correctness/CollectionModifiedAnalyzer.cs"] = Recognition
            + " Collection table over the enumerated expression; the fix inserts `.ToList()`.",
        ["Correctness/DeadConditionalCallAnalyzer.cs"] =
            Recognition + " `ConditionalAttribute`; the fix deletes the call.",
        ["Correctness/DiscardedCaughtExceptionAnalyzer.cs"] =
            Recognition + " The fix passes an identifier already in source.",
        ["Correctness/DiscardedExceptionAnalyzer.cs"] =
            Recognition + " The fix inserts the keyword `throw `, which is not an API.",
        ["Correctness/GetTypeOnATypeAnalyzer.cs"] =
            Recognition + " The fix deletes `.GetType()` and keeps the receiver's own text.",
        ["Correctness/ImplicitStringSearchCultureAnalyzer.cs"] =
            "The emitted `IndexOf`/`StartsWith`/`EndsWith`/`LastIndexOf(string, StringComparison)` "
            + "overloads exist back to netstandard1.0; the lookups only choose between the short and "
            + "qualified spelling.",
        ["Correctness/InvariantCultureComparisonAnalyzer.cs"] = Recognition
            + " The fix swaps one member of the `StringComparison` the source already names.",
        ["Correctness/RedundantSuppressFinalizeAnalyzer.cs"] = Recognition + " `System.GC`; the fix deletes the call.",
        ["Correctness/WrongArgumentNameAnalyzer.cs"] =
            Recognition + " The fix emits `nameof(x)` over a parameter already in scope.",
        ["Design/NullSequenceReturnAnalyzer.cs"] =
            "The lookups type the declared return; the fix emits `[]`, whose C# 12 floor is "
            + "declarative and centrally guarded.",
        ["Maintainability/LoggerForAnotherTypeAnalyzer.cs"] =
            Recognition + " The fix rewrites only the type argument, a source type.",
        ["Modernization/CachedEmptyInstanceAnalyzer.cs"] =
            "The table is matched against the expression being replaced, and each entry's replacement "
            + "is a member of the very type the lookup found.",
        ["Modernization/CollectionExpressionAnalyzer.cs"] = Recognition
            + " `List<T>` types the source expression; the C# 12 floor is centrally guarded.",
        ["Modernization/DictionaryLookupAnalyzer.cs"] = Recognition
            + " `Dictionary<K,V>` is the receiver in source; the fix reuses `TryGetValue`, present wherever the type is.",
        ["Modernization/EnumGetValuesAnalyzer.cs"] = Recognition + " `System.Enum`; the fix names a source enum.",
        ["Modernization/ForeachOverIndexedForAnalyzer.cs"] = Recognition
            + " Collection table over the indexed receiver; the fix rewrites the loop header only.",
        ["Modernization/IndexerOverElementAtAnalyzer.cs"] =
            Recognition + " `Enumerable` — the source already calls LINQ; the fix writes an indexer.",
        ["Modernization/InterpolatedStringFormAnalyzer.cs"] =
            Recognition + " `LoggerExtensions` is the logging package the source calls into.",
        ["Modernization/OfTypeChainAnalyzer.cs"] =
            Recognition + " `Enumerable`; the fix merges two calls the source already makes.",
        ["Modernization/SpanDecodingAnalyzer.cs"] =
            "`IsByteSpan` compares a type already in the source against Span/ReadOnlySpan — the "
            + "argument is a span before the rule fires, so no moniker lacking one can reach it.",
        ["Modernization/Utf8LiteralAnalyzer.cs"] =
            "The `ReadOnlySpan<byte>` lookup types the *called method's parameter*, so the consumer "
            + "already takes one; the `u8` literal's C# 11 floor is declarative and centrally guarded.",
        ["Performance/CollectionOwnMethodAnalyzer.cs"] =
            "The LINQ-to-member map is a fixed three-entry table restricted by symbol to "
            + "`List<T>`/`ImmutableList<T>`, and every target member exists wherever those do.",
        ["Performance/ConcurrentDictionaryMemberAnalyzer.cs"] = Recognition
            + " `ConcurrentDictionary<K,V>` is the receiver; the emitted members are netstandard2.0-era.",
        ["Performance/CopyingPropertyAnalyzer.cs"] = Recognition + " `Enumerable`; the source already calls LINQ.",
        ["Performance/SortBeforeFilterAnalyzer.cs"] = Recognition + " `Enumerable`; the fix swaps two existing spans.",
        ["Performance/WhereBeforeOperatorAnalyzer.cs"] =
            Recognition + " `Enumerable`; the fix reorders existing calls.",
        ["Security/AsymmetricKeySizeAnalyzer.cs"] =
            Recognition + " The RSA/DSA receiver is in source; the fix emits the integer literal 2048."
    };

    static readonly Regex Lookup =
        new(@"GetTypesByMetadataName\s*\(|GetTypeByMetadataName\s*\(", RegexOptions.Compiled);

    const string Prefix = "Rules/Rikarin.Skala.Rules/";

    [Fact]
    public void EveryFixCarryingAvailabilityLookup_IsGuardedOrRecorded() {
        var files = TrackedRuleSources.All();

        // Anti-vacuity: an empty or truncated listing passes every assertion below.
        Assert.True(files.Count > 100, $"Only {files.Count} tracked C# file(s) were listed.");

        var unguarded = new SortedSet<string>(StringComparer.Ordinal);
        var routed = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var relative in files) {
            var normalised = relative.Replace('\\', '/');
            if (!normalised.StartsWith(Prefix, StringComparison.Ordinal)) {
                continue;
            }

            var text = File.ReadAllText(Path.Combine(TrackedRuleSources.RepositoryRoot, relative));
            var code = WithoutComments(text);
            if (!Lookup.IsMatch(code) || !CarriesAFix(text)) {
                continue;
            }

            var name = normalised.Substring(Prefix.Length);
            if (code.Contains("FrameworkAvailability", StringComparison.Ordinal)) {
                routed.Add(name);
            } else {
                unguarded.Add(name);
            }
        }

        // Anti-vacuity: the rules that DO consult the mechanism are known, so finding none of either
        // kind means the scan broke rather than that the tree is clean.
        Assert.True(routed.Count > 0, "No fix-carrying rule consults FrameworkAvailability, which cannot be right.");
        Assert.True(unguarded.Count > 0, "No fix-carrying rule looks a type up unguarded, which cannot be right.");

        var added = unguarded.Where(static file => !Exempt.ContainsKey(file)).ToList();
        Assert.True(
            added.Count == 0,
            "⚠ These rules have a fix, ask a compilation whether a type exists, and never ask the "
            + "project's other target frameworks:\n  "
            + string.Join("\n  ", added)
            + "\n\nA multi-targeted project is one compilation per moniker over one set of source "
            + "files, and the findings are unioned — so the lookup answers \"some moniker has this\" "
            + "while the fix is written to a file every moniker compiles. That is #343: SK1023 "
            + "rewrote to System.Threading.Lock and the netstandard2.1 leg stopped building after "
            + "`skala fix --safe`.\n\nIf the lookup decides whether something your FIX WRITES exists, "
            + "extract the whole condition into a `static bool Supports(Compilation)` and gate on "
            + "`FrameworkAvailability.PathsWithout(start.Options, Supports)` — the WHOLE condition, "
            + "not just \"the name resolves\". If the lookup merely RECOGNISES a type the analysed "
            + "source already references, it cannot differ across monikers that compile that source: "
            + "add the file here saying so."
        );

        var stale = Exempt.Keys.Where(file => !unguarded.Contains(file)).ToList();
        Assert.True(
            stale.Count == 0,
            "These files are recorded as exempt and no longer qualify:\n  "
            + string.Join("\n  ", stale)
            + "\n\nEither the lookup went away, the rule lost its fix, or it now consults "
            + "FrameworkAvailability. Drop the entry — an exemption list carrying names nobody "
            + "checked reads as audited and is not."
        );
    }

    /// <summary>Whether any rule this file declares ships a fix.</summary>
    /// <remarks>
    ///     ⚠ Via <c>RuleIds.X</c> rather than a hard-coded map: the constant is generated from
    ///     <c>rules.json</c>, so a rule that gains a fix tomorrow pulls its analyzer into this ledger
    ///     without anyone remembering to.
    /// </remarks>
    static bool CarriesAFix(string text) {
        foreach (Match match in Regex.Matches(text, @"RuleIds\.(\w+)")) {
            foreach (var rule in RuleCatalog.All) {
                if (rule.HasFix && Concept(rule.Concept) == match.Groups[1].Value) {
                    return true;
                }
            }
        }

        return false;
    }

    static string Concept(string concept) {
        var builder = new System.Text.StringBuilder(concept.Length);
        foreach (var part in concept.Split('-')) {
            if (part.Length > 0) {
                builder.Append(char.ToUpperInvariant(part[0])).Append(part, 1, part.Length - 1);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     ⚠ Comments stripped before the scan, in both directions. A <c>GetTypeByMetadataName</c>
    ///     named in a doc comment is not a call site, and — the one that would actually hurt — the
    ///     word <c>FrameworkAvailability</c> written in a comment must not be able to launder an
    ///     unguarded rule out of this ledger.
    /// </summary>
    static string WithoutComments(string text) {
        var builder = new System.Text.StringBuilder(text.Length);
        foreach (var line in text.Split('\n')) {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("//", StringComparison.Ordinal)) {
                builder.Append(line).Append('\n');
            }
        }

        return builder.ToString();
    }
}
