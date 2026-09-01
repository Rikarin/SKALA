using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1053</c> — a named local that exists only so something has somewhere to go.
/// </summary>
/// <remarks>
///     ⚠ <b>This is not <c>SK0217</c>.</b> <c>arrange-discard-declaration</c> decides how an
///     <em>existing</em> discard is spelled — <c>var _</c> or <c>_</c> — and never introduces one. This
///     rule introduces one, in the two places a name is invented for a value nobody reads: a local
///     whose initializer is a call kept for its effect, and an <c>out var</c> the caller does not want.
///     <para>
///         ⚠ <b><c>_</c> is only a discard where nothing else claims the name.</b> A local, a
///         parameter or a field called <c>_</c> turns <c>_ = Foo();</c> into an assignment to that
///         thing, which is a different program and a silent one. Every lookup at the rewrite position
///         has to come back empty.
///     </para>
///     <para>
///         ⚠ <b>Only <c>out var</c>, never <c>out T</c>.</b> A discard has no type, so replacing an
///         explicitly typed out-variable can change which overload the call resolves to. <c>out var</c>
///         is already typeless in that sense and loses nothing.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiscardAssignmentAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.DiscardOverUnreadLocal);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.DiscardOverUnreadLocal);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(AnalyzeLocal, SyntaxKind.LocalDeclarationStatement);
                start.RegisterSyntaxNodeAction(AnalyzeOutVariable, SyntaxKind.Argument);
            }
        );
    }

    /// <summary>A local whose initializer is a call, and which nothing ever reads.</summary>
    static void AnalyzeLocal(SyntaxNodeAnalysisContext context) {
        var statement = (LocalDeclarationStatementSyntax)context.Node;
        if (statement.UsingKeyword.RawKind != (int)SyntaxKind.None
            || statement.AwaitKeyword.RawKind != (int)SyntaxKind.None
            || statement.Modifiers.Count > 0
            || statement.AttributeLists.Count > 0
            || statement.Declaration.Variables.Count != 1) {
            return;
        }

        var declarator = statement.Declaration.Variables[0];

        // ⚠ Only an initializer whose evaluation is the point. `var x = 5;` is dead code and the
        // repair is to delete it; `_ = 5;` is a different piece of dead code with an assignment
        // bolted on. The shapes that carry an effect worth keeping are calls and constructions.
        if (declarator.Initializer?.Value is not { } initializer || !HasEffect(initializer)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetDeclaredSymbol(declarator, cancellation) is not ILocalSymbol {
                RefKind: RefKind.None,
                IsConst: false
            } local
            || IsRead(model, local, declarator, cancellation)
            || NameIsTaken(model, statement.SpanStart, cancellation)
            || RewriteGuards.ContainsCommentOrDirective(statement)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declarator.Identifier.GetLocation(),
                FixEdits.Pack((statement.Declaration.Span, "_ = " + initializer)),
                "`" + local.Name + "` is assigned and never read; that is what `_` is for"
            )
        );
    }

    /// <summary>An <c>out var</c> the caller declares and then ignores.</summary>
    static void AnalyzeOutVariable(SyntaxNodeAnalysisContext context) {
        var argument = (ArgumentSyntax)context.Node;
        if (!argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)
            || argument.Expression is not DeclarationExpressionSyntax {
                Designation: SingleVariableDesignationSyntax designation
            } declaration
            || !declaration.Type.IsVar) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetDeclaredSymbol(designation, cancellation) is not ILocalSymbol local
            || IsRead(model, local, designation, cancellation)
            || NameIsTaken(model, argument.SpanStart, cancellation)
            || RewriteGuards.ContainsCommentOrDirective(argument)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                designation.GetLocation(),
                FixEdits.Pack((declaration.Span, "_")),
                "`" + local.Name + "` is written by the call and never read; that is what `_` is for"
            )
        );
    }

    /// <summary>
    ///     ⚠ Whether anything at all mentions the local outside its own declaration.
    /// </summary>
    /// <remarks>
    ///     Deliberately "mentions" rather than "reads". A later assignment is not a read, and dropping
    ///     the declaration under one would be a compile error rather than a tidy-up; distinguishing the
    ///     two buys nothing here, because a local that is written twice and read never is a shape this
    ///     rule has no fix for anyway.
    /// </remarks>
    static bool IsRead(SemanticModel model, ILocalSymbol local, SyntaxNode declaration, CancellationToken cancellation) {
        foreach (var node in RewriteGuards.ScopeRoot(declaration).DescendantNodes()) {
            cancellation.ThrowIfCancellationRequested();
            if (node is not IdentifierNameSyntax identifier
                || !string.Equals(identifier.Identifier.ValueText, local.Name, System.StringComparison.Ordinal)
                || declaration.Span.Contains(identifier.Span)) {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellation).Symbol, local)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ Whether anything in scope is already called <c>_</c>, in which case it is a name and not
    ///     a discard.
    /// </summary>
    static bool NameIsTaken(SemanticModel model, int position, CancellationToken cancellation) {
        cancellation.ThrowIfCancellationRequested();
        return model.LookupSymbols(position, name: "_").Length > 0;
    }

    /// <summary>Whether evaluating the initializer is the reason the statement is there.</summary>
    static bool HasEffect(ExpressionSyntax expression) =>
        expression is InvocationExpressionSyntax
            or ObjectCreationExpressionSyntax
            or ImplicitObjectCreationExpressionSyntax
            or AwaitExpressionSyntax
            or ConditionalAccessExpressionSyntax;
}
