using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2111</c> — a <c>!</c> standing where no nullable warning could have been issued.
/// </summary>
/// <remarks>
///     <c>!</c> is the strongest claim a C# author can make — <em>I know better than the compiler
///     here</em> — and it costs every subsequent reader a stop to work out what was known. Where there
///     was no warning to suppress the operator is decoration: it survives every migration untouched and
///     it makes a file look as though its nullability was thought about when it was not.
///     <para>
///         ⚠ <b><c>IDE0080</c> does not host this, and that was measured rather than assumed.</b>
///         <c>CSharpRemoveUnnecessaryNullableWarningSuppressionsDiagnosticAnalyzer</c> ships in the .NET
///         10.0.400 SDK and a probe carrying all three shapes reported nothing from it — with
///         <c>EnforceCodeStyleInBuild=true</c>, <c>AnalysisMode=All</c> and
///         <c>dotnet_diagnostic.IDE0080.severity = warning</c>. The instrument was verified in the same
///         build: <c>IDE0090</c> and <c>IDE0059</c> fired from the same analyzer set under the same
///         <c>.editorconfig</c>, so the silence is IDE0080's and not the code-style analyzers'.
///     </para>
///     <para>
///         ⚠ <b>The flow-state half of the concept is deliberately absent.</b> Reporting <c>x!</c>
///         because <c>x</c>'s flow state is <c>NotNull</c> is wrong twice over. A <c>!</c> can suppress a
///         <em>nested</em> nullability warning that the operand's own flow state says nothing about —
///         <c>List&lt;string?&gt; a = b!;</c> suppresses <c>CS8619</c> — and removing one <c>!</c> can
///         make another necessary, because in <c>x!.A(); x!.B();</c> the second operand is non-null
///         precisely <em>because of</em> the first suppression. A rule reporting both would hand
///         <c>skala fix</c> a pair of edits that together reintroduce the warning.
///     </para>
///     <para>
///         ⚠ What is left needs no flow analysis at all, which is why it is safe. Either nullable
///         <em>warnings</em> are off at that position — then nothing could have been reported there,
///         whatever the operand — or the operand is a non-nullable value type, on which <c>!</c> has
///         never suppressed anything. <c>int?</c> is excluded because <c>!</c> on it suppresses
///         <c>CS8629</c>, and an unconstrained type parameter is excluded because it is not known to be
///         a value type.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InertNullSuppressionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.InertNullSuppression);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SuppressNullableWarningExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var suppression = (PostfixUnaryExpressionSyntax)context.Node;

        // SK2113's ground. That `!` is not inert — it suppresses a warning that is telling the truth —
        // and the two rules are negations of one predicate so that neither can grow into the other.
        if (ServiceResolution.Match(context.SemanticModel, suppression, context.CancellationToken) is not null) {
            return;
        }

        var reason = Reason(context, suppression);
        if (reason is null) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                suppression.OperatorToken.GetLocation(),
                FixEdits.Pack((suppression.OperatorToken.Span, string.Empty)),
                reason
            )
        );
    }

    static string? Reason(SyntaxNodeAnalysisContext context, PostfixUnaryExpressionSyntax suppression) {
        if (!NullabilityFacts.WarningsEnabledAt(context.SemanticModel, suppression.SpanStart)) {
            return "nullable warnings are off here, so `!` suppresses nothing";
        }

        var type = context.SemanticModel.GetTypeInfo(suppression.Operand, context.CancellationToken).Type;
        if (type is null || !type.IsValueType) {
            return null;
        }

        // ⚠ `int?` is a value type too, and `!` on it is load-bearing: it suppresses CS8629 at the
        // `.Value`. `OriginalDefinition` rather than the constructed type, because `Nullable<int>` and
        // `Nullable<T>` are the same shape and only the definition is comparable.
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) {
            return null;
        }

        return "`" + type.Name + "` is a value type and cannot be null";
    }
}
