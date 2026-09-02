using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2220</c> — a call the preprocessor let through to a method the same symbol tells the
///     compiler to delete, so the statement runs in no build at all.
/// </summary>
/// <remarks>
///     <c>[Conditional("DEBUG")]</c> deletes the call site in every compilation that does not define
///     <c>DEBUG</c>. Writing that call inside <c>#if !DEBUG</c> therefore produces code that executes
///     in <b>no</b> configuration: where <c>DEBUG</c> is undefined the directive admits the statement
///     and the attribute deletes it, and where <c>DEBUG</c> is defined the directive excludes the
///     statement before the compiler ever sees it. The two mechanisms cancel, and the author who wrote
///     both plainly meant the call to happen somewhere.
///     <para>
///         ⚠
///         <b>
///             The redundant shape is deliberately not this rule, and the reason is measured rather
///             than argued.
///         </b> The sibling shape — <c>[Conditional("DEBUG")]</c> called inside
///         <c>#if DEBUG</c> — is belt and braces: the guard duplicates what the attribute already does
///         and nothing is broken. It is also <em>unobservable</em> from any compilation that does not
///         define the symbol, because the region is disabled text and holds no invocation node to
///         analyse. Only one of the two shapes is visible in any one compilation, and the visible one
///         is whichever the symbol's absence selects. This rule takes the shape that is both a defect
///         and provable: the branch the preprocessor took is the branch that proves the symbol is
///         undefined, so the attribute's deletion is a fact about this compilation rather than a guess
///         about another.
///     </para>
///     <para>
///         ⚠ <b>Every one of the method's <c>[Conditional]</c> symbols must be proved undefined.</b>
///         The attribute is additive: a method carrying <c>[Conditional("DEBUG")]</c> and
///         <c>[Conditional("TRACE")]</c> survives when <em>either</em> symbol is defined, so proving
///         one of them absent proves nothing about the call. A rule that matched the first attribute
///         and stopped would report a live call as dead, which is the worst thing a correctness rule
///         can say.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeadConditionalCallAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.DeadConditionalCall);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                // ⚠ Without `ConditionalAttribute` nothing in the compilation can be compiled out, so
                // the rule withdraws rather than resolving a symbol per invocation.
                var conditional = start.Compilation.GetTypeByMetadataName("System.Diagnostics.ConditionalAttribute");
                if (conditional is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, conditional),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol conditional) {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // ⚠ Only a statement whose whole content is the call. The fix deletes the statement, and a
        // call nested inside a larger expression has no deletion that leaves the rest standing.
        // A `[Conditional]` method returns `void`, so this is the shape such a call almost always has.
        if (invocation.Parent is not ExpressionStatementSyntax statement || statement.Expression != invocation) {
            return;
        }

        if (context.SemanticModel.GetOperation(
                invocation,
                context.CancellationToken
            ) is not IInvocationOperation call) {
            return;
        }

        var symbols = ConditionalSymbols(call.TargetMethod, conditional);
        if (symbols.Count == 0) {
            return;
        }

        var undefined = UndefinedSymbols(invocation);
        if (undefined.Count == 0 || !symbols.All(undefined.Contains)) {
            return;
        }

        var source = context.Node.SyntaxTree.GetText(context.CancellationToken);

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                statement.GetLocation(),
                FixEdits.Pack((Lines(source, statement), string.Empty)),
                "`"
                + call.TargetMethod.ContainingType.Name
                + "."
                + call.TargetMethod.Name
                + "` is `[Conditional(\""
                + string.Join("\", \"", symbols.OrderBy(static s => s, System.StringComparer.Ordinal))
                + "\")]`, and the directive that admits this statement proves the symbol is not defined, "
                + "so the compiler deletes the call — while the build that defines the symbol never sees "
                + "the statement at all"
            )
        );
    }

    /// <summary>Every symbol a <c>[Conditional]</c> on the method or on what it overrides names.</summary>
    /// <remarks>
    ///     ⚠ The attribute is read from the method and from the definitions it overrides, because
    ///     <c>[Conditional]</c> is inherited by an override and reading only the immediate symbol
    ///     would miss it. An attribute whose argument is not a constant string is returned as
    ///     <c>null</c> in the list's place and withdraws the whole call, because an unknown symbol
    ///     cannot be proved absent.
    /// </remarks>
    static List<string> ConditionalSymbols(IMethodSymbol method, INamedTypeSymbol conditional) {
        var result = new List<string>();
        for (var current = method; current is not null; current = current.OverriddenMethod) {
            foreach (var attribute in current.GetAttributes()) {
                if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, conditional)) {
                    continue;
                }

                if (attribute.ConstructorArguments.Length != 1
                    || attribute.ConstructorArguments[0].Value is not string symbol
                    || symbol.Length == 0) {
                    // An argument this cannot read is a symbol this cannot prove undefined.
                    return [];
                }

                result.Add(symbol);
            }
        }

        return result;
    }

    /// <summary>
    ///     The preprocessor symbols the directives enclosing this node prove are <em>not</em> defined.
    /// </summary>
    /// <remarks>
    ///     ⚠ The evidence is the branch the preprocessor <em>took</em>, never the compilation's symbol
    ///     list. Reading <c>PreprocessorSymbolNames</c> instead would report every <c>Debug.Assert</c>
    ///     in every release build, which is <c>[Conditional]</c> working exactly as designed and is not
    ///     a defect at all. What makes this one a defect is that somebody wrote a guard saying they
    ///     expected the call to run here.
    ///     <para>
    ///         Two directive shapes are read and no others. <c>#if !X</c> whose branch was taken proves
    ///         <c>X</c> undefined. The <c>#else</c> of a plain <c>#if X</c> proves the same thing.
    ///         <c>#elif</c> chains, <c>&amp;&amp;</c>, <c>||</c>, parentheses and comparisons against
    ///         <c>true</c> are all declined: each of them can be true for reasons that say nothing
    ///         about one symbol, and a wrong answer here reports live code as dead.
    ///     </para>
    /// </remarks>
    static HashSet<string> UndefinedSymbols(SyntaxNode node) {
        var result = new HashSet<string>(System.StringComparer.Ordinal);
        var position = node.SpanStart;

        foreach (var directive in node.SyntaxTree.GetRoot()
                     .DescendantNodes(descendIntoTrivia: true)
                     .OfType<BranchingDirectiveTriviaSyntax>()) {
            if (!directive.BranchTaken || !Encloses(directive, position)) {
                continue;
            }

            switch (directive) {
                // `#if !X` — the branch was taken, so `X` is not defined.
                case IfDirectiveTriviaSyntax conditionalDirective
                    when conditionalDirective.Condition is PrefixUnaryExpressionSyntax {
                        RawKind: (int)SyntaxKind.LogicalNotExpression,
                        Operand: IdentifierNameSyntax negated
                    }:
                    result.Add(negated.Identifier.ValueText);
                    break;

                // `#if X` … `#else` — this branch was taken, so `X` is not defined. Only a two-part
                // chain: an `#elif` between them means the `#else` proves several symbols absent at
                // once and the shape is no longer the one being read.
                case ElseDirectiveTriviaSyntax elseDirective: {
                    var chain = elseDirective.GetRelatedDirectives();
                    if (chain.Count == 3
                        && chain[0] is IfDirectiveTriviaSyntax { Condition: IdentifierNameSyntax plain }) {
                        result.Add(plain.Identifier.ValueText);
                    }

                    break;
                }
            }
        }

        return result;
    }

    /// <summary>Whether a taken branch's body contains the position.</summary>
    /// <remarks>
    ///     The body runs from the end of this directive to the start of the next directive in its own
    ///     chain, which is what bounds the branch whatever the chain's shape turns out to be.
    /// </remarks>
    static bool Encloses(DirectiveTriviaSyntax directive, int position) {
        var chain = directive.GetRelatedDirectives();
        var index = chain.IndexOf(directive);
        if (index < 0 || index + 1 >= chain.Count) {
            return false;
        }

        return TextSpan.FromBounds(directive.FullSpan.End, chain[index + 1].FullSpan.Start).Contains(position);
    }

    /// <summary>The whole lines a statement occupies, so deleting it leaves no blank residue.</summary>
    static TextSpan Lines(SourceText source, SyntaxNode node) =>
        TextSpan.FromBounds(
            source.Lines.GetLineFromPosition(node.SpanStart).Start,
            source.Lines.GetLineFromPosition(node.Span.End).EndIncludingLineBreak
        );
}
