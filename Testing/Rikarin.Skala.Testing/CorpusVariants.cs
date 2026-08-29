namespace Rikarin.Skala.Testing;

/// <summary>One configuration a fixture set is run under besides the repository's own.</summary>
/// <param name="Name">
///     The infix in the fixture's file name: <c>invocation-parens.reflow-keep.expected.cs</c>.
/// </param>
/// <param name="Overrides">`.editorconfig` keys and values layered over the resolved configuration.</param>
public sealed record CorpusVariant(string Name, IReadOnlyList<KeyValuePair<string, string>> Overrides) {
    public string FixturePath(CorpusFile file) => Path.ChangeExtension(file.Path, null) + "." + Name + ".expected.cs";

    public bool HasFixture(CorpusFile file) => File.Exists(FixturePath(file));
}

/// <summary>
///     The alternative configurations a fixture set is measured under.
/// </summary>
/// <remarks>
///     ⚠ Milestone 1's harness had no notion of this: a corpus file was formatted with whatever its
///     <c>.editorconfig</c> chain resolved to, and nothing else. That is enough for an option whose two
///     values can be told apart on one file with the option flipped
///     (<c>OptionCoverageTests.EveryImplementedOption_ChangesTheOutputOfItsCorpusFile</c> does exactly
///     that), and it is not enough for docs/plan/05's four-way table, where the question is what two
///     keys do <em>in combination</em> and the answer has to be checked against the oracle in all four
///     corners rather than reasoned about.
///     <para>
///         ⚠ The corner that costs a repository its diff is (<c>keep_user_linebreaks = true</c>,
///         <c>keep_existing_X = false</c>), which reads like "reflow X" and is not. Measured against the
///         oracle: <c>Foo(\n a)</c> re-joins there and <c>Foo(\n a,\n b)</c> does not, because the two keys
///         govern different gaps — the delimiters belong to <c>keep_existing_X</c> and the gaps between
///         items belong to <c>keep_user_linebreaks</c>. Getting that backwards turns a first run on a large
///         tree into a rewrite of every call site.
///     </para>
/// </remarks>
public static class CorpusVariants {
    /// <summary>
    ///     The per-construct preservation keys, set together so the table is 2×2.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>resharper_csharp_keep_existing_linebreaks</c> is deliberately not here. It reads like one
    ///     of the family and it is not: it is the per-language form of the global
    ///     <c>keep_user_linebreaks</c>, so putting it on the <c>keep_existing_*</c> axis collapses the
    ///     table — both "reflow" corners come out identical to their "keep" neighbours and the 2×2 stops
    ///     measuring anything. Its own effect is pinned by its own fixture instead.
    /// </remarks>
    public static readonly string[] KeepExistingKeys = [
        "resharper_csharp_keep_existing_attribute_arrangement",
        "resharper_csharp_keep_existing_declaration_block_arrangement",
        "resharper_csharp_keep_existing_declaration_parens_arrangement",
        "resharper_csharp_keep_existing_embedded_arrangement",
        "resharper_csharp_keep_existing_embedded_block_arrangement", "resharper_csharp_keep_existing_enum_arrangement",
        "resharper_csharp_keep_existing_expr_member_arrangement",
        "resharper_csharp_keep_existing_invocation_parens_arrangement",
        "resharper_csharp_keep_existing_list_patterns_arrangement",
        "resharper_csharp_keep_existing_primary_constructor_declaration_parens_arrangement",
        "resharper_csharp_keep_existing_property_patterns_arrangement",
        "resharper_csharp_keep_existing_switch_expression_arrangement",
        "resharper_keep_existing_lambda_and_anonymous_function_parens_arrangement"
    ];

    /// <summary>The set whose files are run under <see cref="Preservation" />.</summary>
    public const string PreservationSet = "preservation/";

    /// <summary>docs/plan/05 § "keep_existing_*": all four combinations, named as they read.</summary>
    public static IReadOnlyList<CorpusVariant> Preservation { get; } = [
        Variant("keep-keep", keepUserLinebreaks: true, keepExisting: true),
        Variant(
            "keep-rearrange",
            keepUserLinebreaks: true,
            keepExisting: false
        ), Variant("reflow-keep", keepUserLinebreaks: false, keepExisting: true),
        Variant(
            "reflow-rearrange",
            keepUserLinebreaks: false,
            keepExisting: false
        )
    ];

    /// <summary>The variants a corpus file is additionally measured under; empty for most files.</summary>
    public static IReadOnlyList<CorpusVariant> For(CorpusFile file) =>
        file.Set == Corpus.Constructs && file.RelativePath.StartsWith(PreservationSet, StringComparison.Ordinal)
            ? Preservation
            : [];

    /// <summary>Every (file, variant) pair in a set, which is what a variant-aware run enumerates.</summary>
    public static IEnumerable<(CorpusFile File, CorpusVariant Variant)> Pairs(string set) {
        foreach (var file in Corpus.Files(set)) {
            foreach (var variant in For(file)) {
                yield return (file, variant);
            }
        }
    }

    static CorpusVariant Variant(string name, bool keepUserLinebreaks, bool keepExisting) {
        var overrides = new List<KeyValuePair<string, string>> {
            new("resharper_keep_user_linebreaks", keepUserLinebreaks ? "true" : "false"),
            new("resharper_keep_user_wrapping", keepUserLinebreaks ? "true" : "false")
        };

        foreach (var key in KeepExistingKeys) {
            overrides.Add(new KeyValuePair<string, string>(key, keepExisting ? "true" : "false"));
        }

        return new CorpusVariant(name, overrides);
    }
}
