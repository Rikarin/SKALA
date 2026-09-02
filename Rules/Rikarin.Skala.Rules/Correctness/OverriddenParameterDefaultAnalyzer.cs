using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2140</c> — an override whose parameter metadata the call site will not use.
/// </summary>
/// <remarks>
///     <para>
///         A default value is not passed at run time; it is copied into the call site by the compiler
///         from the <em>static</em> type of the receiver. Through a base reference the base's default is
///         the one that lands in the IL and the override's is dead text, and through the derived type
///         the override's wins — so the same member behaves as two methods depending on which reference
///         the caller holds.
///     </para>
///     <para>
///         ⚠
///         <b>
///             <c>params</c> does not work like that, and the difference was measured rather than
///             assumed.
///         </b> An override does not get to change it: dropped, the call still expands through
///         the derived type; added where the base has none, it expands through neither. Roslyn says so
///         at the symbol level too — it propagates the base's <c>IsParams</c> onto the override's
///         parameter even where no keyword is written — so the comparison below simply cannot fire for
///         an override, which is the right answer arrived at by construction rather than by a guard. An
///         interface implementation inherits nothing, and there the divergence is real: dropping the
///         interface's <c>params</c> makes the same call compile through the interface and fail with
///         <c>CS1501</c> through the implementing type.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The whole declaration is one finding carrying every edit, and that is required rather
///             than tidy.
///         </b> C# makes optional parameters a suffix, so repairing one of two redundant
///         defaults on its own leaves <c>(int a = 1, int b)</c> — <c>CS1737</c>. <c>skala fix</c> applies
///         one finding at a time, so per-parameter findings would break the build between the first
///         edit and the second, on the tool's own advice.
///     </para>
///     <para>
///         ⚠ <b>Explicit interface implementations are declined because <c>CS1066</c> reports them</b>
///         — measured on a probe build, not assumed. The compiler says nothing at all about an override
///         or an implicit interface implementation, which is the whole gap this rule fills.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OverriddenParameterDefaultAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.OverriddenParameterDefault);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeIndexer, SyntaxKind.IndexerDeclaration);
    }

    static void AnalyzeMethod(SyntaxNodeAnalysisContext context) {
        var declaration = (MethodDeclarationSyntax)context.Node;

        // An explicit interface implementation is CS1066's business; see the remarks.
        if (declaration.ExplicitInterfaceSpecifier is not null) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } method) {
            return;
        }

        Compare(context, declaration.ParameterList, method, Overridden(method));
    }

    static void AnalyzeIndexer(SyntaxNodeAnalysisContext context) {
        var declaration = (IndexerDeclarationSyntax)context.Node;
        if (declaration.ExplicitInterfaceSpecifier is not null) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } indexer) {
            return;
        }

        Compare(context, declaration.ParameterList, indexer, Overridden(indexer));
    }

    /// <summary>
    ///     The declaration this one is bound to answer for, or null when there is not exactly one.
    /// </summary>
    /// <remarks>
    ///     ⚠ A member implementing several interface members that disagree with each other has no
    ///     single value to be aligned to, and a fix would have to pick one. It is declined rather than
    ///     reported against an arbitrary interface.
    /// </remarks>
    static ISymbol? Overridden(ISymbol member) {
        switch (member) {
            case IMethodSymbol { OverriddenMethod: { } overridden }:
                return overridden;

            case IPropertySymbol { OverriddenProperty: { } overridden }:
                return overridden;
        }

        if (member.IsOverride || member.ContainingType is not { } containing) {
            return null;
        }

        ISymbol? single = null;
        foreach (var @interface in containing.AllInterfaces) {
            foreach (var candidate in @interface.GetMembers()) {
                if (candidate.Kind != member.Kind
                    || !SymbolEqualityComparer.Default.Equals(
                        containing.FindImplementationForInterfaceMember(candidate),
                        member
                    )) {
                    continue;
                }

                if (single is not null) {
                    return null;
                }

                single = candidate;
            }
        }

        return single;
    }

    static ImmutableArray<IParameterSymbol> Parameters(ISymbol member) =>
        member switch {
            IMethodSymbol method => method.Parameters,
            IPropertySymbol property => property.Parameters,
            _ => ImmutableArray<IParameterSymbol>.Empty
        };

    static void Compare(
        SyntaxNodeAnalysisContext context,
        BaseParameterListSyntax list,
        ISymbol member,
        ISymbol? overridden
    ) {
        if (overridden is null) {
            return;
        }

        var declared = Parameters(member);
        var expected = Parameters(overridden);

        // ⚠ Bound on the shorter list, not on either one. An override always has the same arity as
        // what it overrides, but a broken or partially bound compilation does not have to, and an
        // index that is a fact only in the well-formed case is the shape of SK0232's crash (#298).
        var count = declared.Length < expected.Length ? declared.Length : expected.Length;
        if (count == 0 || list.Parameters.Count < count) {
            return;
        }

        var edits = new List<(TextSpan Span, string Text)>();
        ParameterSyntax? first = null;
        string? what = null;

        for (var i = 0; i < count; i++) {
            var syntax = list.Parameters[i];
            var mine = declared[i];
            var theirs = expected[i];

            if (mine.IsParams != theirs.IsParams) {
                if (!ParamsEdit(syntax, theirs.IsParams, edits)) {
                    return;
                }

                first ??= syntax;
                what ??= "`params` modifier";
                continue;
            }

            if (!DefaultDiffers(mine, theirs)) {
                continue;
            }

            if (!DefaultEdit(syntax, theirs, edits)) {
                return;
            }

            first ??= syntax;
            what ??= "default value";
        }

        if (first is null || edits.Count == 0) {
            return;
        }

        foreach (var edit in edits) {
            if (RewriteGuards.ContainsCommentOrDirective(context.Node.SyntaxTree, edit.Span)) {
                return;
            }
        }

        var kind = overridden.ContainingType is { TypeKind: TypeKind.Interface }
            ? "the interface member it implements"
            : "the member it overrides";

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                first.Identifier.GetLocation(),
                FixEdits.Pack(edits.ToArray()),
                "The "
                + what
                + " for '"
                + first.Identifier.Text
                + "' differs from "
                + kind
                + ", and the call site uses the one it can see"
            )
        );
    }

    /// <summary>
    ///     ⚠ Whether the two declarations disagree about the default at all — which is three questions,
    ///     not one.
    /// </summary>
    /// <remarks>
    ///     "Neither has one" is agreement; "one has one and the other does not" is a disagreement with
    ///     no value to compare; and "both have one" is a constant comparison. The constant comparison is
    ///     deliberately exact, so <c>0</c> against <c>0.0</c> reads as a mismatch rather than a match —
    ///     the direction that produces a finding a person can dismiss rather than a silent miss.
    /// </remarks>
    static bool DefaultDiffers(IParameterSymbol mine, IParameterSymbol theirs) {
        if (!mine.HasExplicitDefaultValue && !theirs.HasExplicitDefaultValue) {
            return false;
        }

        if (mine.HasExplicitDefaultValue != theirs.HasExplicitDefaultValue) {
            return true;
        }

        return !Equals(mine.ExplicitDefaultValue, theirs.ExplicitDefaultValue);
    }

    /// <summary>Rewrites this parameter's default clause into the one the base declares.</summary>
    static bool DefaultEdit(
        ParameterSyntax syntax,
        IParameterSymbol theirs,
        List<(TextSpan Span, string Text)> edits
    ) {
        if (!theirs.HasExplicitDefaultValue) {
            if (syntax.Default is null) {
                return false;
            }

            // ⚠ The whitespace before `=` goes with it. Left behind it produces `int a ` — legal, and
            // then reformatted by a separate pass, which turns one rule's fix into two diffs.
            edits.Add((TextSpan.FromBounds(syntax.Identifier.Span.End, syntax.Default.Span.End), string.Empty));
            return true;
        }

        // ⚠ The base's default is taken from its *syntax* where the source declares it, so `= Flags.None`
        // survives as written instead of collapsing to the `0` the constant is. A base in another
        // assembly has no syntax here and the literal is rendered from the constant instead; where
        // neither is possible the finding is dropped rather than fixed with a guess.
        var replacement = Written(theirs) ?? Rendered(theirs.ExplicitDefaultValue, theirs.Type);
        if (replacement is null) {
            return false;
        }

        if (syntax.Default is null) {
            edits.Add((new TextSpan(syntax.Identifier.Span.End, 0), " = " + replacement));
            return true;
        }

        edits.Add((syntax.Default.Value.Span, replacement));
        return true;
    }

    static string? Written(IParameterSymbol parameter) {
        foreach (var reference in parameter.DeclaringSyntaxReferences) {
            if (reference.GetSyntax() is ParameterSyntax { Default.Value: { } value }) {
                return value.ToString();
            }
        }

        return null;
    }

    static string? Rendered(object? value, ITypeSymbol type) {
        if (value is null) {
            return type.IsValueType && type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T
                ? "default"
                : "null";
        }

        switch (value) {
            case bool flag:
                return flag ? "true" : "false";

            case string text:
                return SyntaxFactory.Literal(text).ToString();

            case char character:
                return SyntaxFactory.Literal(character).ToString();
        }

        // ⚠ An enum-typed default arrives here as its boxed underlying value, so writing the number
        // back would compile only where an implicit conversion exists. Named or nothing.
        if (type.TypeKind == TypeKind.Enum) {
            foreach (var candidate in type.GetMembers()) {
                if (candidate is IFieldSymbol { HasConstantValue: true } field && Equals(field.ConstantValue, value)) {
                    return type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) + "." + field.Name;
                }
            }

            return null;
        }

        return value is System.IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : null;
    }

    /// <summary>Adds or removes the <c>params</c> modifier so the two declarations agree.</summary>
    static bool ParamsEdit(ParameterSyntax syntax, bool wanted, List<(TextSpan Span, string Text)> edits) {
        var written = syntax.Modifiers.IndexOf(SyntaxKind.ParamsKeyword);
        if (wanted) {
            if (written >= 0) {
                return false;
            }

            // ⚠ In front of the type, never in front of the first modifier: `params` follows `this`
            // and any attribute list, and `this params int[] xs` is the only legal order.
            return syntax.Type is { } type && Add(edits, new TextSpan(type.SpanStart, 0), "params ");
        }

        if (written < 0) {
            return false;
        }

        var keyword = syntax.Modifiers[written];
        var next = written + 1 < syntax.Modifiers.Count
            ? syntax.Modifiers[written + 1].SpanStart
            : syntax.Type?.SpanStart ?? keyword.Span.End;

        return Add(edits, TextSpan.FromBounds(keyword.SpanStart, next), string.Empty);
    }

    static bool Add(List<(TextSpan Span, string Text)> edits, TextSpan span, string text) {
        edits.Add((span, text));
        return true;
    }
}
