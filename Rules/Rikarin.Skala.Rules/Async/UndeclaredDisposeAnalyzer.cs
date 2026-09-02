using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3540</c> — the type wrote the cleanup and never declared it, so nothing calls it.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime". ⚠
///     <b>
///         This is the other half of the ownership question from the one <c>SK3502</c> asks, and the
///         two are asked of different declarations.
///     </b> <c>SK3502</c> reads a <em>field</em> — a type
///     constructs a disposable and offers no matching disposal — and is silent about how the type
///     cleans up. This one reads a <em>method</em>: the cleanup is written, it is public, it is
///     spelled exactly the way the framework spells it, and the base list does not say so. Every
///     <c>using</c>, every <c>is IDisposable</c> test and every container teardown walks past it.
///     <para>
///         ⚠ <b>A <c>Dispose()</c> that is deliberately not the interface's is legitimate</b>, and two
///         shapes of it are excluded by construction rather than by heuristic. A <c>ref struct</c> is
///         disposable through the language's pattern rule with no interface at all, so its
///         <c>Dispose()</c> is the contract — that is <c>SK3532</c>'s subject, and it is skipped here.
///         And a pooled object's <c>Dispose()</c> that only resets fields is not cleanup this rule can
///         see: the body must actually release something — a <c>Dispose</c>, <c>DisposeAsync</c>,
///         <c>Close</c> or <c>GC.SuppressFinalize</c> call — before the finding stands.
///     </para>
///     <para>
///         ⚠ <b>Not hosted.</b> <c>CA1063</c> and <c>CA1816</c> both take a type that already implements
///         <c>IDisposable</c> as their subject, so neither can see a type that does not. Measured
///         outside this repository against an empty <c>Directory.Build.props</c> at
///         <c>AnalysisMode=All</c>: no <c>CA</c> diagnostic of any severity, hidden included, on the
///         shape this rule reports.
///     </para>
///     <para>
///         ⚠ The fix adds the interface to the base list and is <c>fixIsSafe: false</c> — it widens a
///         public surface, and a type that becomes <c>IDisposable</c> is one every caller may now wrap
///         in a <c>using</c> and one every container will now tear down.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UndeclaredDisposeAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.DisposeMethodWithoutInterface);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var disposable = start.Compilation.GetTypeByMetadataName("System.IDisposable");
                var asyncDisposable = start.Compilation.GetTypeByMetadataName("System.IAsyncDisposable");
                if (disposable is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, disposable, asyncDisposable),
                    SyntaxKind.ClassDeclaration,
                    SyntaxKind.StructDeclaration,
                    SyntaxKind.RecordDeclaration,
                    SyntaxKind.RecordStructDeclaration
                );
            }
        );
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol disposable,
        INamedTypeSymbol? asyncDisposable
    ) {
        var declaration = (TypeDeclarationSyntax)context.Node;

        // ⚠ Another part of a partial may carry the base list, and it is not in this tree.
        if (declaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword))
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
            is not INamedTypeSymbol owner
            || owner.TypeKind == TypeKind.Error
            // ⚠ `SK3532`'s subject. A `ref struct`'s `Dispose()` *is* the disposal contract — the
            // language's pattern rule binds `using` to it with no interface in sight — so reporting
            // it here would report the correct spelling of the thing as a defect.
            || owner.IsRefLikeType
            || UsingResource.Implements(owner, disposable)
            || UsingResource.Implements(owner, asyncDisposable)) {
            return;
        }

        // ⚠ A base type this compilation cannot see may declare the contract, and `AllInterfaces`
        // on an unresolved base is empty rather than unknown — which is the shape of a false
        // positive that only appears in a consumer's tree.
        if (owner.BaseType is { TypeKind: TypeKind.Error }) {
            return;
        }

        var method = FindDispose(declaration);
        if (method is null
            // ⚠ An `override` means the base declares it, so the base is where the contract is
            // missing and the base is what gets reported. Reporting both says one thing twice.
            || method.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.OverrideKeyword))
            || !ReleasesSomething(method)) {
            return;
        }

        var insertion = InsertionPoint(context, declaration, out var text);
        if (insertion is null) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                method.Identifier.GetLocation(),
                FixEdits.Pack((new TextSpan(insertion.Value, 0), text)),
                "`"
                + owner.Name
                + ".Dispose()` releases resources and `"
                + owner.Name
                + "` does not implement `IDisposable`, so no `using`, cast or container teardown reaches it"
            )
        );
    }

    /// <summary>
    ///     The one member spelling <c>using</c> and <c>IDisposable</c> would both have bound to.
    /// </summary>
    static MethodDeclarationSyntax? FindDispose(TypeDeclarationSyntax declaration) {
        foreach (var member in declaration.Members.OfType<MethodDeclarationSyntax>()) {
            if (member.Identifier.ValueText != "Dispose"
                || member.ParameterList.Parameters.Count != 0
                || member.TypeParameterList is not null
                || member.ExplicitInterfaceSpecifier is not null
                // ⚠ No `abstract` test, and its absence is deliberate. One was written here and a
                // sabotage proved it dead: an abstract method has no body, so `ReleasesSomething`
                // withdraws every one of them anyway. Two guards where one is load-bearing reads as
                // two reasons a shape is excluded, and only one of them is true.
                || member.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword))
                || !member.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PublicKeyword))
                || member.ReturnType is not PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword }) {
                continue;
            }

            return member;
        }

        return null;
    }

    /// <summary>
    ///     Whether the body is cleanup rather than a reset.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the guard that keeps the legitimate <c>Dispose()</c> out. A pooled object's
    ///     <c>Dispose()</c> returns itself to its pool and clears its fields; it is named for the
    ///     caller's convenience and was never meant to be the interface's. Requiring the body to
    ///     <em>release</em> something — call <c>Dispose</c>, <c>DisposeAsync</c> or <c>Close</c> on
    ///     something, or suppress a finalizer — is the difference the issue's own rationale turns on:
    ///     the finding is "the type already wrote the cleanup", so where no cleanup is written there is
    ///     nothing to report. It costs the rule every `Dispose` that releases through a helper it
    ///     cannot name, and that cost is the right way round.
    /// </remarks>
    static bool ReleasesSomething(MethodDeclarationSyntax method) {
        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>()) {
            var name = invocation.Expression switch {
                MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
                MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                _ => null
            };

            if (name is "Dispose" or "DisposeAsync" or "Close" or "SuppressFinalize") {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Where <c>IDisposable</c> goes, and whether it can be written unqualified there.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two ways this fix compiles as text and not as a program, and both are guarded rather than
    ///     hoped for. <c>System</c> may not be imported — a file with no <c>using System;</c> and no
    ///     <c>ImplicitUsings</c> — so the name is written qualified unless <c>IDisposable</c> already
    ///     binds to <c>System.IDisposable</c> at the declaration's own position, which is asked of the
    ///     semantic model rather than assumed. And the insertion point is the end of the base list, or
    ///     the end of the header when there is none — never the open brace, because a type parameter
    ///     constraint sits between the two and <c>class C&lt;T&gt; where T : new() : IDisposable</c> is
    ///     not a program.
    ///     <para>
    ///         ⚠ A directive anywhere in the header withdraws the finding: the fix inserts at a position
    ///         rather than replacing a node, and under <c>#if</c> the position it names is not the
    ///         position every branch compiles at. Asked with
    ///         <c>RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(tree, span)</c> over the span the edit lands
    ///         in — not over the trivia above the declaration, which is what an XML comment on the type
    ///         lives in and has nothing to do with this.
    ///     </para>
    /// </remarks>
    static int? InsertionPoint(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax declaration, out string text) {
        text = "";

        var name = context.SemanticModel
            .LookupNamespacesAndTypes(declaration.Identifier.SpanStart, name: "IDisposable")
            .Any(static symbol => symbol is INamedTypeSymbol {
                    Name: "IDisposable",
                    ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
                }
            )
                ? "IDisposable"
                : "System.IDisposable";

        int position;
        if (declaration.BaseList is { Types.Count: > 0 } bases) {
            position = bases.Types[bases.Types.Count - 1].Span.End;
            text = ", " + name;
        } else {
            // ⚠ The header ends after the type parameter list, or after a record's parameter list —
            // and before any constraint clause, which is where the base list is required to go.
            position = declaration.ParameterList?.Span.End
                ?? declaration.TypeParameterList?.Span.End
                ?? declaration.Identifier.Span.End;
            text = " : " + name;
        }

        var header = TextSpan.FromBounds(declaration.Identifier.SpanStart, position);
        return Modernization.RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(declaration.SyntaxTree, header)
            ? null
            : position;
    }
}
