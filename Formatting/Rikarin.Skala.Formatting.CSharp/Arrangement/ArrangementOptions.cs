using System.Collections.Immutable;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>How much of the catalogue may run.</summary>
public enum ArrangementScope {
    /// <summary>
    ///     The subset that needs no compilation (docs/plan/06 § "A few arrangements need no semantics").
    ///     This is what <c>skala format --arrange=syntactic</c> gives an agent on a loose file.
    /// </summary>
    Syntactic,

    /// <summary>Everything, which needs a <see cref="Microsoft.CodeAnalysis.SemanticModel" />.</summary>
    Full
}

/// <summary>
///     The <c>arrange_*</c> and body-style settings of docs/plan/06, resolved.
/// </summary>
/// <remarks>
///     ⚠ A separate struct from <see cref="PhaseOneOptions" /> rather than more properties on it, and the
///     reason is a test rather than taste. <c>OptionCoverageTests</c> reads
///     <c>PhaseOneOptions.Implemented</c> and asserts that every option in it changes the output of
///     <em>the formatter</em> on some corpus file. An arrangement option changes the output of the
///     <em>arranger</em> and leaves the formatter's alone, so folding the two sets together would make
///     that assertion unprovable for a dozen keys and the honest fix — measure each family against the
///     thing that implements it — is two structs and two coverage lists.
/// </remarks>
public readonly struct ArrangementOptions {
    public ArrangementOptions(
        in FormattingOptions options,
        ArrangementScope scope = ArrangementScope.Full,
        bool aggressive = false
    ) {
        Scope = scope;
        Aggressive = aggressive;

        MethodOrOperatorBody = (BodyStyle)options.GetRaw(Ids.MethodOrOperatorBody);
        LocalFunctionBody = (BodyStyle)options.GetRaw(Ids.LocalFunctionBody);
        ConstructorOrDestructorBody = (BodyStyle)options.GetRaw(Ids.ConstructorOrDestructorBody);
        AccessorOwnerBody = (AccessorOwnerBodyStyle)options.GetRaw(Ids.AccessorOwnerBody);
        UseHeuristicsForBodyStyle = options.GetBool(Ids.UseHeuristicsForBodyStyle);

        VarForBuiltInTypes = options.GetBool(Ids.VarForBuiltInTypes);
        VarWhenTypeIsApparent = options.GetBool(Ids.VarWhenTypeIsApparent);
        VarElsewhere = options.GetBool(Ids.VarElsewhere);

        ObjectCreationWhenTypeEvident = (ObjectCreationStyle)options.GetRaw(Ids.ObjectCreationWhenTypeEvident);
        ObjectCreationWhenTypeNotEvident = (ObjectCreationStyle)options.GetRaw(Ids.ObjectCreationWhenTypeNotEvident);
        DefaultValueWhenTypeEvident = (DefaultValueStyle)options.GetRaw(Ids.DefaultValueWhenTypeEvident);
        DefaultValueWhenTypeNotEvident = (DefaultValueStyle)options.GetRaw(Ids.DefaultValueWhenTypeNotEvident);

        NullCheckingPattern = (NullCheckingPatternStyle)options.GetRaw(Ids.NullCheckingPattern);
        EmptyStringIsLiteral = string.Equals(
            options.GetText(Ids.EmptyString),
            "empty_literal",
            StringComparison.OrdinalIgnoreCase
        );

        // ⚠ The export writes `omit_if_default:suggestion`: one value and one severity in one key.
        // Only the value half governs the rewrite; the severity half is `skala check`'s (doc 06 §
        // "Body styles": don't nag, but do fix it when running cleanup). The registry strips the
        // suffix, which is why this reads as a plain enum.
        OmitDefaultAccessibility =
            (AccessibilityModifierStyle)options.GetRaw(Ids.RequireAccessibilityModifiers)
            == AccessibilityModifierStyle.OmitIfDefault;

        RemoveThisQualifier = options.GetBool(Ids.RemoveThisQualifier);
        QualifyField = options.GetBool(Ids.QualificationForField);
        QualifyProperty = options.GetBool(Ids.QualificationForProperty);
        QualifyMethod = options.GetBool(Ids.QualificationForMethod);
        QualifyEvent = options.GetBool(Ids.QualificationForEvent);
        BracesRedundant = options.GetBool(Ids.BracesRedundant);
        PredefinedTypeForLocals = options.GetBool(Ids.PredefinedTypeForLocals);
        PredefinedTypeForMemberAccess = options.GetBool(Ids.PredefinedTypeForMemberAccess);

        ParenthesesRedundancy = (ParenthesesRedundancyStyle)options.GetRaw(Ids.ParenthesesRedundancy);
        ParenthesesInArithmetic = (ParenthesesPreference)options.GetRaw(Ids.ParenthesesInArithmetic);
        ParenthesesInRelational = (ParenthesesPreference)options.GetRaw(Ids.ParenthesesInRelational);
        ParenthesesInOther = (ParenthesesPreference)options.GetRaw(Ids.ParenthesesInOther);
        NamespaceDeclarations = (NamespaceDeclarationStyle)options.GetRaw(Ids.NamespaceDeclarations);
        StaticMembersQualifyMembers = (MemberKind)options.GetRaw(Ids.StaticMembersQualifyMembers);
        StaticMembersQualifyWith = (QualifyWith)options.GetRaw(Ids.StaticMembersQualifyWith);
        TrailingCommaInMultilineLists = options.GetBool(Ids.TrailingCommaInMultilineLists);
        TrailingCommaInSinglelineLists = options.GetBool(Ids.TrailingCommaInSinglelineLists);
        PreferExplicitDiscardDeclaration = options.GetBool(Ids.PreferExplicitDiscardDeclaration);

        ArgumentsLiteral = (ArgumentStyle)options.GetRaw(Ids.ArgumentsLiteral);
        ArgumentsStringLiteral = (ArgumentStyle)options.GetRaw(Ids.ArgumentsStringLiteral);
        ArgumentsAnonymousFunction = (ArgumentStyle)options.GetRaw(Ids.ArgumentsAnonymousFunction);
        ArgumentsNamed = (ArgumentStyle)options.GetRaw(Ids.ArgumentsNamed);
        ArgumentsOther = (ArgumentStyle)options.GetRaw(Ids.ArgumentsOther);
        ArgumentsSkipSingle = options.GetBool(Ids.ArgumentsSkipSingle);

        SortUsings = options.GetBool(Ids.SortUsings);
        SystemDirectivesFirst = options.GetBool(Ids.SystemDirectivesFirst);
        UsingDirectivePlacement = (UsingDirectivePlacement)options.GetRaw(Ids.UsingDirectivePlacement);
        SeparateImportDirectiveGroups = options.GetBool(Ids.SeparateImportDirectiveGroups);
        KeepNontrivialAlias = options.GetBool(Ids.KeepNontrivialAlias);
        RemoveOnlyUnusedAliases = options.GetBool(Ids.RemoveOnlyUnusedAliases);

        MaxLineLength = Math.Max(1, options.GetInt(Ids.MaxLineLength));
        IndentSize = Math.Max(1, options.GetInt(Ids.IndentSize));

        FormatterTagsEnabled = options.GetBool(Ids.FormatterTagsEnabled);
        FormatterOffTag = options.GetString(Ids.FormatterOffTag) ?? "@formatter:off";
        FormatterOnTag = options.GetString(Ids.FormatterOnTag) ?? "@formatter:on";
        FormatterTagsAcceptRegexp = options.GetBool(Ids.FormatterTagsAcceptRegexp);
    }

    public ArrangementScope Scope { get; }

    /// <summary>
    ///     ⚠ Parenthesis removal only. docs/plan/06 § "Qualification and redundancy": it is the
    ///     highest-risk rewrite in the tool and it is gated for the first release regardless of what the
    ///     export says, and revisited when the corpus differential shows zero divergences.
    /// </summary>
    public bool Aggressive { get; }

    public BodyStyle MethodOrOperatorBody { get; }
    public BodyStyle LocalFunctionBody { get; }
    public BodyStyle ConstructorOrDestructorBody { get; }
    public AccessorOwnerBodyStyle AccessorOwnerBody { get; }
    public bool UseHeuristicsForBodyStyle { get; }

    public bool VarForBuiltInTypes { get; }
    public bool VarWhenTypeIsApparent { get; }
    public bool VarElsewhere { get; }

    public ObjectCreationStyle ObjectCreationWhenTypeEvident { get; }
    public ObjectCreationStyle ObjectCreationWhenTypeNotEvident { get; }
    public DefaultValueStyle DefaultValueWhenTypeEvident { get; }
    public DefaultValueStyle DefaultValueWhenTypeNotEvident { get; }

    public NullCheckingPatternStyle NullCheckingPattern { get; }
    public bool EmptyStringIsLiteral { get; }

    public bool OmitDefaultAccessibility { get; }
    public bool RemoveThisQualifier { get; }

    /// <summary>
    ///     <c>dotnet_style_qualification_for_field</c>: whether an instance field is written
    ///     <c>this.x</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ These four are the *adding* direction as well as the removing one, and they are the keys
    ///     the oracle actually consults. Measured against <c>jb cleanupcode</c> 2025.2.6 under
    ///     <see cref="Rikarin.Skala.Testing.OracleProfile" />'s cleanup profile, one key at a time on a
    ///     probe carrying both a bare and a <c>this.</c>-qualified reference to each member kind: at
    ///     <c>true</c> the oracle writes <c>this.</c> onto the bare reference of that kind and leaves the
    ///     other three alone; at <c>false</c> it strips an existing one. Four separate probes, four
    ///     disjoint one-line diffs.
    ///     <para>
    ///         ⚠ <see cref="RemoveThisQualifier" /> is <b>not</b> what the oracle reads. The same probe with
    ///         <c>resharper_remove_this_qualifier = false</c> and the four Roslyn keys left at the export's
    ///         <c>false</c> comes back byte-identical — the qualifier is still removed. The ReSharper key is
    ///         dominated by these four on this repository's configuration; Skala keeps reading it as a gate
    ///         on removal so that its own committed fixture still distinguishes it, and the divergence is
    ///         recorded as SK-DIV-0070.
    ///     </para>
    /// </remarks>
    public bool QualifyField { get; }

    /// <summary><c>dotnet_style_qualification_for_property</c>.</summary>
    public bool QualifyProperty { get; }

    /// <summary><c>dotnet_style_qualification_for_method</c>.</summary>
    public bool QualifyMethod { get; }

    /// <summary><c>dotnet_style_qualification_for_event</c>.</summary>
    public bool QualifyEvent { get; }

    public bool BracesRedundant { get; }
    public bool PredefinedTypeForLocals { get; }

    /// <summary>
    ///     ⚠ A separate key from <see cref="PredefinedTypeForLocals" />, and separate in the oracle too:
    ///     <c>dotnet_style_predefined_type_for_member_access</c> governs the receiver of a member access
    ///     (<c>Int32.MaxValue</c>) while the other governs a type in a declaration. Before this split the
    ///     rewrite read only the declaration key and applied it to both positions, which made the
    ///     member-access key unobservable — implemented behaviour credited to the wrong option.
    /// </summary>
    public bool PredefinedTypeForMemberAccess { get; }

    public ParenthesesRedundancyStyle ParenthesesRedundancy { get; }

    /// <summary>
    ///     <c>dotnet_style_parentheses_in_arithmetic_binary_operators</c>: whether parentheses around an
    ///     arithmetic operand of an arithmetic operator are kept.
    /// </summary>
    /// <remarks>
    ///     ⚠ The three keys partition binary operators by Roslyn's precedence *kind* — arithmetic
    ///     (<c>* / % + -</c>), relational (<c>&lt; &gt; &lt;= &gt;= == != is as</c>) and other
    ///     (<c>&amp;&amp; || ??</c>) — and each one governs only the parentheses whose <em>parent</em>
    ///     binary operator is in the same kind as the parenthesised expression's own. Measured, not read
    ///     off the names: with all three restated in a trailing <c>[*.cs]</c> section at the export's
    ///     values the oracle reproduces the repository's output byte for byte, and flipping one at a time
    ///     moves exactly one line of a ten-case probe. <c>(a + b) &gt; c</c> — arithmetic inside
    ///     relational — is removed at <em>every</em> combination of the three, and so is
    ///     <c>return (a + b);</c>, where the parent is not a binary operator at all.
    ///     <para>
    ///         ⚠ Restating one of the three in a later <c>.editorconfig</c> section resets the other two to
    ///         <em>Roslyn's</em> defaults rather than leaving the earlier section's values standing, which is
    ///         how a single-key probe silently measures three. Every reading here restates all three.
    ///     </para>
    ///     <para>
    ///         ⚠ Shift and the bitwise family are <em>not</em> governed by these keys.
    ///         <c>resharper_parentheses_non_obvious_operations</c> names them and keeps their operands'
    ///         parentheses whatever the three say — <c>a + (b &lt;&lt; c)</c> and <c>(a &amp; b) | c</c> hold
    ///         at all eight combinations.
    ///     </para>
    /// </remarks>
    public ParenthesesPreference ParenthesesInArithmetic { get; }

    /// <summary><c>dotnet_style_parentheses_in_relational_binary_operators</c>.</summary>
    public ParenthesesPreference ParenthesesInRelational { get; }

    /// <summary><c>dotnet_style_parentheses_in_other_binary_operators</c>.</summary>
    public ParenthesesPreference ParenthesesInOther { get; }

    public NamespaceDeclarationStyle NamespaceDeclarations { get; }
    public MemberKind StaticMembersQualifyMembers { get; }
    public QualifyWith StaticMembersQualifyWith { get; }
    public bool TrailingCommaInMultilineLists { get; }
    public bool TrailingCommaInSinglelineLists { get; }
    public bool PreferExplicitDiscardDeclaration { get; }

    public ArgumentStyle ArgumentsLiteral { get; }
    public ArgumentStyle ArgumentsStringLiteral { get; }
    public ArgumentStyle ArgumentsAnonymousFunction { get; }

    /// <summary>
    ///     <c>resharper_arguments_named</c>: an argument that <em>refers to</em> something by name.
    /// </summary>
    /// <remarks>
    ///     ⚠ A simple name or a member access, and nothing else, which is measured rather than read off
    ///     the key's wording. Asked with this key at <c>named</c> and the other four left alone, the
    ///     oracle names <c>local</c>, <c>Property</c>, <c>Field</c>, <c>Static</c> and
    ///     <c>holder.Value</c>; asked with <c>arguments_other</c> at <c>named</c> instead it names the
    ///     complement — an invocation, a binary expression, a cast, an element access, <c>typeof</c>,
    ///     <c>nameof</c>, <c>default</c>, <c>new</c> and a conditional. The two sets partition the
    ///     arguments that are not literals, strings or lambdas, with no overlap and nothing left over.
    /// </remarks>
    public ArgumentStyle ArgumentsNamed { get; }

    public ArgumentStyle ArgumentsOther { get; }

    /// <summary>
    ///     <c>resharper_arguments_skip_single</c>: leave a one-argument call alone whatever the four
    ///     style keys say.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured in the removal direction — with the export's <c>positional</c> everywhere and this
    ///     key on, <c>One(number: local)</c> and <c>One(number: 1)</c> keep names that a two-argument
    ///     call loses. It gates the argument list rather than one argument: "skip single arguments" is a
    ///     property of the call, and applying it per argument would exempt the last argument of every
    ///     call instead.
    /// </remarks>
    public bool ArgumentsSkipSingle { get; }

    public bool SortUsings { get; }
    public bool SystemDirectivesFirst { get; }

    /// <summary>
    ///     <c>csharp_using_directive_placement</c>: the using block above the namespace declaration or
    ///     inside its body.
    /// </summary>
    /// <remarks>
    ///     ⚠ The oracle performs this move under <c>CSOptimizeUsings</c>, measured: at
    ///     <c>inside_namespace</c> a file-scoped <c>namespace Probe;</c> comes back with the whole using
    ///     block below it, and at the export's <c>outside_namespace</c> a block written inside a
    ///     block-scoped namespace is hoisted above it.
    ///     <para>
    ///         ⚠ An <em>alias</em> directive written at nested scope is left where it is by the oracle when
    ///         hoisting, and this rule leaves it too. An alias resolves against the names in scope where it is
    ///         written, so moving it out of the namespace can change what it names.
    ///     </para>
    /// </remarks>
    public UsingDirectivePlacement UsingDirectivePlacement { get; }

    /// <summary>
    ///     <c>dotnet_separate_import_directive_groups</c>: a blank line between groups of usings that
    ///     share a first namespace segment.
    /// </summary>
    /// <remarks>
    ///     ⚠ The grouping is by the first segment and nothing finer, measured: <c>Alpha.Wide</c>,
    ///     <c>Beta.Wide</c>, <c>System</c> and <c>System.Text</c> come back as three groups, with
    ///     <c>System</c> and <c>System.Text</c> together.
    /// </remarks>
    public bool SeparateImportDirectiveGroups { get; }

    /// <summary>
    ///     <c>resharper_csharp_keep_nontrivial_alias</c>: an unused alias whose name is not the aliased
    ///     type's own name survives removal.
    /// </summary>
    /// <remarks>
    ///     ⚠ The registry recorded this key <c>inert</c> — "the oracle returns the alias unchanged at both
    ///     values" — and the probe that established it had the aliases <em>in use</em>, where nothing can
    ///     remove them at either value. With the same aliases unused the key separates cleanly:
    ///     <c>using Regex = System.Text.RegularExpressions.Regex;</c> is removed at both values and
    ///     <c>using Map = System.Collections.Generic.Dictionary&lt;string, int&gt;;</c> survives at
    ///     <c>true</c>. "Trivial" is measured to mean *the alias identifier equals the aliased type's own
    ///     name*, not "the target is short".
    ///     <para>
    ///         ⚠ It ANDs with <see cref="RemoveOnlyUnusedAliases" />: an unused non-trivial alias is removed
    ///         only when this is <c>false</c> <em>and</em> that is <c>true</c>, which is the export's pair and
    ///         the only one of the four combinations that removes it. Each key therefore moves the output on
    ///         its own from the export's configuration, which is why both are claimed rather than one.
    ///     </para>
    /// </remarks>
    public bool KeepNontrivialAlias { get; }

    /// <summary><c>resharper_remove_only_unused_aliases</c>. See <see cref="KeepNontrivialAlias" />.</summary>
    public bool RemoveOnlyUnusedAliases { get; }

    public int MaxLineLength { get; }
    public int IndentSize { get; }

    /// <summary>
    ///     The escape hatch, read by <see cref="FormatterTagGuard" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Inert in the option-coverage sense and not in any other: the formatter already claims these
    ///     four keys and its fixtures pin them, so claiming them again here would make the Tier A set
    ///     ambiguous about which component proves them. What arrangement adds is a *refusal*, and a
    ///     refusal has no output to differential — it is pinned by
    ///     <c>constructs/arrangement/formatter-tags/</c> instead.
    /// </remarks>
    public bool FormatterTagsEnabled { get; }

    public string FormatterOffTag { get; }
    public string FormatterOnTag { get; }
    public bool FormatterTagsAcceptRegexp { get; }

    /// <summary>The four keys as <see cref="FormatterTagGuard" /> wants them.</summary>
    public FormatterTags Tags => new(FormatterTagsEnabled, FormatterOffTag, FormatterOnTag, FormatterTagsAcceptRegexp);

    /// <summary>Every option the arranger reads — the arrangement half of the Tier A claim.</summary>
    public static ImmutableArray<OptionId> Implemented => Ids.All;

    /// <summary>
    ///     The <c>.editorconfig</c> spellings, resolved once.
    /// </summary>
    /// <remarks>
    ///     ⚠ The same shape as <see cref="PhaseOneOptions" />'s <c>Ids</c>, including the inert list:
    ///     <see cref="MaxLineLength" /> and <see cref="IndentSize" /> are read because the body-style
    ///     heuristic needs a column budget, not because arrangement implements them — the formatter
    ///     already claims both, and claiming them twice would make the Tier A set ambiguous about which
    ///     component's fixture pins them.
    /// </remarks>
    public static class Ids {
        static readonly List<OptionId> Collected = [];
        static readonly List<OptionId> Inert = [];

        public static readonly OptionId MethodOrOperatorBody = Of("resharper_csharp_method_or_operator_body");
        public static readonly OptionId LocalFunctionBody = Of("resharper_csharp_local_function_body");

        public static readonly OptionId ConstructorOrDestructorBody =
            Of("resharper_csharp_constructor_or_destructor_body");

        public static readonly OptionId AccessorOwnerBody = Of("resharper_csharp_accessor_owner_body");

        public static readonly OptionId UseHeuristicsForBodyStyle =
            Of("resharper_csharp_use_heuristics_for_body_style");

        public static readonly OptionId VarForBuiltInTypes = Of("csharp_style_var_for_built_in_types");
        public static readonly OptionId VarWhenTypeIsApparent = Of("csharp_style_var_when_type_is_apparent");
        public static readonly OptionId VarElsewhere = Of("csharp_style_var_elsewhere");

        public static readonly OptionId ObjectCreationWhenTypeEvident =
            Of("resharper_csharp_object_creation_when_type_evident");

        public static readonly OptionId ObjectCreationWhenTypeNotEvident =
            Of("resharper_csharp_object_creation_when_type_not_evident");

        public static readonly OptionId DefaultValueWhenTypeEvident =
            Of("resharper_csharp_default_value_when_type_evident");

        public static readonly OptionId DefaultValueWhenTypeNotEvident =
            Of("resharper_csharp_default_value_when_type_not_evident");

        public static readonly OptionId NullCheckingPattern = Of("resharper_csharp_null_checking_pattern_style");
        public static readonly OptionId EmptyString = Of("resharper_empty_string");

        public static readonly OptionId RequireAccessibilityModifiers =
            Of("dotnet_style_require_accessibility_modifiers");

        public static readonly OptionId RemoveThisQualifier = Of("resharper_remove_this_qualifier");

        public static readonly OptionId QualificationForField = Of("dotnet_style_qualification_for_field");

        public static readonly OptionId QualificationForProperty =
            Of("dotnet_style_qualification_for_property");

        public static readonly OptionId QualificationForMethod = Of("dotnet_style_qualification_for_method");
        public static readonly OptionId QualificationForEvent = Of("dotnet_style_qualification_for_event");

        public static readonly OptionId BracesRedundant = Of("resharper_csharp_braces_redundant");

        public static readonly OptionId PredefinedTypeForLocals =
            Of("dotnet_style_predefined_type_for_locals_parameters_members");

        public static readonly OptionId PredefinedTypeForMemberAccess =
            Of("dotnet_style_predefined_type_for_member_access");

        public static readonly OptionId ParenthesesRedundancy =
            Of("resharper_csharp_parentheses_redundancy_style");

        public static readonly OptionId ParenthesesInArithmetic =
            Of("dotnet_style_parentheses_in_arithmetic_binary_operators");

        public static readonly OptionId ParenthesesInRelational =
            Of("dotnet_style_parentheses_in_relational_binary_operators");

        public static readonly OptionId ParenthesesInOther =
            Of("dotnet_style_parentheses_in_other_binary_operators");

        public static readonly OptionId NamespaceDeclarations = Of("csharp_style_namespace_declarations");

        public static readonly OptionId StaticMembersQualifyMembers =
            Of("resharper_csharp_static_members_qualify_members");

        /// <summary>
        ///     ⚠ Read and inert, and it stays Tier D for it. <c>static_members_qualify_with</c> chooses
        ///     *which name* a qualifier is written with, and a qualifier is only ever written when
        ///     <c>static_members_qualify_members</c> names a member kind. The export writes
        ///     <c>none</c>, so on this repository's configuration nothing is ever added and the key
        ///     cannot change a byte of output — <c>declared_type</c> and <c>containing_type</c> produce
        ///     identical files. Honoured vacuously is not implemented, and doc 03's Tier A is a claim
        ///     about behaviour rather than about wiring.
        /// </summary>
        public static readonly OptionId StaticMembersQualifyWith =
            OfInert("resharper_csharp_static_members_qualify_with");

        public static readonly OptionId TrailingCommaInMultilineLists =
            Of("resharper_csharp_trailing_comma_in_multiline_lists");

        public static readonly OptionId TrailingCommaInSinglelineLists =
            Of("resharper_csharp_trailing_comma_in_singleline_lists");

        public static readonly OptionId PreferExplicitDiscardDeclaration =
            Of("resharper_csharp_prefer_explicit_discard_declaration");

        public static readonly OptionId ArgumentsLiteral = Of("resharper_csharp_arguments_literal");
        public static readonly OptionId ArgumentsStringLiteral = Of("resharper_csharp_arguments_string_literal");

        public static readonly OptionId ArgumentsAnonymousFunction =
            Of("resharper_csharp_arguments_anonymous_function");

        public static readonly OptionId ArgumentsNamed = Of("resharper_csharp_arguments_named");
        public static readonly OptionId ArgumentsOther = Of("resharper_csharp_arguments_other");
        public static readonly OptionId ArgumentsSkipSingle = Of("resharper_csharp_arguments_skip_single");

        public static readonly OptionId SortUsings = Of("resharper_sort_usings");
        public static readonly OptionId SystemDirectivesFirst = Of("dotnet_sort_system_directives_first");
        public static readonly OptionId UsingDirectivePlacement = Of("csharp_using_directive_placement");

        public static readonly OptionId SeparateImportDirectiveGroups =
            Of("dotnet_separate_import_directive_groups");

        public static readonly OptionId KeepNontrivialAlias = Of("resharper_csharp_keep_nontrivial_alias");
        public static readonly OptionId RemoveOnlyUnusedAliases = Of("resharper_remove_only_unused_aliases");

        public static readonly OptionId MaxLineLength = OfInert("max_line_length");
        public static readonly OptionId IndentSize = OfInert("indent_size");

        public static readonly OptionId FormatterTagsEnabled = OfInert("resharper_formatter_tags_enabled");
        public static readonly OptionId FormatterOffTag = OfInert("resharper_formatter_off_tag");
        public static readonly OptionId FormatterOnTag = OfInert("resharper_formatter_on_tag");

        public static readonly OptionId FormatterTagsAcceptRegexp =
            OfInert("resharper_formatter_tags_accept_regexp");

        public static ImmutableArray<OptionId> All { get; } = [.. Collected.Distinct().Except(Inert).Order()];

        static OptionId Of(string key) {
            if (!OptionRegistry.TryResolve(key, out var id)) {
                throw new InvalidOperationException($"'{key}' is not in the option registry.");
            }

            Collected.Add(id);
            return id;
        }

        static OptionId OfInert(string key) {
            var id = Of(key);
            Inert.Add(id);
            return id;
        }
    }
}
