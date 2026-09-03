using Rikarin.Skala.Options;
using System.Collections.Immutable;

namespace Rikarin.Skala.Core.Configuration;

/// <summary>Where a value came from, and under which of the option's spellings.</summary>
public sealed record OptionOrigin(EditorConfigAssignment Assignment, int Specificity) {
    public string File => Assignment.File;
    public int Line => Assignment.Line;
    public string Spelling => Assignment.Key;
    public string Value => Assignment.Value;
}

/// <summary>One option's effective value for one file.</summary>
/// <param name="Refused">
///     ⚠ The line that set this option to something outside its domain, when there is one. It is not
///     <see cref="Origin" /> — the value never took effect — and it is not nothing either, which is
///     what <c>config explain</c> used to print: <c>(default)</c>, beside an option the
///     <c>.editorconfig</c> visibly sets. A reader cannot act on a report that hides the line it is
///     talking about, so the refusal travels with the option and carries its own file and line.
/// </param>
public sealed record ResolvedOption(
    OptionId Id,
    string Value,
    OptionOrigin? Origin,
    ImmutableArray<OptionOrigin> Candidates,
    OptionOrigin? Refused = null) {
    public OptionInfo Info => OptionRegistry.Get(Id);

    /// <summary>True when nothing in the chain set the option and the registry default is in force.</summary>
    public bool IsDefault => Origin is null;

    public string SourceText =>
        Origin is not null
        ? Located(Origin)
        : Refused is null
            ? "(default)"
            : $"(default) ⚠ {Diagnostics.ConfigDiagnosticIds.OptionValueOutOfDomain} {Located(Refused)}";

    static string Located(OptionOrigin origin) =>
        $"{origin.File}:{origin.Line.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
}

/// <summary>
///     One assignment that named an option Skala owns and gave it a value the option does not accept.
/// </summary>
/// <remarks>
///     ⚠ A record rather than the pre-formatted string this used to be. The string was computed on
///     every resolution and read by nothing outside the tests — the whole of SK9017's defect — and a
///     diagnostic needs the parts separately: the file and line to point at, the domain to explain,
///     and <see cref="Effective" /> to answer the only question the user actually has, which is what
///     their code is being formatted with now.
/// </remarks>
/// <param name="Effective">
///     The value in force in <see cref="ResolutionResult.Options" /> once the whole chain has been
///     resolved — the registry default, unless a generalized key reached this option afterwards.
///     Measured from the built options rather than assumed from the registry, because those two
///     answers differ exactly when the report matters most.
/// </param>
public sealed record OptionValueError(
    OptionId Id,
    string Spelling,
    string Value,
    string Reason,
    string Effective,
    string File,
    int Line) {
    public string Location => $"{File}:{Line.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    public override string ToString() =>
        $"{Location}: {Spelling} = {Value}: {Reason}; '{Effective}' is in force instead";
}

/// <summary>A key in the configuration that the registry does not know.</summary>
public sealed record UnknownKey(EditorConfigAssignment Assignment, KeyNamespace Namespace);

/// <summary>
///     What kind of key an unrecognised name is. Only <see cref="Option" /> is an SK9001: the export
///     carries three thousand inspection severities, and a tool that warns about all of them on first
///     run gets uninstalled on first run.
/// </summary>
public enum KeyNamespace {
    /// <summary>A style option Skala does not have in its registry.</summary>
    Option,
    /// <summary><c>resharper_*_highlighting</c> — an inspection severity. Milestone 5.</summary>
    InspectionSeverity,
    /// <summary><c>dotnet_diagnostic.*.severity</c> — a Roslyn analyzer severity. Milestone 5.</summary>
    DiagnosticSeverity,
    /// <summary><c>dotnet_naming_*</c> — passed to Roslyn's hosted IDE1006 analyzer (doc 03).</summary>
    NamingRule,
    /// <summary><c>root</c>, and anything else structural.</summary>
    Structural
}

public sealed record ResolutionResult(
    string SourcePath,
    EditorConfigChain Chain,
    FormattingOptions Options,
    ImmutableArray<ResolvedOption> Resolved,
    ImmutableArray<UnknownKey> Unknown,
    ImmutableArray<OptionValueError> ValueErrors) {
    public ResolvedOption this[OptionId id] => Resolved[(int)id];

    public IEnumerable<ResolvedOption> Configured => Resolved.Where(static option => !option.IsDefault);
}

