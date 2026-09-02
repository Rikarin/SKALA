using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1071</c> — <c>new R(x.A, x.B, c)</c> is <c>x with { C = c }</c>.
/// </summary>
/// <remarks>
///     <para>
///         ⚠
///         <b>
///             <c>with</c> does not do what the constructor call does, and the difference is the whole
///             rule.
///         </b> <c>with</c> invokes the record's copy constructor, which copies <em>every</em>
///         field — including the ones the hand-written call deliberately left out. So the rewrite is
///         sound only where the record has no state beyond its positional parameters, and this asks for
///         that rather than pattern-matching the shape: no instance field, no instance event, no
///         settable property outside the parameter list, no base record, and <c>sealed</c>.
///     </para>
///     <para>
///         ⚠ <b><c>sealed</c> is not tidiness.</b> <c>x with { … }</c> calls the virtual clone and
///         returns <em>x</em>'s runtime type; <c>new R(…)</c> returns exactly <c>R</c>. On an unsealed
///         record holding a derived instance those are two different objects, and nothing at the call
///         site says which one is there.
///     </para>
///     <para>
///         ⚠ <b>Every positional property must be the auto-property the record synthesized.</b> A
///         hand-written accessor is called by <c>x.A</c> and is <em>not</em> called by the copy
///         constructor, which copies the backing field straight across — so a property that computes,
///         logs or lazily fills would produce a different value on each side of the rewrite.
///     </para>
///     <para>
///         ⚠
///         <b>
///             At least one argument has to be replaced, which is also what keeps this rule and
///             <c>SK0230</c> off each other's ground.
///         </b> A call carrying every member across unchanged
///         rewrites to <c>x with { }</c> — an empty <c>with</c>, which is exactly what <c>SK0230</c>
///         reports. Requiring a replacement means the two rules never see the same code.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WithExpressionCopyAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.WithExpressionCopy);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.WithExpressionCopy);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    start.RegisterSyntaxNodeAction(
                        Analyze,
                        SyntaxKind.ObjectCreationExpression,
                        SyntaxKind.ImplicitObjectCreationExpression
                    );
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (creation.Initializer is not null
            || creation.ArgumentList is not { Arguments.Count: > 0 } arguments
            || !SitsWhereAWithExpressionNeedsNoParentheses(creation)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetSymbolInfo(creation, cancellation).Symbol is not IMethodSymbol {
                MethodKind: MethodKind.Constructor
            } constructor) {
            return;
        }

        var record = constructor.ContainingType;
        if (!RecordShape.WholeStateIsItsParameters(record, constructor, cancellation)) {
            return;
        }

        if (arguments.Arguments.Count != constructor.Parameters.Length) {
            return;
        }

        // The receiver every carried-across member is read from, discovered from the first such
        // argument and then required to be the same one on all of them.
        IdentifierNameSyntax? receiver = null;
        ISymbol? receiverSymbol = null;
        var replaced = new List<(string Name, ExpressionSyntax Value)>();

        for (var i = 0; i < arguments.Arguments.Count; i++) {
            cancellation.ThrowIfCancellationRequested();
            var argument = arguments.Arguments[i];
            if (argument.NameColon is not null
                || argument.RefKindKeyword.RawKind != (int)SyntaxKind.None) {
                return;
            }

            var parameter = constructor.Parameters[i];
            if (CarriesAcross(model, argument.Expression, parameter, cancellation) is { } source) {
                if (receiver is null) {
                    receiver = source;
                    receiverSymbol = model.GetSymbolInfo(source, cancellation).Symbol;

                    // ⚠ A local or a parameter, and one nothing in the member assigns. `with`
                    // evaluates the receiver once and clones before any initializer runs, so an
                    // argument that reassigns it would leave the two forms reading two objects.
                    if (receiverSymbol is not (ILocalSymbol or IParameterSymbol)
                        || IsAssignedSomewhereInTheMember(model, source, receiverSymbol, cancellation)) {
                        return;
                    }
                } else if (!RewriteGuards.Same(source, receiver)
                           || !SymbolEqualityComparer.Default.Equals(
                               model.GetSymbolInfo(source, cancellation).Symbol,
                               receiverSymbol
                           )) {
                    return;
                }

                continue;
            }

            replaced.Add((parameter.Name, argument.Expression));
        }

        // ⚠ One carried member is what makes this a copy; one replaced member is what keeps the fix
        // off `SK0230`'s ground — a call carrying everything across rewrites to `x with { }`.
        if (receiver is null || replaced.Count == 0) {
            return;
        }

        // The receiver has to hold exactly the record, not a base of it and not a nullable
        // annotation the `with` would read differently.
        if (model.GetTypeInfo(receiver, cancellation).Type is not { } held
            || !SymbolEqualityComparer.Default.Equals(held, record)) {
            return;
        }

        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(creation.SyntaxTree, creation.Span)
            || NullComparison.InsideExpressionTree(model, creation, cancellation)) {
            return;
        }

        var replacement = new StringBuilder(receiver.Identifier.ValueText).Append(" with { ");
        for (var i = 0; i < replaced.Count; i++) {
            if (i > 0) {
                replacement.Append(", ");
            }

            replacement.Append(replaced[i].Name).Append(" = ").Append(replaced[i].Value.ToString());
        }

        replacement.Append(" }");

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                creation.GetLocation(),
                FixEdits.Pack((creation.Span, replacement.ToString())),
                "The record is copied member by member, so the next member added to `"
                + record.Name
                + "` would stop travelling: `"
                + RewriteGuards.Trim(replacement.ToString())
                + "`"
            )
        );
    }

    /// <summary>
    ///     The receiver, when the argument is <c>x.P</c> reading the property this parameter fills.
    /// </summary>
    static IdentifierNameSyntax? CarriesAcross(
        SemanticModel model,
        ExpressionSyntax argument,
        IParameterSymbol parameter,
        CancellationToken cancellation
    ) {
        if (argument is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: IdentifierNameSyntax receiver,
                Name: IdentifierNameSyntax member
            }
            || !string.Equals(member.Identifier.ValueText, parameter.Name, System.StringComparison.Ordinal)) {
            return null;
        }

        return model.GetSymbolInfo(member, cancellation).Symbol is IPropertySymbol { IsStatic: false }
            ? receiver
            : null;
    }

    /// <summary>
    ///     Whether anything in the enclosing member writes to the receiver.
    /// </summary>
    /// <remarks>
    ///     ⚠ The constructor call reads <c>x</c> once per carried member, at each argument's position;
    ///     the <c>with</c> reads it once, before any argument runs. Those are the same program only
    ///     while <c>x</c> cannot change in between — which an argument reassigning it, directly or
    ///     through a lambda that captured it, is exactly what would do. Scanning the whole member for a
    ///     write is the cheapest question that rules all of them out at once.
    /// </remarks>
    static bool IsAssignedSomewhereInTheMember(
        SemanticModel model,
        SyntaxNode reference,
        ISymbol? symbol,
        CancellationToken cancellation
    ) {
        foreach (var node in RewriteGuards.ScopeRoot(reference).DescendantNodes()) {
            cancellation.ThrowIfCancellationRequested();
            if (node is not IdentifierNameSyntax identifier
                || !string.Equals(
                    identifier.Identifier.ValueText,
                    symbol?.Name,
                    System.StringComparison.Ordinal
                )) {
                continue;
            }

            var writes = identifier.Parent switch {
                AssignmentExpressionSyntax assignment => ReferenceEquals(assignment.Left, identifier),
                PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax => true,
                ArgumentSyntax { RefKindKeyword.RawKind: not (int)SyntaxKind.None } => true,
                _ => false
            };

            if (writes
                && SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(identifier, cancellation).Symbol,
                    symbol
                )) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ <c>with</c> binds looser than member access, so <c>new R(…).ToString()</c> would rewrite
    ///     into text that does not parse. Only positions where no parentheses are needed are matched.
    /// </summary>
    static bool SitsWhereAWithExpressionNeedsNoParentheses(ExpressionSyntax creation) =>
        creation.Parent switch {
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax or PropertyDeclarationSyntax } => true,
            ReturnStatementSyntax or ArrowExpressionClauseSyntax or YieldStatementSyntax => true,
            ArgumentSyntax { RefKindKeyword.RawKind: (int)SyntaxKind.None, NameColon: null } => true,
            AssignmentExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleAssignmentExpression
            } assignment => ReferenceEquals(assignment.Right, creation),
            InitializerExpressionSyntax => true,
            _ => false
        };
}
