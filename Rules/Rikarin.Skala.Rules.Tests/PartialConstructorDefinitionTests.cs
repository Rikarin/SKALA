using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     #401: Roslyn's driver never dispatches a partial constructor's defining declaration, and
///     <see cref="PartialConstructorDefinitions" /> is what dispatches it instead.
/// </summary>
public sealed class PartialConstructorDefinitionTests {
    const string Shapes = """
                          public sealed partial class C {
                              public partial C(int x);

                              public partial C(int x) { }

                              public partial int P { get; }

                              public partial int P => 1;

                              public partial void M(int y);

                              public partial void M(int y) { }
                          }
                          """;

    /// <summary>
    ///     ⚠ The measurement, kept as a test: if this goes red, Roslyn has started visiting the
    ///     definition, and the guard in <see cref="PartialConstructorDefinitions" /> is what stops every
    ///     finding being reported twice — so read it before deleting anything.
    /// </summary>
    [Fact]
    public void TheDriver_NeverVisitsAPartialConstructorDefinition() {
        var seen = Visits(new Counter(wrapped: false));
        Assert.DoesNotContain("ConstructorDeclaration@1", seen);
        Assert.DoesNotContain("Parameter@1", seen);
        Assert.Contains("ConstructorDeclaration@3", seen);
        Assert.Contains("MethodDeclaration@9", seen);
    }

    /// <summary>Through the wrapper every declaration and parameter is visited exactly once.</summary>
    [Fact]
    public void ThroughTheWrapper_EveryDeclarationIsVisitedExactlyOnce() {
        var seen = Visits(new Counter(wrapped: true));
        foreach (var expected in new[] {
                     "ConstructorDeclaration@1", "Parameter@1", "ConstructorDeclaration@3", "Parameter@3",
                     "MethodDeclaration@9", "MethodDeclaration@11"
                 }) {
            Assert.Single(seen, expected);
        }
    }

    /// <summary>
    ///     ⚠ The guard, exercised the only way it can be today: by doing what a fixed driver would do,
    ///     handing the wrapped action the definition itself. It must decline, because the semantic-model
    ///     dispatch already ran it.
    /// </summary>
    [Fact]
    public void AWrappedAction_DeclinesADefinitionTheDriverHandsIt() {
        var compilation = RuleFixtures.Compile(Shapes, "Shapes.cs");
        var recorder = new Recorder(compilation);
        var calls = new List<SyntaxNode>();
        PartialConstructorDefinitions.Visiting(new Root(recorder))
            .RegisterSyntaxNodeAction(context => calls.Add(context.Node), SyntaxKind.ConstructorDeclaration);

        var tree = compilation.SyntaxTrees[0];
        var model = compilation.GetSemanticModel(tree);
        var constructors = tree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .ToArray();
        foreach (var constructor in constructors) {
            var symbol = model.GetDeclaredSymbol(constructor, TestContext.Current.CancellationToken);
            foreach (var (action, _) in recorder.Nodes) {
#pragma warning disable CS0618
                action(new(constructor, symbol, model, new([]), static _ => { }, static _ => true, default));
#pragma warning restore CS0618
            }
        }

        Assert.Equal([constructors[1]], calls);
    }

    /// <summary>
    ///     The completeness claim: every shipped analyzer that registers a syntax-node action registers
    ///     it through <see cref="PartialConstructorDefinitions.Visiting" />, directly or inside a
    ///     compilation-start action. A rule added without it is blind to every partial constructor
    ///     definition, and nothing else would say so.
    /// </summary>
    [Fact]
    public void EveryAnalyzerWithANodeAction_ReachesPartialConstructorDefinitions() {
        var compilation = RuleFixtures.Compile("class C { }", "Empty.cs");
        var missing = new List<string>();
        foreach (var analyzer in SkalaAnalyzers.All) {
            var recorder = new Recorder(compilation);
            analyzer.Initialize(new Root(recorder));
            if (recorder.Nodes.Count > 0 && !recorder.Dispatched) {
                missing.Add(analyzer.GetType().Name);
            }
        }

        Assert.Empty(missing);
    }

    static ImmutableArray<string> Visits(Counter counter) {
        var compilation = RuleFixtures.Compile(Shapes, "Shapes.cs");
        Assert.Empty(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        );
        RuleFixtures.Analyze(compilation, [counter], TestContext.Current.CancellationToken);
        return [.. counter.Seen];
    }

#pragma warning disable RS1001, RS1036, RS1038, RS1041, RS2008
    sealed class Counter(bool wrapped) : DiagnosticAnalyzer {
        static readonly DiagnosticDescriptor Descriptor = new(
            "PRB401",
            "probe",
            "probe",
            "probe",
            DiagnosticSeverity.Warning,
            true
        );