/// <summary>
///     Resolves the effective option set for a file, with provenance.
/// </summary>
/// <remarks>
///     docs/plan/03-configuration-model.md § "Precedence": the chain nearest-last, later sections
///     within a file winning over earlier ones, and — the part no other tool implements —
///     <c>skala_x</c> beating <c>csharp_x</c> beating the generic key within one level.
/// </remarks>
public static class OptionResolver {
    /// <remarks>
    ///     ⚠ Read from the generated <see cref="OptionKeyPrefixes" /> rather than restated. This list
    ///     was one of four hand-written copies that no test compared, and it was the only one of the
    ///     four carrying <c>xmldoc_</c>.
    /// </remarks>
    static readonly string[] SpecificityPrefixes = OptionKeyPrefixes.Ordered;

    public static ResolutionResult Resolve(
        string sourcePath,
        IReadOnlyList<KeyValuePair<string, string>>? overrides = null
    ) =>
        Resolve(EditorConfigChain.For(sourcePath), overrides);

    public static ResolutionResult Resolve(
        EditorConfigChain chain,
        IReadOnlyList<KeyValuePair<string, string>>? overrides = null
    ) {
        var winners = new OptionOrigin?[OptionRegistry.Count];

        // ⚠ Which document in the chain the winner came from, kept only for `Expand`. Provenance
        // already records the file by name; this is an ordinal, because a generalized key and the
        // key it names are two different options and "which line is later" is the only question
        // that tells them apart.
        var winnerDocument = new int[OptionRegistry.Count];
        Array.Fill(winnerDocument, -1);
        var documentIndex = -1;
        var candidates = new List<OptionOrigin>?[OptionRegistry.Count];
        var unknown = ImmutableArray.CreateBuilder<UnknownKey>();
        var refused = new (OptionOrigin Origin, string Reason)?[OptionRegistry.Count];
        var builder = new FormattingOptionsBuilder();

        foreach (var document in chain.Documents) {
            documentIndex++;
            var perDocument = new OptionOrigin?[OptionRegistry.Count];
            foreach (var section in document.Sections) {
                if (!SectionMatcher.Matches(section, chain.SourcePath)) {
                    continue;
                }

                foreach (var assignment in section.Assignments) {
                    if (!OptionRegistry.TryResolve(assignment.Key, out var id)) {
                        unknown.Add(new UnknownKey(assignment, Classify(assignment.Key)));
                        continue;
                    }

                    var origin = new OptionOrigin(assignment, SpecificityOf(assignment.Key));
                    (candidates[(int)id] ??= []).Add(origin);

                    // Later sections win for the same spelling; a more specific spelling wins
                    // outright, whichever section it is in.
                    var previous = perDocument[(int)id];
                    if (previous is null || origin.Specificity <= previous.Specificity) {
                        perDocument[(int)id] = origin;
                    }
                }
            }

            for (var i = 0; i < perDocument.Length; i++) {
                if (perDocument[i] is { } origin) {
                    winners[i] = origin;
                    winnerDocument[i] = documentIndex;
                }
            }
        }

        if (overrides is not null) {
            foreach (var (key, value) in overrides) {
                var document = EditorConfigDocument.FromText(
                    "(command line)",
                    $"[*]{Environment.NewLine}{key} = {value}{Environment.NewLine}"
                );
                var assignment = document.Sections[1].Assignments[0];

                // ⚠ An unknown `--option` is the same fact as an unknown key in a file, so it goes
                // through the same channel and is reported as the same SK9001. It used to be
                // appended to the value-error list, where — like every other value error before
                // M9 — nothing read it.
                if (!OptionRegistry.TryResolve(key, out var id)) {
                    unknown.Add(new UnknownKey(assignment, Classify(key)));
                    continue;
                }

                winners[(int)id] = new(assignment, -1);
                winnerDocument[(int)id] = int.MaxValue;
                (candidates[(int)id] ??= []).Add(winners[(int)id]!);
            }
        }

        // ⚠ Before anything is applied, because `indent_size = tab` names a value that lives in
        // another option and the two are not resolved in a fixed order (`indent_size` sorts before
        // `tab_width`, so reading it during the apply loop would read the default every time).
        var applied = SubstituteTabAliases(winners);

        var resolved = ImmutableArray.CreateBuilder<ResolvedOption>(OptionRegistry.Count);
        for (var i = 0; i < OptionRegistry.Count; i++) {
            var id = (OptionId)i;
            var info = OptionRegistry.Get(id);
            var origin = winners[i];
            if (origin is not null && !builder.TrySet(id, applied[i] ?? origin.Value, out var error)) {
                refused[i] = (origin, error ?? "not a value this option accepts");
                origin = null;
            }

            var value = origin?.Value ?? info.Default ?? string.Empty;
            resolved.Add(new ResolvedOption(id, value, origin, [.. candidates[i] ?? []], refused[i]?.Origin));
        }

        Expand(winners, winnerDocument, applied, builder);

        // ⚠ After `Expand`, so that "what is in force instead" is measured rather than assumed. The
        // fallback is normally the registry default, and is not when a generalized key names this
        // option — which is exactly the case where a user reading `(default)` would be misled.
        var options = builder.Build();
        var errors = ImmutableArray.CreateBuilder<OptionValueError>();
        for (var i = 0; i < refused.Length; i++) {
            if (refused[i] is not var (origin, reason)) {
                continue;
            }

            var effective = options.GetText((OptionId)i);
            errors.Add(
                new OptionValueError(
                    (OptionId)i,
                    origin.Spelling,
                    origin.Value,
                    reason,
                    effective,
                    origin.File,
                    origin.Line
                )
            );

            // ⚠ The reported value for a refused option is the one in force, not the registry
            // default it is usually equal to. There is no "what the file says" here — the file's
            // value never took effect — so the only honest number is the one the formatter will
            // read, and a generalized key can have moved it after the refusal.
            resolved[i] = resolved[i] with { Value = effective };
        }

        return new(
            chain.SourcePath,
            chain,
            options,
            resolved.MoveToImmutable(),
            unknown.ToImmutable(),
            errors.ToImmutable()
        );
    }

