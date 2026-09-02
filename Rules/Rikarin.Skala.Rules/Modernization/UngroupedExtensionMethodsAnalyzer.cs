using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1004</c> — a static class whose every member is an extension method on one receiver, which
///     is the pre-C# 14 spelling of a single <c>extension</c> block.
/// </summary>
/// <remarks>
///     ⚠ <b>The syntax was confirmed to compile on the pinned toolchain before this rule was
///     written, because a modernization rule that emits syntax the compiler rejects is worse than no
///     rule.</b> On SDK 10.0.400 with <c>LangVersion 14.0</c>, an <c>extension(string s) { … }</c>
///     block builds with no diagnostic, and on the pinned Roslyn it parses to
///     <c>ExtensionBlockDeclarationSyntax</c> at <c>CSharp14</c> and at <c>Preview</c> alike. ⚠ At
///     <c>CSharp13</c> the same text does not report the feature — it <em>recovers as a constructor
///     named <c>extension</c></em> and then fails with <c>CS1513</c>, so "the fixture did not
///     compile" would have been the only symptom of a missing language floor. The floor is declared
///     in <c>rules.json</c> and <see cref="SkalaRule.MeetsLanguageVersion" /> enforces it.
///     <para>
///         ⚠ <b>Both call forms survive the rewrite, measured rather than assumed.</b> Against a
///         block, <c>"x".Repeat(2)</c> compiles and so does <c>StringExt.Repeat("x", 2)</c> — C# 14
///         keeps the static-invocation form of an extension block member reachable through the
///         enclosing class. That is what makes this a source-compatible change and why the rule does
///         not have to hunt for call sites. It is <em>not</em> binary-compatible, which is why the fix
///         is unsafe.
///     </para>
///     <para>
///         ⚠ <b>One block or nothing.</b> The rule fires only when every member is an extension method
///         on the same receiver type with the same receiver name. Anything else — two receiver types,
///         a plain static mixed in, a differing receiver name — is a conversion that has to move code
///         or rename an identifier, and the whole reason this fix is reviewable is that its edits are
///         subtractive plus two braces.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UngroupedExtensionMethodsAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.UngroupedExtensionMethods);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UngroupedExtensionMethods);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (ClassDeclarationSyntax)context.Node;

        // Only a non-generic, non-partial static class can hold the idiom at all, and `partial`
        // hides the other half of the member list from this analyzer.
        if (!declaration.Modifiers.Any(SyntaxKind.StaticKeyword)
            || declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            || declaration.TypeParameterList is not null) {
            return;
        }

        // ⚠ The block's two braces go in at fixed points. A directive can leave one of them inside
        // a branch the other is not in, which is the same reason SK1005 refuses the conversion.
        if (declaration.ContainsDirectives) {
            return;
        }

        // Two members, because the value of the block is saying the receiver once instead of twice.
        if (declaration.Members.Count < 2) {
            return;
        }

        var methods = new List<MethodDeclarationSyntax>(declaration.Members.Count);
        foreach (var member in declaration.Members) {
            // ⚠ Every member, not most of them. A field, a nested type or an already-converted
            // `extension` block means the block would have to wrap a subset — and a subset need not
            // be contiguous, so there is no pair of insertion points that expresses it.
            if (member is not MethodDeclarationSyntax method || !IsConvertibleExtensionMethod(method)) {
                return;
            }

            methods.Add(method);
        }

        var first = methods[0].ParameterList.Parameters[0];
        var receiverName = first.Identifier.ValueText;
        var receiverType = first.Type!.ToString();

        foreach (var method in methods) {
            var receiver = method.ParameterList.Parameters[0];

            // ⚠ Same type *as written* and same name. Merging `this string value` with
            // `this string s` means renaming an identifier inside a body, which is a rewrite this
            // fix has no way to bound — the name may be shadowed, captured, or spelled in a
            // `nameof`.
            if (!string.Equals(receiver.Identifier.ValueText, receiverName, System.StringComparison.Ordinal)
                || !string.Equals(receiver.Type!.ToString(), receiverType, System.StringComparison.Ordinal)) {
                return;
            }

            // The semantic half of the same question: the symbol really is an extension method, and
            // its receiver really is the one type. Text alone would accept two spellings of two
            // different types that happen to read alike under different usings.
            if (context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken)
                is not { IsExtensionMethod: true, Parameters.Length: > 0 } symbol
                || !SymbolEqualityComparer.Default.Equals(
                    symbol.Parameters[0].Type,
                    context.SemanticModel.GetDeclaredSymbol(methods[0], context.CancellationToken)?.Parameters[0].Type
                )) {
                return;
            }

            // ⚠ The receiver stops existing, so a `<param>` naming it would document nothing and the
            // build would gain a CS1572 on a public API — a fix that trades a suggestion for a
            // warning.
            if (DocumentsTheReceiver(method, receiverName)) {
                return;
            }
        }

        var edits = new List<(TextSpan Span, string Text)>((methods.Count * 2) + 2) {
            (
                TextSpan.FromBounds(declaration.OpenBraceToken.Span.End, declaration.OpenBraceToken.Span.End),
                "\nextension(" + receiverType + " " + receiverName + ") {"
            )
        };

        foreach (var method in methods) {
            if (!TryEdits(method, edits)) {
                return;
            }
        }

        edits.Add(
            (
                TextSpan.FromBounds(declaration.CloseBraceToken.SpanStart, declaration.CloseBraceToken.SpanStart),
                "}\n"
            )
        );

        // A comment inside any span the fix deletes is content, and the deleted spans are only
        // `static ` and the receiver parameter, so this is cheap to ask and rare to hit.
        foreach (var (span, text) in edits) {
            if (text.Length == 0
                && RewriteGuards.ContainsCommentOrDirective(declaration.SyntaxTree, span)) {
                return;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declaration.Identifier.GetLocation(),
                FixEdits.Pack(edits.ToArray()),
                "`"
                + declaration.Identifier.ValueText
                + "` is "
                + methods.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " extension methods on `"
                + receiverType
                + "`; one `extension("
                + receiverType
                + " "
                + receiverName
                + ")` block says the receiver once"
            )
        );
    }

    /// <summary>Whether one method is an extension method this rule is willing to move.</summary>
    static bool IsConvertibleExtensionMethod(MethodDeclarationSyntax method) {
        // ⚠ A method with its own type parameters needs `extension<T>(…)`, and the arity then has to
        // be reconciled across every member of the block. A class of `this IEnumerable<T>` helpers
        // is the common case and it is exactly the one this rule does not attempt.
        if (!method.Modifiers.Any(SyntaxKind.StaticKeyword) || method.TypeParameterList is not null) {
            return false;
        }

        if (method.ParameterList.Parameters.Count == 0) {
            return false;
        }

        var receiver = method.ParameterList.Parameters[0];
        if (receiver.Type is null || receiver.AttributeLists.Count > 0) {
            return false;
        }

        // `this` and nothing else. A `ref`, `in`, `ref readonly` or `scoped` receiver is state the
        // block's receiver declaration would have to reproduce, and the conversion stops being
        // subtractive.
        var sawThis = false;
        foreach (var modifier in receiver.Modifiers) {
            if (modifier.IsKind(SyntaxKind.ThisKeyword)) {
                sawThis = true;
                continue;
            }

            return false;
        }

        return sawThis;
    }

    /// <summary>Whether the method's documentation comment has a <c>&lt;param&gt;</c> for the receiver.</summary>
    static bool DocumentsTheReceiver(MethodDeclarationSyntax method, string receiverName) {
        foreach (var trivia in method.GetLeadingTrivia()) {
            if (trivia.GetStructure() is not DocumentationCommentTriviaSyntax documentation) {
                continue;
            }

            foreach (var node in documentation.DescendantNodes()) {
                var (name, attributes) = node switch {
                    XmlElementSyntax element => (
                        element.StartTag.Name.LocalName.ValueText,
                        element.StartTag.Attributes
                    ),
                    XmlEmptyElementSyntax empty => (empty.Name.LocalName.ValueText, empty.Attributes),
                    _ => (null, default)
                };

                if (!string.Equals(name, "param", System.StringComparison.Ordinal)) {
                    continue;
                }

                foreach (var attribute in attributes) {
                    if (attribute is XmlNameAttributeSyntax { } named
                        && string.Equals(
                            named.Identifier.Identifier.ValueText,
                            receiverName,
                            System.StringComparison.Ordinal
                        )) {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     The two deletions that turn one extension method into a block member: the <c>static</c>
    ///     modifier and the receiver parameter.
    /// </summary>
    static bool TryEdits(MethodDeclarationSyntax method, List<(TextSpan Span, string Text)> edits) {
        SyntaxToken? staticKeyword = null;
        foreach (var modifier in method.Modifiers) {
            if (modifier.IsKind(SyntaxKind.StaticKeyword)) {
                staticKeyword = modifier;
                break;
            }
        }

        if (staticKeyword is not { } keyword) {
            return false;
        }

        // From the keyword to the next token, so the space after `static` goes with it.
        edits.Add((TextSpan.FromBounds(keyword.SpanStart, keyword.GetNextToken().SpanStart), string.Empty));

        var parameters = method.ParameterList.Parameters;
        var receiver = parameters[0];

        // With a second parameter the separator and the space after it go too; alone, the receiver
        // is everything between the parentheses.
        edits.Add(
            (
                parameters.Count > 1
                    ? TextSpan.FromBounds(receiver.SpanStart, parameters[1].SpanStart)
                    : TextSpan.FromBounds(receiver.SpanStart, receiver.Span.End),
                string.Empty
            )
        );

        return true;
    }
}
