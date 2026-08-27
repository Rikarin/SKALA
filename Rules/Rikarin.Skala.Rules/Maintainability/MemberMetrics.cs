using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
/// Every per-member metric docs/plan/07-analysis-host.md § "Metrics" defines, for one member.
/// </summary>
/// <remarks>
/// A record rather than seven out parameters because the whole point of
/// <see cref="MemberMetrics.Compute"/> is that these numbers come out of <em>one</em> traversal and
/// therefore cannot disagree with each other.
/// </remarks>
public sealed record MemberMetricValues {
    /// <summary>
    /// Linearly independent paths through the member: <c>edges − blocks + 2</c> over Roslyn's
    /// <see cref="ControlFlowGraph"/>, or the syntactic decision-point count when there was no
    /// semantic model. <c>SK7001</c>.
    /// </summary>
    public int Cyclomatic { get; init; }

    /// <summary>
    /// ⚠ Whether <see cref="Cyclomatic"/> came from the control-flow graph or from the syntactic
    /// fallback. A consumer that aggregates the number across a run needs to know which, because a
    /// <c>--load=loose</c> run has no semantic model and the two are not guaranteed equal on every
    /// shape.
    /// </summary>
    public bool CyclomaticFromControlFlowGraph { get; init; }

    /// <summary>Sonar's cognitive complexity. <c>SK7002</c>.</summary>
    public int Cognitive { get; init; }

    /// <summary>Statements in the member's bodies — not lines. <c>SK7003</c>.</summary>
    public int Statements { get; init; }

    /// <summary>Declared parameters, primary-constructor parameters included. <c>SK7005</c>.</summary>
    public int Parameters { get; init; }

    /// <summary>Deepest block nesting; a lambda body restarts the count. <c>SK7006</c>.</summary>
    public int NestingDepth { get; init; }
}

/// <summary>
/// A type declaration's size, as docs/plan/07 § "Metrics" defines it: "members, and fields
/// separately". <c>SK7004</c>.
/// </summary>
/// <remarks>
/// ⚠ Per <em>declaration</em>, not per symbol, so a partial type is measured once per file — which
/// is the file a person opens. rules.json's <c>SK7004</c> rationale says so explicitly.
/// </remarks>
public sealed record TypeMetricValues {
    /// <summary>Everything the type declares that is not a field. The rule fires on this.</summary>
    public int Members { get; init; }

    /// <summary>
    /// Field declarators, counted separately: "forty fields is a data carrier, which may be exactly
    /// right, while forty methods is a type doing forty jobs".
    /// </summary>
    public int Fields { get; init; }
}

/// <summary>
/// One walker, every metric, one visit.
/// </summary>
/// <remarks>
/// ⚠ docs/plan/07 § "Metrics": the metrics are "computed in the same pass, from the same trees,
/// because a second traversal of 1.35 M lines to count things is a second traversal". That is the
/// load-bearing design constraint of this file: there is exactly one
/// <see cref="Microsoft.CodeAnalysis.CSharp.CSharpSyntaxWalker"/> and it computes cognitive
/// complexity, statement count, nesting depth and the syntactic cyclomatic count together.
/// <para>
/// ⚠ It is public because <c>Analysis</c> folds the same numbers into the run's aggregates and must
/// call exactly this. Two implementations of "how big is this method" is a way for the aggregate in
/// the report and the number in the finding to disagree, and a reader who notices that stops
/// believing both.
/// </para>
/// </remarks>
public static class MemberMetrics {
    /// <summary>
    /// The diagnostic property every <c>SK70xx</c> metric finding carries its measurement under, so
    /// a reader can see the number without re-deriving it.
    /// </summary>
    public const string ValueKey = "skala.metric.value";