    /// <summary>
    ///     Writes each generalized key's value into the keys it names.
    /// </summary>
    /// <remarks>
    ///     docs/plan/03 § "The option registry": a generalized key is ReSharper's way of setting a
    ///     group of options with one line, and <c>Expands</c> has recorded which group since the
    ///     registry was distilled — but nothing applied it, so a configuration that set only the
    ///     generalized key left every key in the group at its default. Measured against the oracle
    ///     rather than assumed: <c>space_before_open_square_brackets = true</c> alone produces
    ///     <c>int [] data</c> and <c>data [1]</c>, and <c>space_around_ternary_operator = false</c>
    ///     produces <c>flag?a:b</c> even though this export sets all four ternary keys directly.
    ///     <para>
    ///         ⚠ Later wins, not "more specific wins". A generalized key and a key it names are two
    ///         different options, so docs/plan/03 § "Precedence" step 3 — which orders
    ///         <em>
    ///             spellings of one
    ///             option
    ///         </em> — has nothing to say about the pair, and the oracle answers by position: the
    ///         same assignment appended after the group's members overrides them and written before them
    ///         does not. Specificity still breaks a tie, which is what makes
    ///         <c>skala_space_after_keywords_in_control_flow_statements</c> beat its <c>csharp_</c>
    ///         twin — the one case where the two spellings really are the same ReSharper property.
    ///     </para>
    ///     <para>
    ///         ⚠ Fidelity-neutral on the Rider export, and checked rather than hoped: every generalized key
    ///         there carries the same value as every key it names.
    ///     </para>
    ///     <para>
    ///         ⚠ Values only, never provenance. <see cref="ResolutionResult.Resolved" /> and therefore
    ///         <c>skala config explain</c> keep saying what the file says, because "this option is set" and
    ///         "this option's value came from a line that named a different option" are different claims
    ///         and only the first belongs in a provenance column.
    ///     </para>
    /// </remarks>
    static void Expand(
        OptionOrigin?[] winners,
        int[] winnerDocument,
        string?[] applied,
        FormattingOptionsBuilder builder
    ) {
        // (target, winning generalized source) — resolved before anything is written, so that two
        // generalized keys naming one option cannot depend on the order they are visited in.
        var chosen = new (OptionOrigin Origin, int Document, int Source)?[OptionRegistry.Count];
        for (var i = 0; i < winners.Length; i++) {
            if (winners[i] is not { } origin) {
                continue;
            }

            foreach (var target in OptionRegistry.Get((OptionId)i).Expands) {
                if (Outranks(origin, winnerDocument[i], winners[(int)target], winnerDocument[(int)target])
                    && Outranks(
                        origin,
                        winnerDocument[i],
                        chosen[(int)target]?.Origin,
                        chosen[(int)target]?.Document ?? -1
                    )) {
                    chosen[(int)target] = (origin, winnerDocument[i], i);
                }
            }
        }

        for (var i = 0; i < chosen.Length; i++) {
            // A domain mismatch is not an error the user can act on — they never wrote the target's
            // name. `place_attribute_on_same_line = false` names options whose domain is an enum,
            // and silently declining is what leaves them at their own default.
            //
            // ⚠ The *applied* text, not the written one: `indent_size = tab` propagates the width it
            // resolved to, because `tab` is not a value the keys it names would accept and
            // propagating it would silently leave every one of them at its own default.
            if (chosen[i] is { } source) {
                builder.TrySet((OptionId)i, applied[source.Source] ?? source.Origin.Value, out _);
            }
        }
    }

