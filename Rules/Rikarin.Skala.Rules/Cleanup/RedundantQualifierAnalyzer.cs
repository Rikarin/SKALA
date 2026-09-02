using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary><c>SK0243</c> — a qualifier the shorter name would have resolved to anyway.</summary>
/// <remarks>
///     <para>
///         Two shapes: a namespace- or type-qualified type name where the simple name already binds to
///         the same type at that position, and a <c>base.</c> that reaches the same member as no
///         qualifier at all. <c>SK0207</c> owns the <c>this.</c> qualifier and <c>SK0215</c> the static
///         one; these are the two the arrangement rules do not cover.
///     </para>
///     <para>
///         ⚠ <b>Neither shape is decided by comparing text.</b> The type half asks the semantic model to
///         bind the short name <em>at the same position</em> and reports only when it produces the same
///         symbol, which is what makes a shadowing type, a missing using directive and an ambiguity all
///         answer for themselves. Deciding it by looking for a matching using directive would be a
///         different rule that is wrong whenever two namespaces both offer the name.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The <c>base.</c> half is not "the containing type does not override it", and believing
///             so is how this rule reports a behaviour change as a redundancy.
///         </b> Given
///         <c>class A { public virtual void M() { } }</c>, <c>class B : A</c> calling <c>base.M()</c>,
///         and <c>class C : B</c> overriding <c>M</c> — dropping the qualifier in <c>B</c> turns a
///         non-virtual call to <c>A.M</c> into a virtual one that reaches <c>C.M</c>. The member
///         therefore has to be one nothing can override further, or the containing type has to be
///         <c>sealed</c>.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Both halves used to ask a <em>node</em> whether it carried a comment, and that is the
///             defect #302 describes rather than a guard.
///         </b> <c>DescendantTrivia</c> on a node includes
///         the leading trivia of its first token, so a <c>//</c> or a <c>///</c> on the line
///         <em>above</em> — text no fix would touch — turned the rule off. Probed rather than read:
///         a positive fixture with a comment one line above the finding failed on both the qualified
///         name and the <c>base.</c> shape, and no other rule in the <c>SK023x</c> family failed the
///         same probe, because they ask the question of the <em>span</em> the fix deletes. So does
///         this one now.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantQualifierAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantQualifier);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeTypeName, SyntaxKind.QualifiedName);
        context.RegisterSyntaxNodeAction(AnalyzeBaseAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    static void AnalyzeTypeName(SyntaxNodeAnalysisContext context) {
        var qualified = (QualifiedNameSyntax)context.Node;

        // Only the outermost name: `A.B.C` is one finding and one edit, never three overlapping ones.
        if (qualified.Parent is QualifiedNameSyntax || !IsShortenable(qualified)) {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(qualified, context.CancellationToken).Symbol
            is not INamedTypeSymbol bound) {
            return;
        }

        var speculative = context.SemanticModel.GetSpeculativeSymbolInfo(
            qualified.SpanStart,
            SyntaxFactory.ParseTypeName(qualified.Right.ToString()),
            SpeculativeBindingOption.BindAsTypeOrNamespace
        );

        if (!speculative.CandidateSymbols.IsEmpty
            || !SymbolEqualityComparer.Default.Equals(speculative.Symbol, bound)
            || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(qualified.SyntaxTree, qualified.Span)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                qualified.GetLocation(),
                FixEdits.Pack((qualified.Span, qualified.Right.ToString())),
                "`" + qualified.Right + "` already binds to this type here, so the qualifier adds nothing"
            )
        );
    }

    /// <summary>
    ///     The positions and the shapes where shortening a qualified name is not this rule's business.
    /// </summary>
    /// <remarks>
    ///     ⚠ Every one of these is a place where the *written* name is load-bearing rather than a
    ///     spelling choice:
    ///     <list type="bullet">
    ///         <item>
    ///             A <c>using</c> directive and a namespace declaration are the qualified name — shortening
    ///             one changes what is imported or declared.
    ///         </item>
    ///         <item>
    ///             ⚠ An <em>attribute</em> name binds to a constructor rather than to a type, so the symbol
    ///             comparison below would compare the wrong things. The shape is real and it is left out
    ///             rather than answered with a check that does not mean what it says.
    ///         </item>
    ///         <item>
    ///             <c>global::</c> is a deliberate disambiguator. A short name that binds to the same symbol
    ///             today is exactly what somebody wrote <c>global::</c> to stop depending on.
    ///         </item>
    ///         <item>
    ///             A qualified name inside the type arguments would give the outer name and the inner one
    ///             two overlapping edits, and two overlapping edits are not a fix.
    ///         </item>
    ///         <item>A documentation <c>cref</c>, whose resolution rules are not the expression ones.</item>
    ///     </list>
    /// </remarks>
    static bool IsShortenable(QualifiedNameSyntax qualified) {
        foreach (var node in qualified.Ancestors()) {
            // ⚠ Any enclosing qualified name, not only the immediate parent. `List<System.String>`
            // nested inside `System.Collections.Generic.List<…>` would otherwise be shortened on one
            // pass and make the outer name newly reportable on the next, which is `skala fix` looping.
            if (node is QualifiedNameSyntax) {
                return false;
            }

            if (node is UsingDirectiveSyntax
                or BaseNamespaceDeclarationSyntax
                or AttributeSyntax
                or CrefSyntax
                or DocumentationCommentTriviaSyntax) {
                return false;
            }

            if (node is MemberDeclarationSyntax or StatementSyntax) {
                break;
            }
        }

        foreach (var node in qualified.DescendantNodes()) {
            if (node is AliasQualifiedNameSyntax) {
                return false;
            }
        }

        // The replacement text is the right-hand name as written, so a qualified name inside its type
        // arguments would be reported again on its own and produce a second, overlapping edit.
        foreach (var node in qualified.Right.DescendantNodes()) {
            if (node is QualifiedNameSyntax) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     <c>base.X</c> where <c>X</c> alone would reach the same member.
    /// </summary>
    /// <remarks>
    ///     ⚠ Three conditions, and the third is the one that is easy to miss. The containing type must
    ///     declare nothing of that name, so the qualifier is not what selects the base member over a
    ///     hiding one; nothing in scope at that position may be a local, a parameter or a local function
    ///     of that name, because an unqualified use would find it first; and the member must be one that
    ///     nothing can override further, or the containing type must be <c>sealed</c> — otherwise
    ///     dropping <c>base.</c> converts a non-virtual call into a virtual one that a class further
    ///     down the hierarchy can answer.
    /// </remarks>
    static void AnalyzeBaseAccess(SyntaxNodeAnalysisContext context) {
        var access = (MemberAccessExpressionSyntax)context.Node;
        var qualifier = TextSpan.FromBounds(access.SpanStart, access.Name.SpanStart);
        if (access.Expression is not BaseExpressionSyntax
            || access.Parent is MemberAccessExpressionSyntax { Expression: not BaseExpressionSyntax }
            || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(access.SyntaxTree, qualifier)
            || context.SemanticModel.GetSymbolInfo(access, context.CancellationToken).Symbol is not { } member) {
            return;
        }

        if (context.SemanticModel.GetEnclosingSymbol(access.SpanStart, context.CancellationToken)
                ?.ContainingType
            is not { } containing) {
            return;
        }

        var name = access.Name.Identifier.ValueText;
        if (!containing.GetMembers(name).IsEmpty) {
            return;
        }

        if (!containing.IsSealed && CanBeOverriddenFurther(member)) {
            return;
        }

        foreach (var candidate in context.SemanticModel.LookupSymbols(access.SpanStart, name: name)) {
            if (candidate is ILocalSymbol
                or IParameterSymbol
                or IRangeVariableSymbol
                or IMethodSymbol { MethodKind: MethodKind.LocalFunction }) {
                return;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                access.Expression.GetLocation(),
                FixEdits.Pack((qualifier, string.Empty)),
                "`base.` reaches the same member as no qualifier at all: nothing in this type hides `"
                + name
                + "` and nothing can override it"
            )
        );
    }

    static bool CanBeOverriddenFurther(ISymbol member) =>
        (member.IsVirtual || member.IsAbstract || member.IsOverride) && !member.IsSealed;
}
