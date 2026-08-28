namespace Rikarin.Skala.Testing;

/// <summary>
///     One <c>jb cleanupcode</c> profile, and the fixture extension its output is committed under.
/// </summary>
/// <remarks>
///     ⚠ Milestones 1–3.1 had exactly one profile — <c>CSReformatCode</c> and nothing else — and
///     <c>OracleRunner.Profile</c> was a constant because there was nothing to vary. Arrangement is a
///     *cleanup* profile, so the constant becomes a parameter and a corpus file grows a second
///     <c>.expected.cs</c> beside the first. Both fixtures are committed and both are compared; they
///     answer different questions and neither replaces the other.
///     <para>
///         ⚠ The two suffixes are deliberately both <c>*.expected.cs</c>, because <see cref="Corpus.Files" />
///         and <see cref="CorpusSample.IsExcluded" /> already refuse to treat such a file as a corpus input.
///         A fixture that the corpus enumerates as source is a fixture that gets formatted, compared against
///         itself, and counted twice.
///     </para>
/// </remarks>
public sealed record OracleProfile(string Name, string Suffix, string Tasks) {
    /// <summary>
    ///     Formatting only: whitespace moves, the tree does not. Milestones 1–3.1's whole oracle.
    /// </summary>
    public static OracleProfile FormatOnly { get; } = new(
        "SkalaFormatOnly",
        ".expected.cs",
        "<CSReformatCode>True</CSReformatCode><CSUpdateFileHeader>False</CSUpdateFileHeader>"
    );

    /// <summary>
    ///     The cleanup profile: the arrangement half of docs/plan/06, plus the reformat that follows it.
    /// </summary>
    /// <remarks>
    ///     ⚠ Every task in here was established by asking the tool, not by reading a name — see
    ///     <c>docs/oracle-cleanup-profile.md</c> for the sweep. Three of them matter enough to record
    ///     beside the list:
    ///     <list type="bullet">
    ///         <item>
    ///             <c>CSCodeStyleAttributes</c> is a *container*: its sub-tasks are XML attributes, not child
    ///             elements, and an attribute the tool does not know is silently ignored rather than rejected.
    ///             An empty <c>&lt;CSCodeStyleAttributes /&gt;</c> changes nothing, which is what makes the
    ///             per-attribute probe meaningful — every attribute below was observed to move a fixture on its
    ///             own.
    ///         </item>
    ///         <item>
    ///             <c>CSReorderTypeMembers</c> is deliberately absent. It is the one task the roadmap named that
    ///             the export does not configure: member ordering is driven by a file-layout XML that the
    ///             author's export does not ship, so switching it on measures ReSharper's built-in layout rather
    ///             than this repository's configuration. Its cost is measured rather than assumed —
    ///             <c>arrangement --reorder</c> reports it.
    ///         </item>
    ///         <item>
    ///             ⚠ <c>ArrangeNamespaces</c> and <c>ArrangeArgumentsStyle</c> were absent until they were
    ///             probed, and their absence was invisible in exactly the way the sweep warns about: the oracle
    ///             simply declined five of the export's own settings, and docs/plan/17 recorded all five as
    ///             arrangement Skala "declares and does not perform". They were missed because the probe list
    ///             was built from doc 06's catalogue rather than from the tool's own
    ///             <c>CodeCleanupTask_</c> resource names.
    ///         </item>
    ///         <item>
    ///             ⚠ <c>RemoveRedundantParentheses</c> is present and Skala now performs it by default too —
    ///             SK-DIV-0014's <c>--aggressive</c> gate is retired, priced at 4.25 points of changed-span
    ///             agreement before it was lifted.
    ///         </item>
    ///     </list>
    /// </remarks>
    public static OracleProfile Cleanup { get; } = new(
        "SkalaCleanup",
        ".arranged.expected.cs",
        "<CSReformatCode>True</CSReformatCode>"
        + "<CSUpdateFileHeader>False</CSUpdateFileHeader>"
        + "<CSOptimizeUsings><OptimizeUsings>True</OptimizeUsings><EmbraceInRegion>False</EmbraceInRegion><RegionName></RegionName></CSOptimizeUsings>"
        + "<CSArrangeQualifiers>True</CSArrangeQualifiers>"
        + "<CSFixBuiltinTypeReferences>True</CSFixBuiltinTypeReferences>"
        + "<CSCodeStyleAttributes"
        + " ArrangeVarStyle=\"True\""
        + " ArrangeCodeBodyStyle=\"True\""
        + " ArrangeObjectCreation=\"True\""
        + " ArrangeDefaultValue=\"True\""
        + " ArrangeTypeMemberAccessModifier=\"True\""
        + " ArrangeTypeAccessModifier=\"True\""
        + " SortModifiers=\"True\""
        + " ArrangeTrailingCommas=\"True\""
        + " ArrangeAttributes=\"True\""
        + " RemoveRedundantParentheses=\"True\""
        + " ArrangeArgumentsStyle=\"True\""
        + " ArrangeNamespaces=\"True\""
        + " />"
    );