    /// <summary>
    ///     Resolves <c>indent_size = tab</c> to the width <c>tab_width</c> carries.
    /// </summary>
    /// <returns>
    ///     Per option, the text to apply, or <c>null</c> where it is the written text unchanged.
    /// </returns>
    /// <remarks>
    ///     ⚠ <b><c>tab</c> is a legal <c>indent_size</c> in the EditorConfig specification</b>, which
    ///     says the value "when set to <c>tab</c>" means use the value of <c>tab_width</c>. Skala typed
    ///     the key <c>int</c> and refused it, which is a spec-conformant file rejected by the tool
    ///     whose whole job is reading that file — and, until SK9017, refused in silence.
    ///     <para>
    ///         ⚠ It is resolved here rather than in <c>TrySet</c> because the builder cannot see the rest
    ///         of the configuration. Options are applied in ordinal key order, <c>indent_size</c> sorts
    ///         before <c>tab_width</c>, and a <c>TrySet</c> that reached across would therefore read
    ///         <c>tab_width</c>'s default in exactly the file that configures it. It also has to happen
    ///         before <c>Expand</c>: <c>indent_size</c> is a generalized key, and the keys it names take
    ///         a number.
    ///     </para>
    ///     <para>
    ///         ⚠ The written value is what <see cref="ResolvedOption.Value" /> keeps reporting.
    ///         <c>config explain</c> answers "what does my configuration say", and the answer is
    ///         <c>tab</c>; the number it resolved to is <c>tab_width</c>'s own row.
    ///     </para>
    /// </remarks>
    static string?[] SubstituteTabAliases(OptionOrigin?[] winners) {
        var applied = new string?[OptionRegistry.Count];
        for (var i = 0; i < winners.Length; i++) {
            if (winners[i] is not { } origin
                || OptionRegistry.Get((OptionId)i).TabMeans is not { } width
                || !origin.Value.Trim().Equals("tab", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            // The width in force for `tab_width`: what the file says when the file says something
            // usable, and the registry default otherwise. A bad `tab_width` is its own SK9017.
            var configured = winners[(int)width]?.Value.Trim();
            applied[i] = configured is not null
                && int.TryParse(
                    configured,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _
                )
                    ? configured
                    : OptionRegistry.Get(width).Default;
        }

        return applied;
    }

    /// <summary>Later in the chain wins; within one document, later line; then more specific.</summary>
    static bool Outranks(OptionOrigin origin, int document, OptionOrigin? other, int otherDocument) {
        if (other is null) {
            return true;
        }

        if (document != otherDocument) {
            return document > otherDocument;
        }

        return origin.Line != other.Line ? origin.Line > other.Line : origin.Specificity <= other.Specificity;
    }

    /// <summary>Lower is more specific. docs/plan/03 § "Precedence" step 3.</summary>
    public static int SpecificityOf(string key) {
        for (var i = 0; i < SpecificityPrefixes.Length; i++) {
            if (key.StartsWith(SpecificityPrefixes[i], StringComparison.Ordinal)) {
                return i;
            }
        }

        return SpecificityPrefixes.Length;
    }

    public static KeyNamespace Classify(string key) {
        if (key.EndsWith("_highlighting", StringComparison.Ordinal)) {
            return KeyNamespace.InspectionSeverity;
        }

        if (key.StartsWith("dotnet_diagnostic.", StringComparison.Ordinal)) {
            return KeyNamespace.DiagnosticSeverity;
        }

        if (key.StartsWith("dotnet_naming_", StringComparison.Ordinal)) {
            return KeyNamespace.NamingRule;
        }

        return key is "root" ? KeyNamespace.Structural : KeyNamespace.Option;
    }
}