    /// <summary>
    /// Every metric for one member, in a single visit.
    /// </summary>
    /// <param name="member">
    /// A member declaration, a local function, or a type declaration (for which only
    /// <see cref="MemberMetricValues.Parameters"/> — the primary constructor's — is meaningful).
    /// </param>
    /// <param name="model">
    /// ⚠ May be null. Cyclomatic complexity then falls back to the syntactic decision-point count
    /// and <see cref="MemberMetricValues.CyclomaticFromControlFlowGraph"/> says so. Every other
    /// metric here is purely syntactic and is unaffected.
    /// </param>
    /// <param name="cancellation">Every stage takes one (docs/plan/07 § "Cancellation").</param>
    public static MemberMetricValues Compute(SyntaxNode member, SemanticModel? model, CancellationToken cancellation) {
        if (member is null) {
            throw new ArgumentNullException(nameof(member));
        }

        var parameters = ParameterCount(member);

        // ⚠ A type declaration is not a body. Walking it here would count every member's statements
        // into the type and then count them again when the member itself is measured, and the two
        // surfaces that read these numbers would both be wrong.
        if (member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax) {
            return new MemberMetricValues {
                Cyclomatic = 1, CyclomaticFromControlFlowGraph = false, Parameters = parameters
            };
        }

        var walker = new MetricsWalker(RecursionName(member), parameters, cancellation);
        walker.Visit(member);

        var cognitive = walker.Cognitive;

        // ⚠ Sonar adds the recursion increment once per method, not once per recursive call site,
        // and only for a method declaration — CSharpCognitiveComplexityMetric.VisitMethodDeclaration.
        if (walker.HasDirectRecursiveCall && member is MethodDeclarationSyntax) {
            cognitive++;
        }

        var cyclomatic = walker.DecisionPoints + 1;
        var fromGraph = false;
        if (model is not null && TryCyclomaticFromControlFlowGraph(member, model, cancellation, out var measured)) {
            cyclomatic = measured;
            fromGraph = true;
        }

        return new MemberMetricValues {
            Cyclomatic = cyclomatic,
            CyclomaticFromControlFlowGraph = fromGraph,
            Cognitive = cognitive,
            Statements = walker.Statements,
            Parameters = parameters,
            NestingDepth = walker.MaxBlockDepth
        };
    }

    /// <summary>How many members a type declaration declares, with fields counted separately.</summary>
    /// <remarks>
    /// ⚠ Deliberately does not descend: a nested type counts as one member of its container and is
    /// measured again on its own declaration.
    /// </remarks>
    public static TypeMetricValues ComputeTypeSize(SyntaxNode type, CancellationToken cancellation) {
        if (type is not TypeDeclarationSyntax declaration) {
            return new TypeMetricValues();
        }

        var members = 0;
        var fields = 0;
        foreach (var member in declaration.Members) {
            cancellation.ThrowIfCancellationRequested();

            // ⚠ `int a, b, c;` is three fields. They are three things to initialise, to name and to
            // keep consistent, and a type that hides thirty of them behind ten declarations is not
            // smaller than one that does not.
            if (member is BaseFieldDeclarationSyntax field) {
                fields += field.Declaration.Variables.Count;
                continue;
            }

            members++;
        }

        return new TypeMetricValues { Members = members, Fields = fields };
    }

