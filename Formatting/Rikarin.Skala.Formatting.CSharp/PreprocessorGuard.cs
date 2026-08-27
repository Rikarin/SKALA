using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Diagnostics;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
///     Finds members whose braces are split across a preprocessor branch.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/04 § "Trivia": a construct written
///     <c>#if X</c> … <c>{</c> … <c>#else</c> … <c>{</c> … has a brace structure that depends on which
///     branch is active. Roslyn parses one of them and hands back a tree that describes only that one.
///     Reindenting from it moves braces the other branch owns. Skala emits the whole member verbatim
///     and says so (<c>SK9011</c>, info). Silently doing something clever here is how formatters
///     destroy code.
/// </remarks>
public static class PreprocessorGuard {
    public static void MarkUnbalancedMembers(
        SyntaxNode root,
        SourceText text,
        HashSet<int> verbatimMembers,
        List<SkalaDiagnostic> diagnostics,
        string path
    ) {
        var directives = new List<DirectiveTriviaSyntax>();
        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true)) {
            if (trivia.GetStructure() is DirectiveTriviaSyntax directive && IsConditional(directive)) {
                directives.Add(directive);
            }
        }

        if (directives.Count == 0) {
            return;
        }

        var source = text.ToString();
        var index = 0;
        while (index < directives.Count) {
            if (directives[index].IsKind(SyntaxKind.IfDirectiveTrivia)) {
                index = Evaluate(root, source, directives, index, verbatimMembers, diagnostics, path, text).Next;
            } else {
                index++;
            }
        }
    }

    static bool IsConditional(DirectiveTriviaSyntax directive) =>
        directive.Kind() is
        SyntaxKind.IfDirectiveTrivia
            or SyntaxKind.ElifDirectiveTrivia
            or SyntaxKind.ElseDirectiveTrivia
            or SyntaxKind.EndIfDirectiveTrivia;

    /// <summary>
    ///     Walks one <c>#if … #endif</c> group and reports whether its branches agree on how many
    ///     braces they open.
    /// </summary>
    /// <remarks>
    ///     ⚠ Recursive, and it has to be. A branch may contain a whole nested group, and counting
    ///     braces lexically across a nested group counts <em>all</em> of that group's branches at once —
    ///     which is how an ordinary, perfectly balanced file gets reported as unbalanced.
    /// </remarks>
    static (int Next, int Delta, bool Balanced) Evaluate(
        SyntaxNode root,
        string source,
        List<DirectiveTriviaSyntax> directives,
        int start,
        HashSet<int> verbatimMembers,
        List<SkalaDiagnostic> diagnostics,
        string path,
        SourceText text
    ) {
        var index = start + 1;
        var cursor = directives[start].FullSpan.End;
        var branch = 0;
        int? agreed = null;
        var balanced = true;
        var last = start;

        while (index < directives.Count) {
            var directive = directives[index];

            if (directive.IsKind(SyntaxKind.IfDirectiveTrivia)) {
                var nested = Evaluate(root, source, directives, index, verbatimMembers, diagnostics, path, text);
                branch += BraceBalance(source, cursor, directives[index].SpanStart) + nested.Delta;
                balanced &= nested.Balanced;
                index = nested.Next;
                cursor = directives[nested.Next - 1].FullSpan.End;
                continue;
            }

            branch += BraceBalance(source, cursor, directive.SpanStart);

            if (directive.IsKind(SyntaxKind.EndIfDirectiveTrivia)) {
                last = index;
                agreed ??= branch;
                // ⚠ Two conditions, and the second is the one docs/plan/04 is about: the branches
                // must agree, AND each must be self-contained. A branch that opens a brace the
                // #endif does not close is a member whose braces are split across the directive —
                // the tree Roslyn produced describes one branch's brace structure and the others'
                // is different.
                balanced &= agreed == branch && branch == 0;
                index++;
                break;
            }

            // #elif or #else closes this branch and opens the next.
            agreed ??= branch;
            balanced &= agreed == branch && branch == 0;
            branch = 0;
            cursor = directive.FullSpan.End;
            index++;
        }

        if (!balanced) {
            Report(root, directives[start], directives[last], verbatimMembers, diagnostics, path, text);
        }

        return (index, agreed ?? 0, balanced);
    }

    static void Report(
        SyntaxNode root,
        DirectiveTriviaSyntax first,
        DirectiveTriviaSyntax last,
        HashSet<int> verbatimMembers,
        List<SkalaDiagnostic> diagnostics,
        string path,
        SourceText text
    ) {
        var span = TextSpan.FromBounds(first.SpanStart, last.Span.End);
        var member = FindEnclosingMember(root, span);
        if (member is not null) {
            verbatimMembers.Add(member.SpanStart);
        }

        var line = text.Lines.GetLineFromPosition(span.Start).LineNumber + 1;
        diagnostics.Add(
            new SkalaDiagnostic(
                FormatDiagnosticIds.UnbalancedPreprocessor,
                SkalaSeverity.Info,
                member is null
                    ? "not formatted, unbalanced preprocessor structure"
                    : $"'{Describe(member)}' is not formatted, unbalanced preprocessor structure",
                path,
                line,
                "A branch of this #if opens or closes a brace the other branches do not. Reindenting from the tree Roslyn produced for one branch would move braces the others own."
            )
        );
    }

    static string Describe(SyntaxNode member) =>
        member switch {
            BaseTypeDeclarationSyntax type => type.Identifier.Text,
            MethodDeclarationSyntax method => method.Identifier.Text,
            PropertyDeclarationSyntax property => property.Identifier.Text,
            ConstructorDeclarationSyntax constructor => constructor.Identifier.Text,
            _ => member.Kind().ToString()
        };

    static SyntaxNode? FindEnclosingMember(SyntaxNode root, TextSpan span) {
        if (!root.FullSpan.Contains(span)) {
            return null;
        }

        var node = root.FindNode(span, findInsideTrivia: false, getInnermostNodeForTie: false);
        for (; node is not null; node = node.Parent) {
            if (node is MemberDeclarationSyntax and not BaseNamespaceDeclarationSyntax) {
                return node;
            }
        }

        return null;
    }

    /// <summary>
    ///     Net <c>{</c> minus <c>}</c> in a stretch of raw source, skipping string and character
    ///     literals and comments. Deliberately lexical: a disabled branch is unstructured text and
    ///     there is no tree to ask.
    /// </summary>
    static int BraceBalance(string source, int start, int end) {
        var balance = 0;
        var i = start;
        while (i < end) {
            var c = source[i];
            switch (c) {
                case '{':
                    balance++;
                    i++;
                    continue;

                case '}':
                    balance--;
                    i++;
                    continue;

                case '/' when i + 1 < end && source[i + 1] == '/':
                    while (i < end && source[i] != '\n') {
                        i++;
                    }

                    continue;

                case '/' when i + 1 < end && source[i + 1] == '*':
                    i += 2;
                    while (i + 1 < end && !(source[i] == '*' && source[i + 1] == '/')) {
                        i++;
                    }

                    i = Math.Min(end, i + 2);
                    continue;

                case '"':
                case '\'':
                    i = SkipLiteral(source, i, end);
                    continue;

                case '@' when i + 1 < end && source[i + 1] == '"':
                    i = SkipVerbatimString(source, i + 1, end);
                    continue;

                default:
                    i++;
                    continue;
            }
        }

        return balance;
    }

    static int SkipLiteral(string source, int i, int end) {
        var quote = source[i];
        i++;
        while (i < end) {
            if (source[i] == '\\') {
                i += 2;
                continue;
            }

            if (source[i] == quote || source[i] == '\n') {
                return i + 1;
            }

            i++;
        }

        return end;
    }

    static int SkipVerbatimString(string source, int i, int end) {
        i++;
        while (i < end) {
            if (source[i] == '"') {
                if (i + 1 < end && source[i + 1] == '"') {
                    i += 2;
                    continue;
                }

                return i + 1;
            }

            i++;
        }

        return end;
    }
}
