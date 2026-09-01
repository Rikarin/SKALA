using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Design;

/// <summary><c>SK6041</c> — a <c>foreach</c> variable declared as a base type of the element.</summary>
/// <remarks>
///     The collection already knows what it holds; declaring the loop variable wider throws that away
///     at the one point in the method where it was free. Where the element is a value type it also
///     boxes once per iteration.
///     <para>
///         ⚠ The safety argument that is <em>not</em> needed here is worth writing down, because it is
///         the first thing a reader looks for: there is no guard against the body reassigning the
///         variable, because C# forbids it. CS1656 — "cannot assign to an iteration variable" — means
///         the declared type is only ever read, so narrowing it can never invalidate a write.
///     </para>
///     <para>
///         ⚠ The conversion is classified rather than inferred from the type relationship. Only an
///         implicit reference conversion and a boxing conversion are widenings of the kind this rule
///         means; an implicit numeric one is an arithmetic width the body depends on, a nullable one
///         says the loop deals in absence, and a user-defined one is somebody's operator.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WiderForeachVariableTypeAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.WiderForeachVariableType);

    /// <summary>⚠ The nullable annotation is part of the answer, so it is part of the rendering.</summary>
    /// <remarks>
    ///     An element type of <c>string?</c> narrowed to a bare <c>string</c> would be the rule handing
    ///     out a declaration the compilation does not support. The minimal form is resolved against the
    ///     position of the loop, so the replacement uses the file's own <c>using</c> directives and never
    ///     needs a new one.
    /// </remarks>
    static readonly SymbolDisplayFormat Format = SymbolDisplayFormat.MinimallyQualifiedFormat
        .WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ForEachStatement);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var loop = (ForEachStatementSyntax)context.Node;

        // `var` already *is* the element type, and asking for it to be written down is the whole rule.
        if (loop.Type.IsVar) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(loop, context.CancellationToken) is not { Type: { } declared }) {
            return;
        }

        var element = context.SemanticModel.GetForEachStatementInfo(loop).ElementType;
        if (element is null
            || element.TypeKind is TypeKind.Error or TypeKind.Dynamic
            || declared.TypeKind is TypeKind.Error or TypeKind.Dynamic) {
            return;
        }

        // An anonymous type has no name the fix could write, and neither has a type the code holding
        // the loop cannot see. Both are silence rather than a finding without a fix: this rule's
        // whole content is "write the element type here", and it has to be writable.
        //
        // ⚠ `IsSymbolAccessibleWithin` throws on anything that is not a type or an assembly, and an
        // analyzer that throws is an AD0001 nobody reads — every positive fixture went quiet and the
        // negatives passed for the wrong reason. The containing *type* is what the question is
        // about in any case.
        var within = context.ContainingSymbol as INamedTypeSymbol ?? context.ContainingSymbol?.ContainingType;
        if (element.IsAnonymousType
            || within is null
            || !context.Compilation.IsSymbolAccessibleWithin(element, within)) {
            return;
        }

        if (context.Compilation is not CSharpCompilation compilation) {
            return;
        }

        var conversion = compilation.ClassifyConversion(element, declared);
        if (!conversion.Exists
            || !conversion.IsImplicit
            || conversion.IsIdentity
            || conversion.IsNumeric
            || conversion.IsNullable
            || conversion.IsUserDefined
            || !(conversion.IsReference || conversion.IsBoxing)) {
            return;
        }

        var replacement = element.ToMinimalDisplayString(context.SemanticModel, loop.Type.SpanStart, Format);

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                loop.Type.GetLocation(),
                FixEdits.Pack((new TextSpan(loop.Type.SpanStart, loop.Type.Span.Length), replacement)),
                "`"
                + loop.Identifier.ValueText
                + "` is declared `"
                + declared.ToMinimalDisplayString(context.SemanticModel, loop.Type.SpanStart, Format)
                + "` and the collection yields `"
                + replacement
                + "`; declare the element type the collection already knows"
            )
        );
    }
}
