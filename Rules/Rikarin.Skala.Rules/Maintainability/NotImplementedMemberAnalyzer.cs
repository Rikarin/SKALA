using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7090</c> — a thrown <c>NotImplementedException</c> with no issue reference.
/// </summary>
/// <remarks>
///     ⚠ This is <c>SK7040</c>'s requirement on the form that compiles, and it is on Skala's premise
///     rather than beside it. A model asked for an implementation produces a signature with a
///     <c>throw new NotImplementedException()</c> body far more readily than it says it cannot do the
///     work: the result type-checks, binds, formats, passes every analyzer and fails only when it runs.
///     A <c>TODO</c> at least announces itself to a reader; this announces itself to nobody.
///     <para>
///         The requirement is the same one <c>SK7040</c> makes of a <c>TODO</c> — somewhere a reader
///         will see it, name the issue that owns finishing this. The reference is accepted from the
///         exception's own message, from the throw's own comments, or from the enclosing member's
///         leading trivia, and it is the same vocabulary <c>SK7040</c> accepts: a URL, <c>#123</c>, or a
///         project key such as <c>SKALA-123</c>.
///     </para>
///     <para>
///         ⚠
///         <b>
///             <c>NotSupportedException</c> and <c>UnreachableException</c> never fire, and that is the
///             rule's position rather than an omission.
///         </b> Both are permanent statements about a contract —
///         an operation this type will never offer, a branch the author asserts cannot be reached — and
///         they are what an author writes when the answer really is "not here". <c>NotImplemented</c> is
///         the one that means "not yet", and "not yet" is what needs an owner.
///     </para>
///     <para>
///         Report-only. No edit writes the implementation, and there is no defensible mechanical
///         guess — deleting the member, returning <c>default</c> and swapping the exception type are
///         all worse than the throw, because each one turns a loud failure into a quiet one.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NotImplementedMemberAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NotImplementedMember);

    // ⚠ Deliberately the same vocabulary SK7040 accepts, character for character. Two rules asking
    // for "an issue reference" and disagreeing about what one looks like is a rule nobody can obey.
    static readonly Regex Issue = new(
        @"(?:https?://\S+|#\d+\b|\b[A-Z][A-Z0-9]{1,15}-\d+\b)",
        RegexOptions.Compiled
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var notImplemented = start.Compilation.GetTypeByMetadataName("System.NotImplementedException");
                if (notImplemented is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, notImplemented),
                    SyntaxKind.ObjectCreationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol notImplemented) {
        var creation = (ObjectCreationExpressionSyntax)context.Node;

        // ⚠ Only a construction that is *thrown*. Sonar's S3717 tracks every use of the type;
        // constructing one to inspect, to compare against or to hand to a test helper is not a
        // member that compiles and does not work, and reporting it would make the rule about the
        // type's name rather than about the unfinished member.
        if (creation.Parent is not (ThrowStatementSyntax or ThrowExpressionSyntax)) {
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type,
                notImplemented
            )) {
            return;
        }

        if (HasIssueReference(creation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                creation.GetLocation(),
                "the member compiles and does not work, and no issue reference says who owes the "
                + "implementation"
            )
        );
    }

    /// <summary>Anywhere a reader looking at the unfinished member would already be looking.</summary>
    static bool HasIssueReference(ObjectCreationExpressionSyntax creation) {
        if (creation.ArgumentList is { } arguments && Issue.IsMatch(arguments.ToString())) {
            return true;
        }

        // The throw's own statement, for `throw new NotImplementedException(); // #412`, and the
        // enclosing member, for a doc comment or a note written above the signature. An
        // expression-bodied member has no enclosing statement, which is why both are consulted.
        if (creation.FirstAncestorOrSelf<StatementSyntax>() is { } statement
            && (Mentions(statement.GetLeadingTrivia()) || Mentions(statement.GetTrailingTrivia()))) {
            return true;
        }

        return creation.FirstAncestorOrSelf<MemberDeclarationSyntax>() is { } member
            && (Mentions(member.GetLeadingTrivia()) || Mentions(member.GetTrailingTrivia()));
    }

    static bool Mentions(SyntaxTriviaList trivia) {
        foreach (var item in trivia) {
            if (IsComment(item) && Issue.IsMatch(item.ToFullString())) {
                return true;
            }
        }

        return false;
    }

    static bool IsComment(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
}
