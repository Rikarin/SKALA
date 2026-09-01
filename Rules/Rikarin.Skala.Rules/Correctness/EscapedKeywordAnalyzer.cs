using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2034</c> — a declaration named after a reserved keyword, so it can only be written
///     <c>@class</c>.
/// </summary>
/// <remarks>
///     Legal, and a stop for every reader: an <c>@</c> in front of a word is a verbatim string prefix,
///     an identifier escape or a typo, and telling which requires looking.
///     <para>
///         ⚠ <b>Reserved keywords only, and that is the whole safety argument.</b> Every escape a
///         language feature can <em>require</em> is on a <b>contextual</b> keyword —
///         <c>@field</c> once <c>field</c> became the backing-field keyword (SK1003's territory),
///         <c>@value</c> in an accessor, <c>@extension</c> in the extension-block work, <c>@record</c>,
///         <c>@var</c>, <c>@await</c>. A contextual keyword is a legal identifier <em>without</em> the
///         escape, so an escape on one is disambiguation the author had no choice about. A reserved
///         keyword is the opposite: the escape is not a disambiguation at all, it is the only spelling
///         the chosen name has, and the finding is about the name.
///     </para>
///     <para>
///         ⚠ <b>Declarations only, never references.</b> A name that comes from another assembly must be
///         escaped at every use site and nobody in this repository can rename it, so reporting uses
///         would report something no one can fix — and it would multiply one defect by its call count.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EscapedKeywordAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.EscapedKeyword);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(Analyze);
    }

    static void Analyze(SyntaxTreeAnalysisContext context) {
        var root = context.Tree.GetRoot(context.CancellationToken);
        foreach (var token in root.DescendantTokens()) {
            if (!token.IsKind(SyntaxKind.IdentifierToken)
                || token.Text.Length == 0
                || token.Text[0] != '@'
                || SyntaxFacts.GetKeywordKind(token.ValueText) == SyntaxKind.None
                || !IsDeclaringIdentifier(token)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    token.GetLocation(),
                    "`"
                    + token.Text
                    + "` names a declaration after the reserved keyword `"
                    + token.ValueText
                    + "`; rename it so the escape is not needed"
                )
            );
        }
    }

    /// <summary>
    ///     Whether this token is the name a declaration introduces, rather than a mention of one.
    /// </summary>
    /// <remarks>
    ///     ⚠ Constructors and destructors are absent on purpose: their identifier repeats the type's
    ///     name, which the type declaration already reports, and counting both would report one naming
    ///     decision twice. Namespace name parts and query continuations are absent because neither has
    ///     been thought through, and a rule that guesses at a shape is how a syntactic rule acquires its
    ///     first false positive.
    /// </remarks>
    static bool IsDeclaringIdentifier(SyntaxToken token) =>
        token.Parent switch {
            BaseTypeDeclarationSyntax node => node.Identifier == token,
            DelegateDeclarationSyntax node => node.Identifier == token,
            EnumMemberDeclarationSyntax node => node.Identifier == token,
            MethodDeclarationSyntax node => node.Identifier == token,
            PropertyDeclarationSyntax node => node.Identifier == token,
            EventDeclarationSyntax node => node.Identifier == token,
            VariableDeclaratorSyntax node => node.Identifier == token,
            ParameterSyntax node => node.Identifier == token,
            TypeParameterSyntax node => node.Identifier == token,
            LocalFunctionStatementSyntax node => node.Identifier == token,
            ForEachStatementSyntax node => node.Identifier == token,
            CatchDeclarationSyntax node => node.Identifier == token,
            SingleVariableDesignationSyntax node => node.Identifier == token,
            LabeledStatementSyntax node => node.Identifier == token,
            TupleElementSyntax node => node.Identifier == token,
            ExternAliasDirectiveSyntax node => node.Identifier == token,
            FromClauseSyntax node => node.Identifier == token,
            LetClauseSyntax node => node.Identifier == token,
            JoinClauseSyntax node => node.Identifier == token,
            JoinIntoClauseSyntax node => node.Identifier == token,

            // A `using X = …` alias and an anonymous-object member name, both spelled `NameEquals`.
            IdentifierNameSyntax node => node.Parent is NameEqualsSyntax,
            _ => false
        };
}
