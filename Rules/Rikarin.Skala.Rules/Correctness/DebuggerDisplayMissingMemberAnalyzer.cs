using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2102</c> — a <c>[DebuggerDisplay]</c> string names a member the type does not have.
/// </summary>
/// <remarks>
///     The expression is bound by the debugger at inspection time, so a rename breaks it silently and
///     it is found while debugging something else, at the moment it is least welcome. It is the same
///     string-to-member binding <c>nameof</c> exists to remove.
///     <para>
///         ⚠ <b>The text inside the braces is a limited expression language, not a member name</b>, and a
///         parser that guesses at it produces false positives on correct code. This one reports only
///         what it can prove, and the accepted grammar is deliberately tiny: a single identifier, with
///         an optional <c>this.</c> in front and an optional <c>,nq</c>-style format specifier behind.
///         Everything else — a call, an index, an operator, a literal, a further <c>.</c>, an escape, a
///         nested brace, an unbalanced brace — <b>withdraws the whole attribute</b>.
///     </para>
///     <para>
///         ⚠ The dotted path is the exclusion that matters most. <c>{Owner.Name}</c> would need the
///         member's type to answer, and <c>{DateTime.Now}</c> has a root that is not a member of
///         anything — reporting either would be wrong for a different reason each time.
///     </para>
///     <para>
///         ⚠ Only a type declaration is examined. <c>DebuggerDisplay</c> also targets a field, a property
///         and an assembly, and on those the expression binds against something other than the annotated
///         declaration; there is nothing to prove without deciding which.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DebuggerDisplayMissingMemberAnalyzer : DiagnosticAnalyzer {
    const string DebuggerDisplayAttribute = "System.Diagnostics.DebuggerDisplayAttribute";

    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.DebuggerDisplayMissingMember);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } type) {
            return;
        }

        foreach (var list in declaration.AttributeLists) {
            foreach (var attribute in list.Attributes) {
                var attributeType = AttributeContract.Resolve(
                    context.SemanticModel,
                    attribute,
                    context.CancellationToken
                );

                if (attributeType is null
                    || !string.Equals(
                        AttributeContract.NameOf(attributeType),
                        DebuggerDisplayAttribute,
                        StringComparison.Ordinal
                    )
                    || attribute.ArgumentList is null) {
                    continue;
                }

                foreach (var argument in attribute.ArgumentList.Arguments) {
                    Check(context, type, argument);
                }
            }
        }
    }

    static void Check(SyntaxNodeAnalysisContext context, INamedTypeSymbol type, AttributeArgumentSyntax argument) {
        // The positional value, and the `Name =` / `Type =` strings, all carry the same grammar.
        if (argument.NameEquals is { } name
            && name.Name.Identifier.ValueText is not ("Name" or "Type")) {
            return;
        }

        if (context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken)
            is not { HasValue: true, Value: string format }) {
            return;
        }

        var roots = Roots(format);
        if (roots is null) {
            return;
        }

        foreach (var root in roots) {
            if (AttributeContract.HasMemberNamed(type, root) is not false) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    argument.GetLocation(),
                    "`" + root + "` is not a member of `" + type.Name + "`, so the debugger cannot bind it"
                )
            );
        }
    }

    /// <summary>
    ///     Every root identifier the format string names, or null when any part of it was not provable.
    /// </summary>
    /// <remarks>
    ///     ⚠ Null is returned for the <em>whole</em> string rather than skipping the hole that could not
    ///     be parsed. A string this parser only half understands is a string it has no standing to
    ///     report on, and reporting the half it did understand is how a conservative parser stops being
    ///     one.
    /// </remarks>
    internal static List<string>? Roots(string format) {
        var roots = new List<string>();
        for (var i = 0; i < format.Length; i++) {
            if (format[i] == '}') {
                // A closing brace with no opener. The runtime's own parser is undocumented here.
                return null;
            }

            if (format[i] != '{') {
                continue;
            }

            var close = format.IndexOf('}', i + 1);
            if (close < 0) {
                return null;
            }

            var inner = format.Substring(i + 1, close - i - 1);
            if (inner.IndexOf('{') >= 0) {
                return null;
            }

            var root = Root(inner);
            if (root is null) {
                return null;
            }

            roots.Add(root);
            i = close;
        }

        return roots;
    }

    static string? Root(string inner) {
        // `,nq`, `,raw`, `,ac` and the rest: letters only, at the very end, or the comma is one this
        // parser cannot account for and the string is withdrawn.
        var comma = inner.LastIndexOf(',');
        if (comma >= 0) {
            var specifier = inner.Substring(comma + 1).Trim();
            if (specifier.Length == 0) {
                return null;
            }

            foreach (var c in specifier) {
                if (!char.IsLetter(c)) {
                    return null;
                }
            }

            inner = inner.Substring(0, comma);
        }

        var expression = inner.Trim();
        const string self = "this.";
        if (expression.StartsWith(self, StringComparison.Ordinal)) {
            expression = expression.Substring(self.Length).Trim();
        }

        return IsIdentifier(expression) ? expression : null;
    }

    static bool IsIdentifier(string text) {
        if (text.Length == 0 || !(char.IsLetter(text[0]) || text[0] == '_')) {
            return false;
        }

        foreach (var c in text) {
            if (!char.IsLetterOrDigit(c) && c != '_') {
                return false;
            }
        }

        return true;
    }
}
