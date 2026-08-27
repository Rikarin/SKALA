namespace Rikarin.Skala.Testing;

/// <summary>
/// One <c>jb cleanupcode</c> profile, and the fixture extension its output is committed under.
/// </summary>
/// <remarks>
/// ⚠ Milestones 1–3.1 had exactly one profile — <c>CSReformatCode</c> and nothing else — and
/// <c>OracleRunner.Profile</c> was a constant because there was nothing to vary. Arrangement is a
/// *cleanup* profile, so the constant becomes a parameter and a corpus file grows a second
/// <c>.expected.cs</c> beside the first. Both fixtures are committed and both are compared; they
/// answer different questions and neither replaces the other.
/// <para>
/// ⚠ The two suffixes are deliberately both <c>*.expected.cs</c>, because <see cref="Corpus.Files"/>
/// and <see cref="CorpusSample.IsExcluded"/> already refuse to treat such a file as a corpus input.
/// A fixture that the corpus enumerates as source is a fixture that gets formatted, compared against
/// itself, and counted twice.
/// </para>
/// </remarks>
public sealed record OracleProfile(string Name, string Suffix, string Tasks) {
    /// <summary>
    /// Formatting only: whitespace moves, the tree does not. Milestones 1–3.1's whole oracle.
    /// </summary>
    public static OracleProfile FormatOnly { get; } = new(
        "SkalaFormatOnly",
        ".expected.cs",
        "<CSReformatCode>True</CSReformatCode><CSUpdateFileHeader>False</CSUpdateFileHeader>"
    );

    /// <summary>
    /// The cleanup profile: the arrangement half of docs/plan/06, plus the reformat that follows it.
    /// </summary>
    /// <remarks>
    /// ⚠ Every task in here was established by asking the tool, not by reading a name — see
    /// <c>docs/oracle-cleanup-profile.md</c> for the sweep. Three of them matter enough to record
    /// beside the list:
    /// <list type="bullet">
    /// <item>
    /// <c>CSCodeStyleAttributes</c> is a *container*: its sub-tasks are XML attributes, not child
    /// elements, and an attribute the tool does not know is silently ignored rather than rejected.
    /// An empty <c>&lt;CSCodeStyleAttributes /&gt;</c> changes nothing, which is what makes the
    /// per-attribute probe meaningful — every attribute below was observed to move a fixture on its
    /// own.
    /// </item>
    /// <item>
    /// <c>CSReorderTypeMembers</c> is deliberately absent. It is the one task the roadmap named that
    /// the export does not configure: member ordering is driven by a file-layout XML that the
    /// author's export does not ship, so switching it on measures ReSharper's built-in layout rather
    /// than this repository's configuration. Its cost is measured rather than assumed —
    /// <c>arrangement --reorder</c> reports it.
    /// </item>
    /// <item>
    /// ⚠ <c>RemoveRedundantParentheses</c> *is* present, and Skala's own parenthesis removal is
    /// gated behind <c>--aggressive</c> (docs/plan/06 § "Qualification and redundancy"). That is a
    /// deliberate, measured divergence rather than an oversight: the profile mirrors the export, and
    /// the gate's cost is a number in the M4 report instead of a hidden agreement.
    /// </item>
    /// </list>
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
        + " />"
    );

    /// <summary>Both profiles, in the order <c>./build.sh Oracle</c> regenerates them.</summary>
    public static IReadOnlyList<OracleProfile> All { get; } = [FormatOnly, Cleanup];

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
    /// The profile document is XML whose one string value is *itself* XML, so the inner document is
    /// escaped once. Writing the tasks pre-escaped by hand is how a profile silently becomes empty.
    /// </summary>
    static string Escape(string xml) =>
        xml.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
