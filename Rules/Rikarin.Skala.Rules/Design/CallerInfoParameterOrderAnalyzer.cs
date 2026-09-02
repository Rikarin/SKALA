using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Design;

/// <summary>
///     <c>SK6061</c> — a caller-info parameter with an ordinary parameter declared after it.
/// </summary>
/// <remarks>
///     <para>
///         The compiler fills a caller-info parameter only where the caller <em>omitted</em> it. A
///         parameter after one can therefore never be reached positionally without also supplying the
///         caller-info parameter, at which point the substitution is cancelled and the value is
///         whatever was typed. The declaration reads as two independent conveniences and behaves as
///         one that disables the other.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The shape the concept is usually described with does not compile, and establishing
///             that is what made the rule narrow.
///         </b> A caller-info attribute on a parameter without a
///         default value is <c>CS4022</c>, verified against the compiler rather than reasoned about,
///         so "a required parameter after a caller-info one" is not a program. Everything after an
///         optional parameter is optional or <c>params</c>, and the real defect is two optional
///         parameters where the first is filled by the compiler and the second is what the caller
///         wanted.
///     </para>
///     <para>
///         ⚠ A trailing <em>run</em> of caller-info parameters is the correct shape and is never
///         reported. The rule fires only where a caller-info parameter is followed by one carrying no
///         caller-info attribute, which is also why the fix rotates the whole run to the end together
///         instead of swapping a pair.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CallerInfoParameterOrderAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CallerInfoParameterNotLast);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.LocalFunctionStatement
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var list = context.Node switch {
            MethodDeclarationSyntax declaration => declaration.ParameterList,
            ConstructorDeclarationSyntax constructor => constructor.ParameterList,
            LocalFunctionStatementSyntax local => local.ParameterList,
            _ => null
        };

        if (list is not { Parameters.Count: > 1 }) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken)
            is not IMethodSymbol method
            || method.Parameters.Length != list.Parameters.Count) {
            return;
        }

        // ⚠ Three declarations whose parameter order is fixed by something else in the program. An
        // override, an interface implementation and a partial implementation would each stop
        // overriding, implementing or matching if a parameter moved. The defect belongs on the
        // declaration that owns the order; reporting it here would report it where it cannot be
        // fixed.
        if (method.IsOverride
            || method.IsPartialDefinition
            || method.PartialDefinitionPart is not null
            || method.PartialImplementationPart is not null
            || Implements(method)) {
            return;
        }

        var caller = new bool[method.Parameters.Length];
        var any = false;
        for (var i = 0; i < method.Parameters.Length; i++) {
            // ⚠ `params` must be last, so the caller-info run has nowhere to go and the only
            // rewrite satisfying both constraints is the one already written. Silence, not a
            // finding without a fix.
            if (method.Parameters[i].IsParams) {
                return;
            }

            caller[i] = IsCallerInfo(method.Parameters[i]);

            // ⚠ CS4022 makes a caller-info parameter without a default value a compiler error, so
            // this should be unreachable — and it is asserted rather than assumed, because the
            // rewrite depends on it. A required parameter moved behind an optional one is CS1737,
            // and the whole fix rests on every caller-info parameter already being optional.
            if (caller[i] && !method.Parameters[i].IsOptional) {
                return;
            }

            any |= caller[i];
        }

        if (!any) {
            return;
        }

        // The correct shape is a caller-info run at the very end. Anything the run does not cover
        // from the last parameter backwards is a parameter the compiler cannot help.
        var last = caller.Length - 1;
        while (last >= 0 && caller[last]) {
            last--;
        }

        var misplaced = new List<int>();
        for (var i = 0; i <= last; i++) {
            if (caller[i]) {
                misplaced.Add(i);
            }
        }

        if (misplaced.Count == 0) {
            return;
        }

        if (RewriteGuards.ContainsCommentOrDirective(context.Node.SyntaxTree, list.Span)) {
            return;
        }

        var builder = new System.Text.StringBuilder();
        foreach (var index in Order(caller)) {
            if (builder.Length > 0) {
                builder.Append(", ");
            }

            builder.Append(list.Parameters[index].WithoutTrivia().ToFullString());
        }

        var rewritten = builder.ToString();
        var span = TextSpan.FromBounds(
            list.Parameters[0].SpanStart,
            list.Parameters[list.Parameters.Count - 1].Span.End
        );

        var subject = list.Parameters[misplaced[0]].Identifier.ValueText;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                list.Parameters[misplaced[0]].GetLocation(),
                FixEdits.Pack((span, rewritten)),
                "`"
                + subject
                + "` carries a caller-info attribute and is not at the end of the parameter list, so "
                + "reaching a later parameter positionally cancels the substitution"
            )
        );
    }

    /// <summary>The parameter indexes with every caller-info parameter moved to the end.</summary>
    /// <remarks>
    ///     ⚠ The whole run moves together and keeps its internal order, and the ordinary parameters
    ///     keep theirs. A swap of one pair would be a smaller edit and would leave a second caller-info
    ///     parameter stranded in the middle, so <c>skala fix</c> would report its own output.
    /// </remarks>
    static IEnumerable<int> Order(bool[] caller) {
        for (var i = 0; i < caller.Length; i++) {
            if (!caller[i]) {
                yield return i;
            }
        }

        for (var i = 0; i < caller.Length; i++) {
            if (caller[i]) {
                yield return i;
            }
        }
    }

    static bool IsCallerInfo(IParameterSymbol parameter) {
        foreach (var attribute in parameter.GetAttributes()) {
            switch (attribute.AttributeClass?.Name) {
                case "CallerMemberNameAttribute":
                case "CallerLineNumberAttribute":
                case "CallerFilePathAttribute":
                case "CallerArgumentExpressionAttribute":
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether this method's signature is dictated by an interface it implements, explicitly or
    ///     implicitly.
    /// </summary>
    static bool Implements(IMethodSymbol method) {
        if (!method.ExplicitInterfaceImplementations.IsDefaultOrEmpty) {
            return true;
        }

        if (method.ContainingType is not { } containing) {
            return false;
        }

        foreach (var @interface in containing.AllInterfaces) {
            foreach (var member in @interface.GetMembers(method.Name)) {
                if (member is IMethodSymbol candidate
                    && SymbolEqualityComparer.Default.Equals(
                        containing.FindImplementationForInterfaceMember(candidate),
                        method
                    )) {
                    return true;
                }
            }
        }

        return false;
    }
}
