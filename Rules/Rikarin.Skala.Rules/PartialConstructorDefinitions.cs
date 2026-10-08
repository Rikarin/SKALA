using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Rikarin.Skala.Rules;

/// <summary>
///     Hands every syntax-node action the partial constructor definitions Roslyn's driver never
///     dispatches (#401).
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         On <c>Microsoft.CodeAnalysis.CSharp</c> 5.9 no syntax-node action, symbol action or
///         symbol-start node action is ever given a C# 14 partial constructor's <em>defining</em>
///         declaration, nor any node inside it.
///     </b>
///     Measured with a probe analyzer over
///     <c>partial C(int x); partial C(int x) { }</c>: the implementation, its parameter and every other
///     partial kind's definition were visited; the definition and its parameter were not, and the
///     symbol action saw only the implementation's symbol. A syntax-tree action and a semantic-model
///     action do see the node, and <c>GetDeclaredSymbol</c> on it answers with the definition's symbol
///     (<c>IsPartialDefinition</c>, a <c>PartialImplementationPart</c>). So the node is in the tree and
///     bound; it is the driver's per-symbol dispatch that skips it — the definition never arrives as a
///     declared symbol of its own, unlike a partial method's, property's, indexer's or event's.
///     <para>
///         ⚠ The loss is not confined to the constructor rules. Run by hand over the definition, every
///         registered action that would have fired is a finding the driver drops: an orphan
///         <c>&lt;inheritdoc/&gt;</c> and a <c>&lt;returns&gt;</c> (SK7103, SK7102), an attribute with no
///         justification or message (SK7051, SK7070, SK7071), a redundant <c>[SetsRequiredMembers]</c>
///         (SK0281), a redundant attribute detail or empty argument list (SK0261, SK0233), a qualifier
///         or <c>Nullable&lt;T&gt;</c> in a parameter type (SK0243, SK1040). The doc comment, the
///         attributes and the defaults of a partial constructor are written on the definition by
///         convention, which is why the half the driver skips is the half that says most.
///     </para>
///     <para>
///         ⚠ <b>So the fix is in the registration, not in each rule.</b> <see cref="Visiting" /> wraps an
///         analyzer's <see cref="AnalysisContext" />: every syntax-node action registered through it,
///         directly or in a compilation-start action, is also run over the nodes of every partial
///         constructor definition in a tree, from one semantic-model action per tree. Per tree is what
///         keeps it sound under the per-file diagnostic cache: what it reports is in the file being
///         analysed and depends on nothing a <c>Syntax</c>- or <c>Semantic</c>-scoped rule could not
///         already read there.
///     </para>
///     <para>
///         ⚠ And the wrapped node action declines a node inside a partial constructor definition, so a
///         Roslyn that starts dispatching them does not double every finding: exactly one of the two
///         paths reports a definition, whichever version of the driver runs.
///     </para>
///     <para>
///         ⚠ Not covered, and said so: an operation action and a node action nested in a symbol-start
///         action. A definition holds no statements, and the operations it has — a default value, an
///         attribute argument — are constants no shipped operation rule inspects there.
///     </para>
/// </remarks>
public static class PartialConstructorDefinitions {
    static readonly ConditionalWeakTable<SyntaxTree, Box> Found = new();

    /// <summary>
    ///     The context to register through, so that the analyzer's node actions also reach partial
    ///     constructor definitions.
    /// </summary>
    public static AnalysisContext Visiting(AnalysisContext context) => new Root(context);

    /// <summary>Every partial constructor definition in a tree, found once per tree.</summary>
    /// <remarks>
    ///     Members are only ever children of a compilation unit, a namespace or a type, so the walk never
    ///     descends into a member: it is proportional to the number of declarations, not of nodes.
    /// </remarks>
    public static ImmutableArray<ConstructorDeclarationSyntax> In(SyntaxTree tree, CancellationToken cancellation) =>
        Found.GetValue(tree, key => new(Collect(key.GetRoot(cancellation)))).Definitions;

    /// <summary>Whether a node is, or is inside, a partial constructor's defining declaration.</summary>
    public static bool IsInADefinition(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            if (current is ConstructorDeclarationSyntax constructor) {
                return PartialMembers.IsDefinition(constructor);
            }

            if (current is MemberDeclarationSyntax) {
                return false;
            }
        }