    /// <summary>
    /// Whether a declaration is part of the publicly visible surface, through its whole containing
    /// chain. <c>SK7010</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ `public` only, and rules.json's <c>SK7010</c> false-positive note is the reason: the rule
    /// "counts only members that are publicly visible through their whole containing chain". A
    /// `protected` member of a public class is reachable by a derived type and not by a caller, and
    /// including it is the difference between a metric a library author recognises and one that
    /// fires on every template method. A repository that wants the wider surface has
    /// <c>SK6001</c>.
    /// </remarks>
    public static bool IsPublicApi(SyntaxNode declaration) {
        for (var node = declaration; node is not null; node = node.Parent) {
            switch (node) {
                case BaseNamespaceDeclarationSyntax:
                case CompilationUnitSyntax:
                    return true;

                case MemberDeclarationSyntax member when !IsDeclaredPublic(member):
                    return false;

                case MemberDeclarationSyntax:
                    continue;

                default:
                    // A local function, a statement, an accessor: not API at all.
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether <c>SK7010</c> has anything to say about this declaration at all.
    /// </summary>
    /// <remarks>
    /// The exclusions are rules.json's, verbatim: accessors, explicit interface implementations,
    /// operators, finalizers and <c>record</c> positional members are not things a person writes a
    /// <c>&lt;summary&gt;</c> for. Fields are excluded too — a public constant's name is its
    /// documentation far more often than not, and including them is how the metric becomes noise.
    /// </remarks>
    public static bool IsDocumentable(SyntaxNode declaration) =>
        declaration switch {
            MethodDeclarationSyntax method => method.ExplicitInterfaceSpecifier is null,
            PropertyDeclarationSyntax property => property.ExplicitInterfaceSpecifier is null,
            IndexerDeclarationSyntax indexer => indexer.ExplicitInterfaceSpecifier is null,
            EventDeclarationSyntax @event => @event.ExplicitInterfaceSpecifier is null,
            BaseTypeDeclarationSyntax => true,
            DelegateDeclarationSyntax => true,
            ConstructorDeclarationSyntax => true,
            _ => false
        };

    /// <summary>
    /// Whether a declaration carries a documentation comment. <c>&lt;inheritdoc/&gt;</c> counts.
    /// </summary>
    /// <remarks>
    /// ⚠ rules.json: "treats an <c>&lt;inheritdoc/&gt;</c> as documentation". It is a deliberate
    /// statement that the base member's prose applies here, which is exactly what the metric is
    /// asking for; requiring the author to repeat it would make the rule a copy-paste generator.
    /// </remarks>
    public static bool HasDocumentation(SyntaxNode declaration) {
        foreach (var trivia in declaration.GetLeadingTrivia()) {
            if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)) {
                continue;
            }

            // ⚠ An empty `///` is not documentation, and a file that has a few of them would
            // otherwise measure as documented. Content is one XML element or one word of prose.
            if (trivia.GetStructure() is not DocumentationCommentTriviaSyntax structure) {
                continue;
            }

            foreach (var content in structure.Content) {
                switch (content) {
                    case XmlElementSyntax:
                    case XmlEmptyElementSyntax:
                        return true;

                    case XmlTextSyntax text when !string.IsNullOrWhiteSpace(
                        text.ToString()
                        .Replace("/", string.Empty)
                    ):
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>The declared parameters of anything that declares parameters.</summary>
    /// <remarks>
    /// ⚠ docs/plan/07 § "Metrics" says "including primary-constructor parameters", so a
    /// <see cref="TypeDeclarationSyntax"/> with a parameter list answers here: it is the type's
    /// constructor whatever the syntax. An extension method's <c>this</c> parameter is in the list
    /// and stays counted, because a caller supplies it.
    /// </remarks>
    static int ParameterCount(SyntaxNode member) =>
        member switch {
            BaseMethodDeclarationSyntax method => method.ParameterList.Parameters.Count,
            LocalFunctionStatementSyntax local => local.ParameterList.Parameters.Count,
            DelegateDeclarationSyntax @delegate => @delegate.ParameterList.Parameters.Count,
            IndexerDeclarationSyntax indexer => indexer.ParameterList.Parameters.Count,
            TypeDeclarationSyntax type => type.ParameterList?.Parameters.Count ?? 0,
            _ => 0
        };

    /// <summary>
    /// The name a direct recursive call would use, or null where Sonar does not look for one.
    /// </summary>
    static string? RecursionName(SyntaxNode member) =>
        member is MethodDeclarationSyntax method ? method.Identifier.ValueText : null;

    /// <summary>
    /// Cyclomatic complexity as <c>edges − blocks + 2</c> over the member's control-flow graphs.
    /// </summary>
    /// <remarks>
    /// ⚠ docs/plan/07 § "Metrics" specifies Roslyn's <see cref="ControlFlowGraph"/> — "basic blocks
    /// + conditional edges" — rather than a keyword count, so a <c>switch</c> expression, a
    /// <c>when</c> clause, a conditional access chain and a <c>&amp;&amp;</c> short-circuit are each
    /// counted the way the compiler sees them rather than the way a regular expression would.
    /// <para>
    /// In a well-formed graph every block but the exit has exactly one fall-through branch, so
    /// <c>E = (N − 1) + C</c> and the classic formula reduces to "one more than the number of
    /// conditional branches". Both are computed here; keeping the general form means an unreachable
    /// block or a graph shape Roslyn changes later does not silently move the number.
    /// </para>
    /// <para>
    /// ⚠ A member can have several bodies — a property's two accessors — and each has its own
    /// graph. Their decision counts are summed and the +1 is applied once, so a property whose
    /// getter and setter are both trivial scores 1 rather than 2.
    /// </para>
    /// </remarks>
    static bool TryCyclomaticFromControlFlowGraph(
        SyntaxNode member,
        SemanticModel model,
        CancellationToken cancellation,
        out int complexity
    ) {
        var decisions = 0;
        var any = false;

        foreach (var body in BodyRoots(member)) {
            cancellation.ThrowIfCancellationRequested();

            var graph = TryCreateGraph(body, model, cancellation);
            if (graph is null) {
                continue;
            }

            any = true;
            var blocks = 0;
            var edges = 0;
            foreach (var block in graph.Blocks) {
                blocks++;
                if (block.FallThroughSuccessor is not null) {
                    edges++;
                }

                if (block.ConditionalSuccessor is not null) {
                    edges++;
                }
            }

            decisions += edges - blocks + 1;
        }

        complexity = decisions + 1;
        return any;
    }

    static ControlFlowGraph? TryCreateGraph(SyntaxNode body, SemanticModel model, CancellationToken cancellation) {
        // ⚠ Ask for the operation rather than handing the syntax node to ControlFlowGraph.Create:
        // that overload throws for a node that is not an executable code-block root, and a throw
        // per member over 1.35 M lines is not a fallback, it is the run.
        var operation = model.GetOperation(body, cancellation);

        // ⚠ ControlFlowGraph.Create throws for an operation that is not a root — an accessor's body
        // block, for instance, hangs off the accessor's IMethodBodyOperation. A throw per member
        // over 1.35 M lines is not a fallback, it is the run, so the shape is checked rather than
        // caught.
        if (operation is null || operation.Parent is not null) {
            return null;
        }

        return operation switch {
            IMethodBodyOperation method => ControlFlowGraph.Create(method),
            IConstructorBodyOperation constructor => ControlFlowGraph.Create(constructor),
            IBlockOperation block => ControlFlowGraph.Create(block),
            _ => null
        };
    }

    /// <summary>The nodes under a member that Roslyn will give an executable operation for.</summary>
    static IEnumerable<SyntaxNode> BodyRoots(SyntaxNode member) {
        switch (member) {
            case BaseMethodDeclarationSyntax method:
                // One node covers both `{ … }` and `=> …`: Roslyn's IMethodBodyOperation carries
                // whichever the member has.
                if (method.Body is not null || method.ExpressionBody is not null) {
                    yield return method;
                }

                break;

            case LocalFunctionStatementSyntax local:
                if (local.Body is not null || local.ExpressionBody is not null) {
                    yield return local;
                }

                break;

            case BasePropertyDeclarationSyntax property: {
                // ⚠ A property is one member with up to two bodies, and each accessor is its own
                // executable root. Yielding the accessor rather than its block matters: the block
                // hangs off the accessor's operation and is not a root.
                if (property.AccessorList is not null) {
                    foreach (var accessor in property.AccessorList.Accessors) {
                        if (accessor.Body is not null || accessor.ExpressionBody is not null) {
                            yield return accessor;
                        }
                    }
                }

                var arrow = property switch {
                    PropertyDeclarationSyntax declared => declared.ExpressionBody,
                    IndexerDeclarationSyntax indexer => indexer.ExpressionBody,
                    _ => null
                };

                if (arrow is not null) {
                    yield return arrow;
                }

                break;
            }
        }
    }

    /// <summary>
    /// Whether a declaration's own modifiers make it public.
    /// </summary>
    /// <remarks>
    /// ⚠ The default matters and it is not uniform: an interface member with no modifier is public,
    /// and everything else with no modifier is not. Reading the modifier list alone and calling the
    /// absence "not public" would make every interface's surface invisible to the metric.
    /// </remarks>
    static bool IsDeclaredPublic(MemberDeclarationSyntax member) {
        var stated = false;
        foreach (var modifier in member.Modifiers) {
            switch (modifier.Kind()) {
                case SyntaxKind.PublicKeyword:
                    return true;

                case SyntaxKind.PrivateKeyword:
                case SyntaxKind.ProtectedKeyword:
                case SyntaxKind.InternalKeyword:
                case SyntaxKind.FileKeyword:
                    stated = true;
                    break;
            }
        }

        if (stated) {
            return false;
        }

        return member.Parent is InterfaceDeclarationSyntax && member is not BaseTypeDeclarationSyntax;
    }
}