        public ConcurrentBag<string> Seen { get; } = [];

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Descriptor];

        public override void Initialize(AnalysisContext context) {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            var registrar = wrapped ? PartialConstructorDefinitions.Visiting(context) : context;
            registrar.RegisterCompilationStartAction(start => start.RegisterSyntaxNodeAction(
                    node => Seen.Add(node.Node.Kind() + "@" + node.Node.GetLocation().GetLineSpan().StartLinePosition.Line),
                    SyntaxKind.ConstructorDeclaration,
                    SyntaxKind.MethodDeclaration,
                    SyntaxKind.Parameter
                )
            );
        }
    }
#pragma warning restore RS1001, RS1036, RS1038, RS1041, RS2008

    /// <summary>Records what an analyzer registers, and whether the dispatch is among it.</summary>
    sealed class Recorder(Compilation compilation)
        : CompilationStartAnalysisContext(compilation, new([]), CancellationToken.None) {
        public List<(Action<SyntaxNodeAnalysisContext> Action, ImmutableArray<SyntaxKind> Kinds)> Nodes { get; } = [];

        public bool Dispatched { get; private set; }

        public override void RegisterCompilationEndAction(Action<CompilationAnalysisContext> action) { }

        public override void RegisterSemanticModelAction(Action<SemanticModelAnalysisContext> action) =>
            Dispatched |= action.Method.DeclaringType?.DeclaringType == typeof(PartialConstructorDefinitions);

        public override void RegisterSymbolAction(
            Action<SymbolAnalysisContext> action,
            ImmutableArray<SymbolKind> symbolKinds
        ) { }

        public override void RegisterSymbolStartAction(Action<SymbolStartAnalysisContext> action, SymbolKind symbolKind) { }

        public override void RegisterCodeBlockStartAction<TLanguageKindEnum>(
            Action<CodeBlockStartAnalysisContext<TLanguageKindEnum>> action
        ) { }

        public override void RegisterCodeBlockAction(Action<CodeBlockAnalysisContext> action) { }

        public override void RegisterSyntaxTreeAction(Action<SyntaxTreeAnalysisContext> action) { }

        public override void RegisterOperationAction(
            Action<OperationAnalysisContext> action,
            ImmutableArray<OperationKind> operationKinds
        ) { }

        public override void RegisterOperationBlockStartAction(Action<OperationBlockStartAnalysisContext> action) { }

        public override void RegisterOperationBlockAction(Action<OperationBlockAnalysisContext> action) { }

        public override void RegisterSyntaxNodeAction<TLanguageKindEnum>(
            Action<SyntaxNodeAnalysisContext> action,
            ImmutableArray<TLanguageKindEnum> syntaxKinds
        ) => Nodes.Add((action, [.. syntaxKinds.Cast<SyntaxKind>()]));
    }

    sealed class Root(Recorder inner) : AnalysisContext {
        public override void EnableConcurrentExecution() { }

        public override void ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags analysisMode) { }

        public override void RegisterCompilationStartAction(Action<CompilationStartAnalysisContext> action) =>
            action(inner);

        public override void RegisterCompilationAction(Action<CompilationAnalysisContext> action) { }

        public override void RegisterSemanticModelAction(Action<SemanticModelAnalysisContext> action) =>
            inner.RegisterSemanticModelAction(action);

        public override void RegisterSymbolAction(
            Action<SymbolAnalysisContext> action,
            ImmutableArray<SymbolKind> symbolKinds
        ) { }

        public override void RegisterSymbolStartAction(Action<SymbolStartAnalysisContext> action, SymbolKind symbolKind) { }

        public override void RegisterCodeBlockStartAction<TLanguageKindEnum>(
            Action<CodeBlockStartAnalysisContext<TLanguageKindEnum>> action
        ) { }

        public override void RegisterCodeBlockAction(Action<CodeBlockAnalysisContext> action) { }

        public override void RegisterSyntaxTreeAction(Action<SyntaxTreeAnalysisContext> action) { }

        public override void RegisterOperationAction(
            Action<OperationAnalysisContext> action,
            ImmutableArray<OperationKind> operationKinds
        ) { }

        public override void RegisterOperationBlockStartAction(Action<OperationBlockStartAnalysisContext> action) { }

        public override void RegisterOperationBlockAction(Action<OperationBlockAnalysisContext> action) { }

        public override void RegisterSyntaxNodeAction<TLanguageKindEnum>(
            Action<SyntaxNodeAnalysisContext> action,
            ImmutableArray<TLanguageKindEnum> syntaxKinds
        ) => inner.RegisterSyntaxNodeAction(action, syntaxKinds);
    }
}
