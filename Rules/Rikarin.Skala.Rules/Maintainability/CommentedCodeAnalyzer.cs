using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>SK7060: bounded, standalone comment blocks that parse as multiple executable statements.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CommentedCodeAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CommentedCode);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CompilationUnit);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        var lines = new List<string>();
        var start = 0;
        var end = 0;
        var previousLine = -2;
        var length = 0;
        var oversized = false;
        foreach (var trivia in context.Node.DescendantTrivia()) {
            if (trivia.ContainsDiagnostics) {
                Flush();
                continue;
            }

            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && Standalone(trivia, text, false)) {
                var line = text.Lines.GetLineFromPosition(trivia.SpanStart).LineNumber;
                if (line != previousLine + 1) {
                    Flush();
                    start = trivia.SpanStart;
                }

                previousLine = line;
                end = trivia.Span.End;
                length += trivia.Span.Length;
                oversized |= length > 8192 || lines.Count >= 100;
                if (!oversized) {
                    lines.Add(trivia.ToString().Substring(2));
                }
            } else if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)) {
                Flush();
                if (trivia.Span.Length > 8192 || !Standalone(trivia, text, true)) {
                    continue;
                }

                var raw = trivia.ToString();
                var body = raw.Substring(2, raw.Length - 4);
                var stripped = string.Join(
                    "\n",
                    body.Split('\n')
                        .Select(static line => {
                                var trimmed = line.TrimStart();
                                return trimmed.StartsWith("*", StringComparison.Ordinal) ? trimmed.Substring(1) : line;
                            }
                        )
                );
                Report(stripped, trivia.Span);
            }
        }

        Flush();
        return;

        void Flush() {
            if (lines.Count > 0 && !oversized) {
                Report(string.Join("\n", lines), TextSpan.FromBounds(start, end));
            }

            lines.Clear();
            length = 0;
            oversized = false;
            previousLine = -2;
        }

        void Report(string source, TextSpan span) {
            if (LooksLikeCode(source, (CSharpParseOptions)context.Node.SyntaxTree.Options)) {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        Location.Create(context.Node.SyntaxTree, span),
                        "This standalone comment block looks like disabled C# statements; remove it or explain why it is retained"
                    )
                );
            }
        }
    }

    static bool Standalone(SyntaxTrivia trivia, SourceText text, bool checkEnd) {
        var line = text.Lines.GetLineFromPosition(trivia.SpanStart);
        if (!text.ToString(TextSpan.FromBounds(line.Start, trivia.SpanStart)).All(char.IsWhiteSpace)) {
            return false;
        }

        return !checkEnd
            || text.ToString(
                TextSpan.FromBounds(
                    trivia.Span.End,
                    text.Lines.GetLineFromPosition(trivia.Span.End).End
                )
            )
                .All(char.IsWhiteSpace);
    }

    static bool LooksLikeCode(string source, CSharpParseOptions options) {
        if (source.IndexOf('`') >= 0
            || source.IndexOf("<code", StringComparison.OrdinalIgnoreCase) >= 0
            || source.IndexOf("http", StringComparison.OrdinalIgnoreCase) >= 0
            || source.Count(static character => character is '(' or '[' or '{') > 128) {
            return false;
        }

        var parsed = SyntaxFactory.ParseStatement("{" + source + "\n}", options: options, consumeFullText: true);
        if (parsed is not BlockSyntax block
            || block.ContainsDiagnostics
            || block.ContainsDirectives
            || block.DescendantTrivia()
                .Any(static trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                )) {
            return false;
        }

        var atomic = 0;
        foreach (var statement in block.DescendantNodes().OfType<StatementSyntax>()) {
            switch (statement) {
                case ExpressionStatementSyntax {
                    Expression: InvocationExpressionSyntax or AssignmentExpressionSyntax or AwaitExpressionSyntax
                }:
                case ReturnStatementSyntax { Expression: not null }:
                case ThrowStatementSyntax { Expression: not null }:
                    atomic++;
                    break;
                case LocalDeclarationStatementSyntax local when local.Declaration.Variables.All(static variable => variable.Initializer is not null
                ):
                    atomic++;
                    break;
                case BlockSyntax
                    or IfStatementSyntax
                    or ForStatementSyntax
                    or CommonForEachStatementSyntax
                    or WhileStatementSyntax
                    or DoStatementSyntax
                    or LockStatementSyntax
                    or UsingStatementSyntax
                    or TryStatementSyntax
                    or SwitchStatementSyntax
                    or BreakStatementSyntax
                    or ContinueStatementSyntax:
                    break;
                default:
                    return false;
            }
        }

        var tokens = block.DescendantTokens().ToArray();
        var punctuation = tokens.Count(static token => token.IsKind(SyntaxKind.SemicolonToken)
            || token.IsKind(SyntaxKind.OpenParenToken)
            || token.IsKind(SyntaxKind.CloseParenToken)
            || token.IsKind(SyntaxKind.EqualsToken)
        );
        return atomic >= 2 && tokens.Length >= 10 && punctuation * 4 >= tokens.Length;
    }
}
