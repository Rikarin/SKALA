using System.Collections.Immutable;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>How much of the catalogue may run.</summary>
public enum ArrangementScope {
    /// <summary>
    /// The subset that needs no compilation (docs/plan/06 § "A few arrangements need no semantics").
    /// This is what <c>skala format --arrange=syntactic</c> gives an agent on a loose file.
    /// </summary>
    Syntactic,

    /// <summary>Everything, which needs a <see cref="Microsoft.CodeAnalysis.SemanticModel"/>.</summary>
    Full
}

/// <summary>
/// The <c>arrange_*</c> and body-style settings of docs/plan/06, resolved.
/// </summary>
/// <remarks>
/// ⚠ A separate struct from <see cref="PhaseOneOptions"/> rather than more properties on it, and the
/// reason is a test rather than taste. <c>OptionCoverageTests</c> reads
/// <c>PhaseOneOptions.Implemented</c> and asserts that every option in it changes the output of
/// <em>the formatter</em> on some corpus file. An arrangement option changes the output of the
/// <em>arranger</em> and leaves the formatter's alone, so folding the two sets together would make
/// that assertion unprovable for a dozen keys and the honest fix — measure each family against the
/// thing that implements it — is two structs and two coverage lists.
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
        BracesRedundant = options.GetBool(Ids.BracesRedundant);
        PredefinedTypeForLocals = options.GetBool(Ids.PredefinedTypeForLocals);
        PredefinedTypeForMemberAccess = options.GetBool(Ids.PredefinedTypeForMemberAccess);

        ParenthesesRedundancy = (ParenthesesRedundancyStyle)options.GetRaw(Ids.ParenthesesRedundancy);
        NamespaceDeclarations = (NamespaceDeclarationStyle)options.GetRaw(Ids.NamespaceDeclarations);
        StaticMembersQualifyMembers = (MemberKind)options.GetRaw(Ids.StaticMembersQualifyMembers);
        StaticMembersQualifyWith = (QualifyWith)options.GetRaw(Ids.StaticMembersQualifyWith);
        TrailingCommaInMultilineLists = options.GetBool(Ids.TrailingCommaInMultilineLists);
        TrailingCommaInSinglelineLists = options.GetBool(Ids.TrailingCommaInSinglelineLists);
        PreferExplicitDiscardDeclaration = options.GetBool(Ids.PreferExplicitDiscardDeclaration);

        ArgumentsLiteral = (ArgumentStyle)options.GetRaw(Ids.ArgumentsLiteral);
        ArgumentsStringLiteral = (ArgumentStyle)options.GetRaw(Ids.ArgumentsStringLiteral);
        ArgumentsAnonymousFunction = (ArgumentStyle)options.GetRaw(Ids.ArgumentsAnonymousFunction);
        ArgumentsOther = (ArgumentStyle)options.GetRaw(Ids.ArgumentsOther);

        SortUsings = options.GetBool(Ids.SortUsings);
        SystemDirectivesFirst = options.GetBool(Ids.SystemDirectivesFirst);

        MaxLineLength = Math.Max(1, options.GetInt(Ids.MaxLineLength));
        IndentSize = Math.Max(1, options.GetInt(Ids.IndentSize));

        FormatterTagsEnabled = options.GetBool(Ids.FormatterTagsEnabled);
        FormatterOffTag = options.GetString(Ids.FormatterOffTag) ?? "@formatter:off";
        FormatterOnTag = options.GetString(Ids.FormatterOnTag) ?? "@formatter:on";
        FormatterTagsAcceptRegexp = options.GetBool(Ids.FormatterTagsAcceptRegexp);
    }

    public ArrangementScope Scope { get; }

    /// <summary>
    /// ⚠ Parenthesis removal only. docs/plan/06 § "Qualification and redundancy": it is the
    /// highest-risk rewrite in the tool and it is gated for the first release regardless of what the
    /// export says, and revisited when the corpus differential shows zero divergences.
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
    public bool BracesRedundant { get; }
    public bool PredefinedTypeForLocals { get; }

    /// <summary>
    /// ⚠ A separate key from <see cref="PredefinedTypeForLocals"/>, and separate in the oracle too:
    /// <c>dotnet_style_predefined_type_for_member_access</c> governs the receiver of a member access
    /// (<c>Int32.MaxValue</c>) while the other governs a type in a declaration. Before this split the
    /// rewrite read only the declaration key and applied it to both positions, which made the
    /// member-access key unobservable — implemented behaviour credited to the wrong option.
    /// </summary>
    public bool PredefinedTypeForMemberAccess { get; }

    public ParenthesesRedundancyStyle ParenthesesRedundancy { get; }
    public NamespaceDeclarationStyle NamespaceDeclarations { get; }
    public MemberKind StaticMembersQualifyMembers { get; }
    public QualifyWith StaticMembersQualifyWith { get; }
    public bool TrailingCommaInMultilineLists { get; }
    public bool TrailingCommaInSinglelineLists { get; }
    public bool PreferExplicitDiscardDeclaration { get; }

    public ArgumentStyle ArgumentsLiteral { get; }
    public ArgumentStyle ArgumentsStringLiteral { get; }
    public ArgumentStyle ArgumentsAnonymousFunction { get; }
    public ArgumentStyle ArgumentsOther { get; }

    public bool SortUsings { get; }
    public bool SystemDirectivesFirst { get; }

    public int MaxLineLength { get; }
    public int IndentSize { get; }

    /// <summary>
    /// The escape hatch, read by <see cref="FormatterTagGuard"/>.
    /// </summary>
    /// <remarks>
    /// ⚠ Inert in the option-coverage sense and not in any other: the formatter already claims these
    /// four keys and its fixtures pin them, so claiming them again here would make the Tier A set
    /// ambiguous about which component proves them. What arrangement adds is a *refusal*, and a
    /// refusal has no output to differential — it is pinned by
    /// <c>constructs/arrangement/formatter-tags/</c> instead.
    /// </remarks>
    public bool FormatterTagsEnabled { get; }

    public string FormatterOffTag { get; }
    public string FormatterOnTag { get; }
    public bool FormatterTagsAcceptRegexp { get; }

    /// <summary>The four keys as <see cref="FormatterTagGuard"/> wants them.</summary>
    public FormatterTags Tags => new(FormatterTagsEnabled, FormatterOffTag, FormatterOnTag, FormatterTagsAcceptRegexp);

    /// <summary>Every option the arranger reads — the arrangement half of the Tier A claim.</summary>
    public static ImmutableArray<OptionId> Implemented => Ids.All;

    /// <summary>
    /// The <c>.editorconfig</c> spellings, resolved once.
    /// </summary>
    /// <remarks>
    /// ⚠ The same shape as <see cref="PhaseOneOptions"/>'s <c>Ids</c>, including the inert list:
    /// <see cref="MaxLineLength"/> and <see cref="IndentSize"/> are read because the body-style
    /// heuristic needs a column budget, not because arrangement implements them — the formatter
    /// already claims both, and claiming them twice would make the Tier A set ambiguous about which
    /// component's fixture pins them.
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
        public static readonly OptionId BracesRedundant = Of("resharper_csharp_braces_redundant");

        public static readonly OptionId PredefinedTypeForLocals =
            Of("dotnet_style_predefined_type_for_locals_parameters_members");

        public static readonly OptionId PredefinedTypeForMemberAccess =
            Of("dotnet_style_predefined_type_for_member_access");

        public static readonly OptionId ParenthesesRedundancy =
            Of("resharper_csharp_parentheses_redundancy_style");

        public static readonly OptionId NamespaceDeclarations = Of("csharp_style_namespace_declarations");

        public static readonly OptionId StaticMembersQualifyMembers =
            Of("resharper_csharp_static_members_qualify_members");

        public static readonly OptionId StaticMembersQualifyWith =
            Of("resharper_csharp_static_members_qualify_with");

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

        public static readonly OptionId ArgumentsOther = Of("resharper_csharp_arguments_other");

        public static readonly OptionId SortUsings = Of("resharper_sort_usings");
        public static readonly OptionId SystemDirectivesFirst = Of("dotnet_sort_system_directives_first");

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