        return false;
    }

    static ImmutableArray<ConstructorDeclarationSyntax> Collect(SyntaxNode root) {
        var builder = ImmutableArray.CreateBuilder<ConstructorDeclarationSyntax>();
        foreach (var node in root.DescendantNodes(static node =>
                     node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax
                 )) {
            if (node is ConstructorDeclarationSyntax constructor && PartialMembers.IsDefinition(constructor)) {
                builder.Add(constructor);
            }
        }

        return builder.ToImmutable();
    }

    sealed class Box(ImmutableArray<ConstructorDeclarationSyntax> definitions) {
        public ImmutableArray<ConstructorDeclarationSyntax> Definitions { get; } = definitions;
    }

    /// <summary>
    ///     The node actions one context collected, and the semantic-model action that runs them over the
    ///     definitions — registered once, on the first node action.
    /// </summary>
    sealed class Dispatch {
        readonly List<(Action<SyntaxNodeAnalysisContext> Action, ImmutableArray<SyntaxKind> Kinds)> actions = [];

        public bool Add(Action<SyntaxNodeAnalysisContext> action, ImmutableArray<SyntaxKind> kinds) {
            actions.Add((action, kinds));
            return actions.Count == 1;
        }

        public static Action<SyntaxNodeAnalysisContext> Guarded(Action<SyntaxNodeAnalysisContext> action) =>
            context => {
                // ⚠ The driver does not give us these today; if it ever does, the dispatch below already
                // has, and running them twice would report every finding twice.
                if (context.ContainingSymbol is IMethodSymbol {
                        MethodKind: MethodKind.Constructor, IsPartialDefinition: true
                    }
                    || (context.ContainingSymbol is IMethodSymbol { MethodKind: MethodKind.Constructor }
                        && IsInADefinition(context.Node))) {
                    return;
                }

                action(context);
            };

        public void Run(SemanticModelAnalysisContext context) {
            var definitions = In(context.SemanticModel.SyntaxTree, context.CancellationToken);
            if (definitions.IsEmpty) {
                return;
            }

            foreach (var definition in definitions) {
                if (context.FilterSpan is { } filter && !filter.IntersectsWith(definition.FullSpan)) {
                    continue;
                }

                var symbol = context.SemanticModel.GetDeclaredSymbol(definition, context.CancellationToken);
                foreach (var node in definition.DescendantNodesAndSelf()) {
                    var kind = node.Kind();
                    foreach (var (action, kinds) in actions) {
                        if (!kinds.Contains(kind)) {
                            continue;
                        }

                        // ⚠ The public constructor is marked obsolete because it is meant for tests; it is
                        // the only way to hand a registered action a node the driver did not, and the
                        // diagnostics it reports go through the semantic-model context's own reporter,
                        // which validates them against the analyzer and applies suppressions as usual.
#pragma warning disable CS0618
                        action(
                            new SyntaxNodeAnalysisContext(
                                node,
                                symbol,
                                context.SemanticModel,
                                context.Options,
                                context.ReportDiagnostic,
                                static _ => true,
                                context.CancellationToken
                            )
                        );
#pragma warning restore CS0618
                    }
                }
            }
        }
    }

    /// <summary>
    ///     ⚠ Defers every registration into one compilation-start action, so that <see cref="Start" /> is
    ///     the only place a node action is wrapped and one dispatch serves a whole analyzer: an action
    ///     registered in <c>Initialize</c> and one registered at compilation start are the same action
    ///     to the driver.
    /// </summary>
    sealed class Root(AnalysisContext inner) : AnalysisContext {
        readonly List<Action<CompilationStartAnalysisContext>> deferred = [];

        public override void EnableConcurrentExecution() => inner.EnableConcurrentExecution();

        public override void ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags analysisMode) =>
            inner.ConfigureGeneratedCodeAnalysis(analysisMode);

        public override void RegisterCompilationStartAction(Action<CompilationStartAnalysisContext> action) =>
            Defer(action);

        public override void RegisterCompilationAction(Action<CompilationAnalysisContext> action) =>
            Defer(start => start.RegisterCompilationEndAction(action));

        public override void RegisterSemanticModelAction(Action<SemanticModelAnalysisContext> action) =>
            Defer(start => start.RegisterSemanticModelAction(action));

        public override void RegisterSymbolAction(
            Action<SymbolAnalysisContext> action,
            ImmutableArray<SymbolKind> kinds
        ) =>
            Defer(start => start.RegisterSymbolAction(action, kinds));

        public override void RegisterSymbolStartAction(Action<SymbolStartAnalysisContext> action, SymbolKind kind) =>
            Defer(start => start.RegisterSymbolStartAction(action, kind));

        public override void RegisterCodeBlockStartAction<TKind>(Action<CodeBlockStartAnalysisContext<TKind>> action) =>
            Defer(start => start.RegisterCodeBlockStartAction(action));

        public override void RegisterCodeBlockAction(Action<CodeBlockAnalysisContext> action) =>
            Defer(start => start.RegisterCodeBlockAction(action));

        public override void RegisterSyntaxTreeAction(Action<SyntaxTreeAnalysisContext> action) =>
            Defer(start => start.RegisterSyntaxTreeAction(action));

        public override void RegisterAdditionalFileAction(Action<AdditionalFileAnalysisContext> action) =>
            Defer(start => start.RegisterAdditionalFileAction(action));

        public override void RegisterOperationAction(
            Action<OperationAnalysisContext> action,
            ImmutableArray<OperationKind> kinds
        ) =>
            Defer(start => start.RegisterOperationAction(action, kinds));

        public override void RegisterOperationBlockStartAction(Action<OperationBlockStartAnalysisContext> action) =>
            Defer(start => start.RegisterOperationBlockStartAction(action));

        public override void RegisterOperationBlockAction(Action<OperationBlockAnalysisContext> action) =>
            Defer(start => start.RegisterOperationBlockAction(action));

        public override void RegisterSyntaxNodeAction<TKind>(
            Action<SyntaxNodeAnalysisContext> action,
            ImmutableArray<TKind> kinds
        ) =>
            Defer(start => start.RegisterSyntaxNodeAction(action, kinds));

        void Defer(Action<CompilationStartAnalysisContext> registration) {
            deferred.Add(registration);
            if (deferred.Count > 1) {
                return;
            }

            inner.RegisterCompilationStartAction(start => {
                    var wrapped = new Start(start);
                    foreach (var each in deferred) {
                        each(wrapped);
                    }
                }
            );
        }
    }

    // RS1012 reads the forwarded start context as a start action that registers nothing; every
    // registration below is forwarded to it.
