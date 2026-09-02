using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1093</c> — <c>var w = (TextWriter)new StringWriter();</c> is
///     <c>TextWriter w = new StringWriter();</c>.
/// </summary>
/// <remarks>
///     <para>
///         The cast is not converting anything: the conversion is implicit, and the compiler emits
///         nothing for it. It is there only to talk <c>var</c> into inferring a wider type. Moving the
///         type to the left of the <c>=</c> states it where a reader looks for it and removes an
///         expression that reads like a runtime check and is not one.
///     </para>
///     <para>
///         ⚠ <b>Implicit, and not an identity.</b> Implicit is what makes <c>T x = expr;</c> compile
///         without the cast; an explicit cast such as <c>var s = (string)obj;</c> is a narrowing the
///         declaration cannot express. Not-an-identity is what keeps this out of
///         <see cref="Cleanup.RedundantCastAnalyzer" />'s territory — <c>SK0234</c> requires
///         <c>IsIdentity</c> and this requires its negation, so the two partition the space rather
///         than sharing it.
///     </para>
///     <para>
///         ⚠ <b>The cast must be the entire initializer.</b> <c>var total = (long)count * size;</c>
///         binds the cast to <c>count</c> alone, and moving <c>long</c> into the declaration would
///         change which multiplication happens. The guard is that the initializer node <em>is</em> the
///         cast, which that expression is not.
///     </para>
///     <para>
///         ⚠ <b><c>SK0202</c> and this rule cannot argue over the declaration, in either direction.</b>
///         <c>SK0202</c>'s <c>VarRule</c> converts an explicit type to <c>var</c> and only that
///         direction, and it returns on its first line for a declaration that is already <c>var</c> —
///         which is the only kind reported here, so the two never see the same span. After the fix the
///         declaration reads <c>T x = expr;</c> where <c>T</c> is deliberately <em>not</em> the
///         initializer's own type, and <c>SK0202</c> converts only when the two are identical, so it
///         declines and there is no cycle to enter.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CastInDeclarationAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CastInDeclaration);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LocalDeclarationStatement);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var statement = (LocalDeclarationStatementSyntax)context.Node;
        if (statement.UsingKeyword.RawKind != (int)SyntaxKind.None
            || statement.AwaitKeyword.RawKind != (int)SyntaxKind.None
            || statement.Modifiers.Count > 0
            || statement.AttributeLists.Count > 0
            || statement.Declaration.Variables.Count != 1
            || !statement.Declaration.Type.IsVar
            || statement.ContainsDirectives
            || statement.SpanContainsComment()) {
            return;
        }

        var declarator = statement.Declaration.Variables[0];

        // ⚠ The initializer must *be* the cast. `(long)count * size` is a binary expression whose
        // left operand is the cast, and hoisting the type out of it changes the arithmetic.
        if (declarator.Initializer?.Value is not CastExpressionSyntax cast) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (model.GetTypeInfo(cast.Type, cancellation).Type is not { } target
            || model.GetTypeInfo(cast.Expression, cancellation).Type is not { } source
            || target.TypeKind is TypeKind.Error or TypeKind.Dynamic or TypeKind.Pointer
            || source.TypeKind is TypeKind.Error or TypeKind.Dynamic or TypeKind.Pointer
            || source.SpecialType == SpecialType.System_Void) {
            return;
        }

        var conversion = model.ClassifyConversion(cast.Expression, target);
        if (!conversion.Exists
            || !conversion.IsImplicit
            || conversion.IsIdentity
            || SymbolEqualityComparer.Default.Equals(source, target)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                cast.Type.GetLocation(),
                FixEdits.Pack(
                    (statement.Declaration.Type.Span, cast.Type.ToString()),
                    (TextSpan.FromBounds(cast.OpenParenToken.SpanStart, cast.Expression.SpanStart), string.Empty)
                ),
                "The cast only widens, so `"
                + cast.Type
                + "` belongs in the declaration and the cast does not"
            )
        );
    }
}
