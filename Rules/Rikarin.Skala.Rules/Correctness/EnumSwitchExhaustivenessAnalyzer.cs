using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary><c>SK2009</c> — a non-flags enum switch statement omits declared values and has no catch-all.</summary>
/// <remarks>
///     <para>
///         ⚠
///         <b>
///             The switch <em>expression</em> is the compiler's, and this rule does not look at
///             one.
///         </b> ADR-008 is host, never rebuild: <c>CS8509</c> ("does not handle all possible values
///         … the pattern 'K.C' is not covered") and <c>CS8524</c> (the undeclared-value half) are on by
///         default and name the missing member. Probed on a scratch project rather than recalled:
///         <c>k switch { K.A => 1, K.B => 2 }</c> draws <c>CS8509</c>, and the same switch written as a
///         statement draws nothing at all. The statement is the whole of what is left to say.
///     </para>
///     <para>
///         ⚠ <b>A statement that covers a minority of the enum is a filter, not a forgotten case</b>
///         (#280). A <c>switch</c> statement is under no obligation to be exhaustive — falling out of it
///         continues at the next statement, and that is the designed path for
///         <c>switch (modifier.Kind()) { case SyntaxKind.AsyncKeyword: return false; }</c>. The
///         boundary is <c>missing &lt;= handled</c>: a switch that already lists at least half the
///         declared values is visibly attempting exhaustiveness and forgot some; one listing three of
///         <c>SyntaxKind</c>'s 570 is selecting.
///     </para>
///     <para>
///         ⚠ Measured on Skala's own tree, where the rule made <b>14</b> findings and <b>13</b> were
///         false: every <c>SyntaxKind</c> and <c>SpecialType</c> filter, and — against #280's own
///         reading — the <c>JsonValueKind</c> one too, which recurses into <c>Object</c> and
///         <c>Array</c> and correctly ignores the six scalar kinds. The single survivor is
///         <c>OptionValueKind</c>, four of five members handled and <c>String</c> forgotten.
///     </para>
///     <para>
///         The recall this costs is real and unmeasured: a ten-member enum with three cases handled and
///         seven genuinely forgotten is now silent. The two candidates that would have kept it — a
///         member-count threshold, and "the enum is declared in this compilation" — were rejected on
///         #280, the second because <c>OptionValueKind</c> and <c>SyntaxKind</c> both arrive from
///         referenced assemblies and land on the same side of it.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EnumSwitchExhaustivenessAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.EnumSwitchMissingMembers);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var flags = start.Compilation.GetTypeByMetadataName("System.FlagsAttribute");
                start.RegisterSyntaxNodeAction(
                    context => AnalyzeStatement(context, flags),
                    SyntaxKind.SwitchStatement
                );
            }
        );
    }

    /// <summary>
    ///     ⚠ The exhaustiveness question itself lives in <see cref="EnumSwitchCoverage" /> rather than
    ///     here, because <c>SK0240</c> has to ask it too.
    /// </summary>
    /// <remarks>
    ///     <c>SK0240</c> offers to delete an empty <c>default:</c> section as dead control flow, and on a
    ///     non-exhaustive enum switch that section is the only thing keeping this rule quiet — so taking
    ///     the fix produced an <c>SK2009</c> the author did not have ([#321]). <c>SK0240</c> now asks the
    ///     shared predicate what its own fix would leave behind and stands down where the answer is a
    ///     finding. Sharing the code is what makes "where <c>SK2009</c> would fire" mean the same thing
    ///     in both rules; two copies of this logic would drift and re-open the loop.
    /// </remarks>
    static void AnalyzeStatement(SyntaxNodeAnalysisContext context, INamedTypeSymbol? flags) {
        var statement = (SwitchStatementSyntax)context.Node;
        if (EnumSwitchCoverage.Gap(
                context.SemanticModel,
                statement,
                flags,
                null,
                context.CancellationToken
            ) is not { } gap) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                statement.SwitchKeyword.GetLocation(),
                EnumSwitchCoverage.Describe(gap.Type, gap.Missing)
            )
        );
    }
}
