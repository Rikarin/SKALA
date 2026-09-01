using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1063</c> — the interpolation that puts the value where it is printed.
/// </summary>
/// <remarks>
///     ⚠ <b>The unsound case is overload resolution, not formatting.</b> An interpolated string
///     converts to <c>string</c>, to <c>FormattableString</c> and to <c>IFormattable</c>, so a call
///     site that offers a <c>FormattableString</c> overload beside the <c>string</c> one can bind
///     somewhere else after the rewrite — and in EF Core's <c>FromSql</c> family that is the
///     difference between a parameterised query and a concatenated one. The rule reads the member
///     group at the call site and declines.
///     <para>
///         ⚠ <c>SK2016</c> reports the one place interpolation must <em>not</em> be used, and this rule
///         never creates that finding: a <c>string.Format</c> feeding a logger's <c>message</c>
///         parameter is left alone.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InterpolatedStringFormAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.InterpolatedStringForm);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.InterpolatedStringForm);

    /// <summary>A separator no interpolated string can contain, so the flattening is unambiguous.</summary>
    const char Separator = '\u0001';

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                var logger = start.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.LoggerExtensions");
                start.RegisterSyntaxNodeAction(
                    context => AnalyzeFormat(context, logger),
                    SyntaxKind.InvocationExpression
                );

                start.RegisterSyntaxNodeAction(AnalyzeHole, SyntaxKind.Interpolation);
            }
        );
    }

    /// <summary>Shape 1: <c>string.Format("{0} of {1}", done, total)</c>.</summary>
    static void AnalyzeFormat(SyntaxNodeAnalysisContext context, INamedTypeSymbol? logger) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetOperation(invocation, cancellation) is not IInvocationOperation operation
            || operation.TargetMethod is not {
                Name: "Format",
                IsStatic: true,
                ContainingType.SpecialType: SpecialType.System_String
            } method
            || method.Parameters.Length < 2
            || method.Parameters[0].Type.SpecialType != SpecialType.System_String) {
            return;
        }

        if (invocation.ArgumentList.Arguments.Count < 2
            || invocation.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax {
                RawKind: (int)SyntaxKind.StringLiteralExpression
            } format
            || !format.Token.IsKind(SyntaxKind.StringLiteralToken)
            || format.Token.Text.StartsWith("@", StringComparison.Ordinal)) {
            return;
        }

        var arguments = new List<string>();
        foreach (var argument in invocation.ArgumentList.Arguments) {
            if (ReferenceEquals(argument, invocation.ArgumentList.Arguments[0])) {
                continue;
            }

            // ⚠ Plain names only. A comma or a colon inside a hole is grammar, not text, so an
            // argument carrying either would land in an alignment or a format clause.
            if (argument is not { NameColon: null, RefKindKeyword.RawKind: 0 }
                || !RewriteGuards.IsPlainNamePath(argument.Expression)) {
                return;
            }

            arguments.Add(argument.Expression.ToString());
        }

        // ⚠ An explicitly passed `object[]` is not two arguments, it is one — and `{0}` then means
        // its first element. Only the array the compiler synthesised for a `params` call qualifies.
        if (method.Parameters[method.Parameters.Length - 1].IsParams) {
            var last = operation.Arguments[operation.Arguments.Length - 1];
            if (last.ArgumentKind != ArgumentKind.ParamArray
                || last.Value is not IArrayCreationOperation { IsImplicit: true }) {
                return;
            }
        }

        var text = format.Token.Text;
        if (Interpolate(text.Substring(1, text.Length - 2), arguments) is not { } inner) {
            return;
        }

        var replacement = "$\"" + inner + "\"";
        if (!ParsesToTheSameHoles(replacement, arguments)
            || FeedsALoggerTemplate(model, invocation, logger, cancellation)
            || OffersAFormattableStringOverload(model, invocation, cancellation)
            || NullComparison.InsideExpressionTree(model, invocation, cancellation)
            || RewriteGuards.ContainsCommentOrDirective(invocation)) {
            return;
        }

        Report(context, invocation.Span, replacement, "The numbered holes are an interpolation");
    }

    /// <summary>Shapes 2 and 3: what is sitting inside an interpolation hole.</summary>
    static void AnalyzeHole(SyntaxNodeAnalysisContext context) {
        var hole = (InterpolationSyntax)context.Node;
        if (hole.FormatClause is not null || hole.Parent is not InterpolatedStringExpressionSyntax whole) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (NullComparison.InsideExpressionTree(model, hole, cancellation)
            || RewriteGuards.ContainsCommentOrDirective(hole)) {
            return;
        }

        if (hole.Expression is InvocationExpressionSyntax {
                ArgumentList.Arguments.Count: 0,
                Expression: MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
                } call
            } invocation
            && call.Name.Identifier.ValueText == "ToString") {
            AnalyzeRedundantToString(context, invocation, call, model, cancellation);
            return;
        }

        if (hole.AlignmentClause is null) {
            AnalyzeLiteralInAHole(context, hole, whole);
        }
    }

    /// <summary>Shape 2: <c>$"{count.ToString()}"</c> renders through a string nobody keeps.</summary>
    /// <remarks>
    ///     ⚠ Value types only. On a reference type <c>null.ToString()</c> throws where <c>$"{null}"</c>
    ///     renders the empty string, so the rewrite would be a behaviour change rather than a
    ///     modernization. <c>Nullable&lt;T&gt;</c> is admitted: both spellings render empty.
    /// </remarks>
    static void AnalyzeRedundantToString(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax call,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol {
                Name: "ToString",
                Parameters.Length: 0
            }
            || !RewriteGuards.IsPlainNamePath(call.Expression)
            || model.GetTypeInfo(call.Expression, cancellation).Type is not {
                TypeKind: not (TypeKind.Error or TypeKind.Dynamic or TypeKind.TypeParameter)
            } type
            || !type.IsValueType) {
            return;
        }

        Report(
            context,
            invocation.Span,
            call.Expression.ToString(),
            "The hole renders the value without the intermediate string"
        );
    }

    /// <summary>Shape 3: <c>$"a{"b"}c"</c> is a hole holding something that was never a variable.</summary>
    static void AnalyzeLiteralInAHole(
        SyntaxNodeAnalysisContext context,
        InterpolationSyntax hole,
        InterpolatedStringExpressionSyntax whole
    ) {
        // ⚠ Regular forms on both sides. A verbatim or raw interpolated string escapes its text
        // differently, so the literal's characters would need re-escaping rather than copying.
        if (!whole.StringStartToken.IsKind(SyntaxKind.InterpolatedStringStartToken)
            || hole.Expression is not LiteralExpressionSyntax {
                RawKind: (int)SyntaxKind.StringLiteralExpression
            } literal
            || !literal.Token.IsKind(SyntaxKind.StringLiteralToken)
            || literal.Token.Text.StartsWith("@", StringComparison.Ordinal)) {
            return;
        }

        var inner = literal.Token.Text;
        var merged = inner.Substring(1, inner.Length - 2).Replace("{", "{{").Replace("}", "}}");

        var source = whole.ToString();
        var offset = hole.Span.Start - whole.Span.Start;
        var rebuilt = source.Substring(0, offset) + merged + source.Substring(offset + hole.Span.Length);
        if (!SplicesCleanly(whole, hole, literal.Token.ValueText, rebuilt)) {
            return;
        }

        Report(context, hole.Span, merged, "The hole holds a literal that belongs in the text");
    }

    static void Report(
        SyntaxNodeAnalysisContext context,
        Microsoft.CodeAnalysis.Text.TextSpan span,
        string replacement,
        string reason
    ) =>
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(context.Node.SyntaxTree, span),
                FixEdits.Pack((span, replacement)),
                reason + ": `" + RewriteGuards.Trim(replacement) + "`"
            )
        );

    /// <summary>
    ///     The interpolated body for a composite format string, or null where it does not qualify.
    /// </summary>
    /// <remarks>
    ///     ⚠ The indices must be <c>0</c>…<c>n-1</c>, each used once and in ascending order. A repeat
    ///     would evaluate its argument twice where <c>Format</c> evaluated it once; an out-of-order
    ///     index would evaluate the arguments in a different order; a gap leaves an argument unused.
    ///     Alignment and format clauses are carried across verbatim — <c>{0,10:N2}</c> becomes
    ///     <c>{done,10:N2}</c> — and <c>{{</c>/<c>}}</c> mean a literal brace in both grammars.
    /// </remarks>
    static string? Interpolate(string inner, IReadOnlyList<string> arguments) {
        var builder = new StringBuilder(inner.Length);
        var next = 0;
        var index = 0;
        while (index < inner.Length) {
            var character = inner[index];
            if (character == '}') {
                if (index + 1 >= inner.Length || inner[index + 1] != '}') {
                    return null;
                }

                builder.Append("}}");
                index += 2;
                continue;
            }

            if (character != '{') {
                builder.Append(character);
                index++;
                continue;
            }

            if (index + 1 < inner.Length && inner[index + 1] == '{') {
                builder.Append("{{");
                index += 2;
                continue;
            }

            var end = inner.IndexOf('}', index + 1);
            if (end < 0) {
                return null;
            }

            var body = inner.Substring(index + 1, end - index - 1);
            var cut = body.Length;
            for (var i = 0; i < body.Length; i++) {
                if (body[i] is ',' or ':') {
                    cut = i;
                    break;
                }
            }

            if (!int.TryParse(body.Substring(0, cut), NumberStyles.None, CultureInfo.InvariantCulture, out var hole)
                || hole != next
                || hole >= arguments.Count) {
                return null;
            }

            next++;
            builder.Append('{').Append(arguments[hole]).Append(body.Substring(cut)).Append('}');
            index = end + 1;
        }

        return next == arguments.Count ? builder.ToString() : null;
    }

    /// <summary>⚠ Parse the replacement back and check the holes are the arguments, in order.</summary>
    /// <remarks>
    ///     The text between the placeholders is copied character for character out of the format
    ///     literal into an interpolated string with the same escaping rules, so it cannot change. The
    ///     placeholder scan is the part that can be wrong, and this is the assertion that it was not.
    /// </remarks>
    static bool ParsesToTheSameHoles(string replacement, IReadOnlyList<string> arguments) {
        var parsed = SyntaxFactory.ParseExpression(
            replacement,
            0,
            new CSharpParseOptions(LanguageVersion.CSharp11),
            true
        );

        if (parsed is not InterpolatedStringExpressionSyntax interpolated
            || parsed.ContainsDiagnostics
            || parsed.FullSpan.Length != replacement.Length) {
            return false;
        }

        var seen = 0;
        foreach (var content in interpolated.Contents) {
            if (content is not InterpolationSyntax hole) {
                continue;
            }

            if (seen >= arguments.Count
                || !string.Equals(hole.Expression.ToString(), arguments[seen], StringComparison.Ordinal)) {
                return false;
            }

            seen++;
        }

        return seen == arguments.Count;
    }

    /// <summary>⚠ Parse the spliced interpolated string back and compare it part by part.</summary>
    static bool SplicesCleanly(
        InterpolatedStringExpressionSyntax whole,
        InterpolationSyntax removed,
        string value,
        string rebuilt
    ) {
        var parsed = SyntaxFactory.ParseExpression(
            rebuilt,
            0,
            new CSharpParseOptions(LanguageVersion.CSharp11),
            true
        );

        if (parsed is not InterpolatedStringExpressionSyntax after
            || parsed.ContainsDiagnostics
            || parsed.FullSpan.Length != rebuilt.Length) {
            return false;
        }

        return string.Equals(Flatten(after), Expected(whole, removed, value), StringComparison.Ordinal);
    }

    /// <summary>Text parts as their values, holes as their source, with a separator neither can hold.</summary>
    static string Flatten(InterpolatedStringExpressionSyntax node) {
        var builder = new StringBuilder();
        foreach (var content in node.Contents) {
            switch (content) {
                case InterpolatedStringTextSyntax text:
                    builder.Append(text.TextToken.ValueText);
                    break;

                case InterpolationSyntax hole:
                    builder.Append(Separator).Append(hole.ToString()).Append(Separator);
                    break;
            }
        }

        return builder.ToString();
    }

    static string Expected(InterpolatedStringExpressionSyntax node, InterpolationSyntax removed, string value) {
        var builder = new StringBuilder();
        foreach (var content in node.Contents) {
            switch (content) {
                case InterpolatedStringTextSyntax text:
                    builder.Append(text.TextToken.ValueText);
                    break;

                case InterpolationSyntax hole when ReferenceEquals(hole, removed):
                    builder.Append(value);
                    break;

                case InterpolationSyntax hole:
                    builder.Append(Separator).Append(hole.ToString()).Append(Separator);
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>Whether the call's result is a logger's message template, which SK2016 owns.</summary>
    static bool FeedsALoggerTemplate(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol? logger,
        CancellationToken cancellation
    ) {
        if (logger is null
            || invocation.Parent is not ArgumentSyntax argument
            || argument.Parent?.Parent is not InvocationExpressionSyntax outer) {
            return false;
        }

        if (model.GetOperation(outer, cancellation) is not IInvocationOperation operation
            || !SymbolEqualityComparer.Default.Equals(
                (operation.TargetMethod.ReducedFrom ?? operation.TargetMethod).ContainingType,
                logger
            )) {
            return false;
        }

        foreach (var bound in operation.Arguments) {
            if (bound.Parameter?.Name == "message" && bound.Value.Syntax == invocation) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ Whether some overload at this call site would take the interpolation instead of the string.
    /// </summary>
    /// <remarks>
    ///     An interpolated string converts to <c>string</c>, <c>FormattableString</c> and
    ///     <c>IFormattable</c>, and <c>string.Format</c>'s result converts to only the first. So a
    ///     member group holding both — EF Core's <c>FromSql</c> family is the case that matters —
    ///     can rebind after the rewrite, and there the difference is whether the query is
    ///     parameterised. The member group is read rather than reasoned about.
    /// </remarks>
    static bool OffersAFormattableStringOverload(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellation
    ) {
        if (invocation.Parent is not ArgumentSyntax argument
            || argument.Parent is not ArgumentListSyntax list
            || list.Parent is not (InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax)) {
            return false;
        }

        var position = list.Arguments.IndexOf(argument);
        var candidates = list.Parent is InvocationExpressionSyntax outer
            ? model.GetMemberGroup(outer.Expression, cancellation)
            : model.GetMemberGroup(list.Parent, cancellation);

        foreach (var candidate in candidates) {
            if (candidate is not IMethodSymbol overload) {
                continue;
            }

            foreach (var parameter in overload.Parameters) {
                var name = parameter.Type.ToDisplayString();
                if ((name is "System.FormattableString" or "System.IFormattable")
                    && (parameter.Ordinal == position || parameter.IsParams)) {
                    return true;
                }
            }
        }

        return false;
    }
}
