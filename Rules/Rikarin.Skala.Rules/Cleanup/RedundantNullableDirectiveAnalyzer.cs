using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary><c>SK0242</c> — a <c>#nullable</c> directive that sets the state already in effect.</summary>
/// <remarks>
///     <para>
///         The file's nullable state is two independent settings — annotations and warnings — each of
///         which is enabled, disabled, or inherited from the project. Every <c>#nullable</c> directive
///         moves one or both; a directive that moves neither is a line that says something and does
///         nothing.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The file's opening state is <em>inherited</em>, not enabled, and that is what makes this
///             rule exact without a project.
///         </b> The first <c>#nullable enable</c> in a file is never
///         reported, because whether the project already enables annotations is not written in the file
///         and this rule does not guess. What the opening state does settle is the other direction: a
///         <c>#nullable restore</c> before any other directive restores the project default the file
///         already had, and that is a no-op no project setting can change.
///     </para>
///     <para>
///         ⚠ <b>A conditional directive anywhere in the file withdraws every finding in it.</b> Which
///         <c>#nullable</c> directives are live inside <c>#if</c> depends on the preprocessor symbols
///         the compilation was given, so a file that is clean under one <c>#define</c> set and
///         redundant under another has no single answer — and a fix applied under the wrong one deletes
///         a directive the other configuration needs.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantNullableDirectiveAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantNullableDirective);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    /// <summary>One of the two settings a <c>#nullable</c> directive moves.</summary>
    enum Setting {
        /// <summary>Whatever the project says. The state every file opens in.</summary>
        Inherited,

        Enabled,
        Disabled
    }

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CompilationUnit);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var unit = (CompilationUnitSyntax)context.Node;
        var nullable = new List<NullableDirectiveTriviaSyntax>();
        for (var directive = unit.GetFirstDirective();
             directive is not null;
             directive = directive.GetNextDirective()) {
            if (directive is BranchingDirectiveTriviaSyntax or EndIfDirectiveTriviaSyntax) {
                return;
            }

            // ⚠ An inactive directive is one inside a branch this compilation did not take. It sets
            // nothing, so it must not move the state either — reading it would make the directive after
            // it look redundant when it is the only one that runs.
            if (directive is NullableDirectiveTriviaSyntax { IsActive: true } setting) {
                nullable.Add(setting);
            }
        }

        var annotations = Setting.Inherited;
        var warnings = Setting.Inherited;
        foreach (var directive in nullable) {
            var target = Target(directive);
            var movesAnnotations = directive.TargetToken.IsKind(SyntaxKind.None)
                || directive.TargetToken.IsKind(SyntaxKind.AnnotationsKeyword);
            var movesWarnings = directive.TargetToken.IsKind(SyntaxKind.None)
                || directive.TargetToken.IsKind(SyntaxKind.WarningsKeyword);

            if ((movesAnnotations && annotations != target) || (movesWarnings && warnings != target)) {
                if (movesAnnotations) {
                    annotations = target;
                }

                if (movesWarnings) {
                    warnings = target;
                }

                continue;
            }

            if (!IsPlain(directive)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    directive.GetLocation(),
                    FixEdits.Pack((directive.FullSpan, string.Empty)),
                    target == Setting.Inherited
                        ? "nothing has changed the nullable state, so `#nullable restore` restores the project "
                        + "default the file already had"
                        : "the nullable state this directive sets is the one already in effect"
                )
            );
        }
    }

    static Setting Target(NullableDirectiveTriviaSyntax directive) =>
        directive.SettingToken.Kind() switch {
            SyntaxKind.EnableKeyword => Setting.Enabled,
            SyntaxKind.DisableKeyword => Setting.Disabled,
            _ => Setting.Inherited
        };

    /// <summary>
    ///     ⚠ A comment on the directive withdraws the finding, because the fix deletes the whole line.
    /// </summary>
    /// <remarks>
    ///     <c>#nullable disable // the generated half of this file is not annotated yet</c> is the
    ///     author explaining a migration, and a directive that is redundant today is exactly the kind
    ///     whose note says why it is there.
    /// </remarks>
    static bool IsPlain(NullableDirectiveTriviaSyntax directive) {
        foreach (var trivia in directive.DescendantTrivia(descendIntoTrivia: true)) {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)) {
                return false;
            }
        }

        return true;
    }
}