#pragma warning disable RS1012
    sealed class Start(CompilationStartAnalysisContext inner)
        : CompilationStartAnalysisContext(inner.Compilation, inner.Options, inner.CancellationToken) {
        readonly Dispatch dispatch = new();

        public override void RegisterCompilationEndAction(Action<CompilationAnalysisContext> action) =>
            inner.RegisterCompilationEndAction(action);

        public override void RegisterSemanticModelAction(Action<SemanticModelAnalysisContext> action) =>
            inner.RegisterSemanticModelAction(action);

        public override void RegisterSymbolAction(
            Action<SymbolAnalysisContext> action,
            ImmutableArray<SymbolKind> symbolKinds
        ) =>
            inner.RegisterSymbolAction(action, symbolKinds);

        public override void RegisterSymbolStartAction(
            Action<SymbolStartAnalysisContext> action,
            SymbolKind symbolKind
        ) =>
            inner.RegisterSymbolStartAction(action, symbolKind);

        public override void RegisterCodeBlockStartAction<TLanguageKindEnum>(
            Action<CodeBlockStartAnalysisContext<TLanguageKindEnum>> action
        ) =>
            inner.RegisterCodeBlockStartAction(action);

        public override void RegisterCodeBlockAction(Action<CodeBlockAnalysisContext> action) =>
            inner.RegisterCodeBlockAction(action);

        public override void RegisterSyntaxTreeAction(Action<SyntaxTreeAnalysisContext> action) =>
            inner.RegisterSyntaxTreeAction(action);

        public override void RegisterAdditionalFileAction(Action<AdditionalFileAnalysisContext> action) =>
            inner.RegisterAdditionalFileAction(action);

        public override void RegisterOperationAction(
            Action<OperationAnalysisContext> action,
            ImmutableArray<OperationKind> operationKinds
        ) =>
            inner.RegisterOperationAction(action, operationKinds);

        public override void RegisterOperationBlockStartAction(Action<OperationBlockStartAnalysisContext> action) =>
            inner.RegisterOperationBlockStartAction(action);

        public override void RegisterOperationBlockAction(Action<OperationBlockAnalysisContext> action) =>
            inner.RegisterOperationBlockAction(action);

        public override void RegisterSyntaxNodeAction<TLanguageKindEnum>(
            Action<SyntaxNodeAnalysisContext> action,
            ImmutableArray<TLanguageKindEnum> syntaxKinds
        ) {
            inner.RegisterSyntaxNodeAction(Dispatch.Guarded(action), syntaxKinds);
            if (syntaxKinds is ImmutableArray<SyntaxKind> kinds && dispatch.Add(action, kinds)) {
                inner.RegisterSemanticModelAction(dispatch.Run);
            }
        }
    }
#pragma warning restore RS1012
}
