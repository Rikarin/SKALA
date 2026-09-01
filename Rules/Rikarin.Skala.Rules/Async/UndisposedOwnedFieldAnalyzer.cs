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

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3530</c> — the type is disposable, owns a disposable field, and its <c>Dispose</c> never
///     reaches it.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime". This is the half of the
///     ownership question that looks finished. <c>SK3502</c> reports a type that owns a disposable and
///     is <em>not</em> disposable, which reads as unfinished work; here the declaration is present, the
///     <c>Dispose</c> is present, and the resource is still never released — so every reader, every
///     caller and every review sees a type that cleans up after itself.
///     <para>
///         ⚠ <b>Disjoint from <c>SK3502</c> by construction, not by deduplication.</b> This rule
///         requires the owner to implement <c>IDisposable</c>; <c>SK3502</c> reports only where the owner
///         does <em>not</em> implement the contract the field offers. The two predicates cannot both hold
///         for one field, so neither needs <c>supersedes</c> — which would suppress one of them on a
///         shared span rather than keeping each rule's report a property of that rule.
///     </para>
///     <para>
///         ⚠ <b>"Never disposed" is asked of the whole type, not of <c>Dispose</c>'s body.</b> The
///         documented pattern puts the work in <c>Dispose(bool disposing)</c>, and a helper called from
///         <c>Dispose</c> is just as good; reading only the entry point would report every correct
///         implementation of the pattern. Any disposal of the field anywhere in the declaration withdraws
///         the finding.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UndisposedOwnedFieldAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.DisposableFieldNotDisposed);

    /// <summary>⚠ Disposables the framework asks callers to leave alone.</summary>
    static readonly string[] Excluded = ["System.Threading.Tasks.Task", "System.Threading.Tasks.Task`1"];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var disposable = start.Compilation.GetTypeByMetadataName("System.IDisposable");
                if (disposable is null) {
                    return;
                }

                var excluded = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                foreach (var name in Excluded) {
                    if (start.Compilation.GetTypeByMetadataName(name) is { } type) {
                        excluded.Add(type);
                    }
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, disposable, excluded),
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
        HashSet<INamedTypeSymbol> excluded
    ) {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
            is not INamedTypeSymbol owner
            || owner.TypeKind == TypeKind.Error
            // ⚠ A partial type's other half may hold the disposal, and it is not in this tree.
            || owner.DeclaringSyntaxReferences.Length != 1
            || !UsingResource.Implements(owner, disposable)) {
            return;
        }

        if (DisposeBodyOf(context, owner, declaration) is not { } body) {
            return;
        }

        foreach (var field in declaration.Members.OfType<FieldDeclarationSyntax>()) {
            if (field.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword))) {
                continue;
            }

            foreach (var variable in field.Declaration.Variables) {
                // ⚠ Ownership is inferred exactly the way SK3502 infers it — a direct object
                // creation in the field's own initializer — so the pair covers one predicate split
                // in two rather than two predicates that happen to be near each other.
                if (variable.Initializer?.Value is not BaseObjectCreationExpressionSyntax
                    || context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken)
                    is not IFieldSymbol symbol
                    || symbol.DeclaredAccessibility != Accessibility.Private
                    || symbol.Type.TypeKind == TypeKind.Error
                    || symbol.NullableAnnotation == NullableAnnotation.Annotated
                    || symbol.Type is INamedTypeSymbol named
                    && excluded.Contains(named.OriginalDefinition)
                    || !UsingResource.Implements(symbol.Type, disposable)
                    || !NothingReleasesIt(context, declaration, symbol, variable)) {
                    continue;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        variable.Identifier.GetLocation(),
                        Insert(body, symbol.Name),
                        "`"
                        + owner.Name
                        + "` is disposable and nothing in it ever disposes `"
                        + symbol.Name
                        + "`"
                    )
                );
            }
        }
    }

    /// <summary>
    ///     The block body of this type's own parameterless <c>Dispose</c>, or null.
    /// </summary>
    /// <remarks>
    ///     ⚠ A block, because the fix has to have somewhere to land. An expression-bodied
    ///     <c>Dispose</c>, an abstract or extern one, and a <c>Dispose</c> inherited rather than
    ///     declared here are all the same bug and none is reported: doc 08's bar is a finding an agent
    ///     can act on, and a report whose repair this rule cannot write is one it declines to make.
    /// </remarks>
    static BlockSyntax? DisposeBodyOf(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol owner,
        TypeDeclarationSyntax declaration
    ) {
        foreach (var member in owner.GetMembers("Dispose")) {
            if (member is not IMethodSymbol { ReturnsVoid: true, Parameters.IsEmpty: true, IsStatic: false } method) {
                continue;
            }

            foreach (var reference in method.DeclaringSyntaxReferences) {
                if (reference.GetSyntax(context.CancellationToken) is MethodDeclarationSyntax { Body: { } block }
                    && block.Ancestors().Contains(declaration)) {
                    return block;
                }
            }
        }

        return null;
    }

    /// <summary>
    ///     Whether no reference to the field anywhere in the declaration releases it or hands it on.
    /// </summary>
    /// <remarks>
    ///     ⚠ The default is "something does". A reference shape this method does not recognise
    ///     withdraws the finding, so a construct nobody thought about costs a missed report rather than
    ///     a wrong one — the same default <c>SK3501</c> takes, and for the same reason.
    /// </remarks>
    static bool NothingReleasesIt(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax declaration,
        IFieldSymbol field,
        VariableDeclaratorSyntax declarator
    ) {
        foreach (var identifier in declaration.DescendantNodes().OfType<IdentifierNameSyntax>()) {
            if (!string.Equals(identifier.Identifier.ValueText, field.Name, StringComparison.Ordinal)
                || declarator.Span.Contains(identifier.Span)) {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol,
                    field
                )) {
                continue;
            }

            // ⚠ `this.stream` is one reference, not two. Reading the identifier alone would make
            // every `this.`-qualified codebase withdraw on its first ordinary member read.
            var reference = (ExpressionSyntax)identifier;
            if (reference.Parent is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } qualified
                && ReferenceEquals(qualified.Name, reference)) {
                reference = qualified;
            }

            while (reference.Parent is ParenthesizedExpressionSyntax
                   or PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression }) {
                reference = (ExpressionSyntax)reference.Parent;
            }

            switch (reference.Parent) {
                case MemberAccessExpressionSyntax access when ReferenceEquals(access.Expression, reference):
                    if (IsDisposal(access.Name.Identifier.ValueText)) {
                        return false;
                    }

                    continue;

                case ElementAccessExpressionSyntax element when ReferenceEquals(element.Expression, reference):
                    continue;

                // `stream?.Dispose()` — the member sits in a binding under `WhenNotNull`.
                case ConditionalAccessExpressionSyntax conditional
                    when ReferenceEquals(conditional.Expression, reference):
                    foreach (var binding in conditional.WhenNotNull.DescendantNodesAndSelf()
                                 .OfType<MemberBindingExpressionSyntax>()) {
                        if (IsDisposal(binding.Name.Identifier.ValueText)) {
                            return false;
                        }
                    }

                    continue;

                // ⚠ Anything else — an argument, an assignment, a cast, a `return`, an interpolation
                // — is a way the resource can leave this type, and the rule cannot follow it.
                default:
                    return false;
            }
        }

        return true;
    }

    static bool IsDisposal(string name) => name is "Dispose" or "DisposeAsync" or "Close";

    /// <summary>
    ///     ⚠ The disposal goes first in the block, where the pattern puts owned resources.
    /// </summary>
    /// <remarks>
    ///     The empty body is a separate edit because inserting after the brace of <c>{ }</c> leaves the
    ///     closing brace on the statement's line — text that parses and that nobody would have written.
    /// </remarks>
    static ImmutableDictionary<string, string?> Insert(BlockSyntax body, string name) {
        if (body.Statements.Count > 0) {
            var first = body.Statements[0];
            return FixEdits.Pack(
                (new TextSpan(first.SpanStart, 0), name + ".Dispose();\n" + UsingResource.IndentOf(first))
            );
        }

        var indent = UsingResource.IndentOf(body.Parent ?? body);
        return FixEdits.Pack(
            (
                TextSpan.FromBounds(body.OpenBraceToken.Span.End, body.CloseBraceToken.SpanStart),
                "\n" + indent + "    " + name + ".Dispose();\n" + indent
            )
        );
    }
}