    /// <summary>
    ///     Format-only, plus ReSharper's own documentation-comment task. The only profile that can be
    ///     asked what the oracle does to a <c>///</c> comment.
    /// </summary>
    /// <remarks>
    ///     ⚠ This profile exists because a whole family of options was Tier D for a reason that turned
    ///     out to be a property of the *profile* and not of the tool. SK-DIV-0006 read "every committed
    ///     fixture returns its documentation comments exactly as written" as "the oracle declines to
    ///     format documentation comments", and 22 keys — 21 <c>resharper_xmldoc_*</c> plus
    ///     <c>resharper_space_after_triple_slash</c> — were registered <c>OfUnoracled</c> on the strength
    ///     of it. <c>CSharpFormatDocComments</c> is a real <c>CodeCleanupTask_</c>, and
    ///     <see cref="FormatOnly" /> is byte-for-byte <c>Built-in: Reformat Code</c>, which is the one
    ///     built-in profile that switches it off.
    ///     <para>
    ///         ⚠ Re-measured before this profile was added, on a scratch solution carrying this
    ///         repository's <c>.editorconfig</c>, with the negative control the sweep's method demands:
    ///         <list type="bullet">
    ///             <item><c>&lt;CSReformatCode&gt;</c> alone — the doc comment came back byte-identical.</item>
    ///             <item>
    ///                 the same plus <c>&lt;CSharpFormatDocComments&gt;True&lt;/CSharpFormatDocComments&gt;</c>
    ///                 — the comment was rewrapped at <c>max_line_length</c>, the <c>///</c> markers grew
    ///                 their space, two crammed <c>&lt;param&gt;</c>s split onto their own lines, and the
    ///                 blank <c>///</c> lines went away.
    ///             </item>
    ///             <item>
    ///                 the same with the element renamed <c>ZZNotARealDocTask</c> — byte-identical, so the
    ///                 tool silently ignores an unknown task and the row above is not measuring a
    ///                 typo.
    ///             </item>
    ///         </list>
    ///     </para>
    ///     <para>
    ///         ⚠ The suffix obeys the rule above: it ends <c>.expected.cs</c>, so the corpus still refuses
    ///         to enumerate it as an input.
    ///     </para>
    ///     <para>
    ///         ⚠ <c>CSUpdateFileHeader</c> is off for the same reason it is off in <see cref="FormatOnly" />,
    ///         and the profile is otherwise <see cref="FormatOnly" /> exactly: one element apart, so a
    ///         difference between the two fixtures is a difference the doc-comment task made and nothing
    ///         else.
    ///     </para>
    /// </remarks>
    public static OracleProfile DocComments { get; } = new(
        "SkalaDocComments",
        ".xmldoc.expected.cs",
        "<CSReformatCode>True</CSReformatCode>"
        + "<CSUpdateFileHeader>False</CSUpdateFileHeader>"
        + "<CSharpFormatDocComments>True</CSharpFormatDocComments>"
    );

    /// <summary>All three profiles, in the order <c>./build.sh Oracle</c> regenerates them.</summary>
    public static IReadOnlyList<OracleProfile> All { get; } = [FormatOnly, Cleanup, DocComments];

    public static OracleProfile? ByName(string name) =>
        All.FirstOrDefault(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The <c>.DotSettings</c> document that defines this profile, and nothing else.</summary>
    public string SettingsFile =>
        """
        <wpf:ResourceDictionary xml:space="preserve" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:s="clr-namespace:System;assembly=mscorlib" xmlns:ss="urn:shemas-jetbrains-com:settings-storage-xaml" xmlns:wpf="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
        	<s:String x:Key="/Default/CodeStyle/CodeCleanup/Profiles/=PROFILE/@EntryIndexedValue">&lt;?xml version="1.0" encoding="utf-16"?&gt;&lt;Profile name="PROFILE"&gt;TASKS&lt;/Profile&gt;</s:String>
        </wpf:ResourceDictionary>
        """
                .Replace("PROFILE", Name, StringComparison.Ordinal)
                .Replace("TASKS", Escape(Tasks), StringComparison.Ordinal);

    /// <summary>
    ///     The profile document is XML whose one string value is *itself* XML, so the inner document is
    ///     escaped once. Writing the tasks pre-escaped by hand is how a profile silently becomes empty.
    /// </summary>
    static string Escape(string xml) =>
        xml.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
