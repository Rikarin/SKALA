using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Diagnostics;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
/// Step 2 of docs/plan/04's pipeline: tree ⇒ tree, with the three safety layers of doc 06 § "Safety".
/// </summary>
/// <remarks>
/// ⚠ This never emits whitespace decisions of its own. Every rule rewrites nodes and the formatter
/// lays the result out (doc 06 § "Interaction with the formatter"), which is what makes
/// arrangement-and-format idempotent as a *pair* rather than only individually — see
/// <see cref="ArrangementPipeline"/>.
/// </remarks>
public static class Arranger {
    /// <summary>
    /// The catalogue, in the order it is applied.
    /// </summary>
    /// <remarks>
    /// ⚠ The order is load-bearing in exactly one place and inert everywhere else.
    /// <see cref="VarRule"/> must precede <see cref="ObjectCreationRule"/> and
    /// <see cref="DefaultValueRule"/>, because <c>var</c> consumes the left-hand type those two rules
    /// need as a target: run the other way round, <c>List&lt;int&gt; x = new List&lt;int&gt;()</c>
    /// becomes <c>List&lt;int&gt; x = new()</c> and then cannot become <c>var</c> at all, and the
    /// output disagrees with the oracle on every local declaration in the corpus.
    /// <para>
    /// <see cref="BodyStyleRule"/> runs last so that the expression it lifts into <c>=&gt;</c> is the
    /// already-arranged one, and the pair reaches a fixed point in one pass rather than two.
    /// </para>
    /// </remarks>
    public static ImmutableArray<ArrangementRule> Rules(ImmutableHashSet<string>? removableUsings = null) => [
        new AccessibilityRule(),
        new PredefinedTypeRule(),
        new VarRule(),
        new ObjectCreationRule(),
        new DefaultValueRule(),
        new NullCheckingPatternRule(),
        new EmptyStringRule(),
        new ThisQualifierRule(),
        new RedundantBracesRule(),
        new RedundantParenthesesRule(),
        new UsingsRule(removableUsings),
        new BodyStyleRule()
    ];

    /// <summary>Arranges text that has already been read, with options already resolved.</summary>
    /// <param name="compilation">
    /// The compilation the document belongs to, or null for the syntactic subset. ⚠ Null is not an
    /// error and not a degraded mode — it is <c>skala format --arrange=syntactic</c>, which is the
    /// contract an agent gets on a loose file with no project (doc 06 § "A few arrangements need no
    /// semantics").
    /// </param>
    public static ArrangementResult Arrange(
        string path,
        SourceText text,
        in ArrangementOptions options,
        CSharpCompilation? compilation = null,
        ImmutableHashSet<string>? removableUsings = null,
        string? crashRoot = null,
        ArrangementFilter? filter = null,
        CancellationToken cancellation = default
    ) {
        filter ??= ArrangementFilter.All;
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();
        if (GeneratedCode.IsGenerated(path, text)) {
            return new ArrangementResult(path, text.ToString(), [], [], ArrangementOutcome.Generated);
        }

        var scope = compilation is null ? ArrangementScope.Syntactic : options.Scope;
        var tree = FindTree(compilation, path, text, cancellation);
        foreach (var diagnostic in tree.GetDiagnostics(cancellation)) {
            if (diagnostic.Severity != DiagnosticSeverity.Error) {
                continue;
            }

            // ⚠ ADR-003, the same rule the formatter obeys: a file that does not parse is reported
            // and left byte-identical. Arranging a broken tree is how a broken file becomes a
            // differently broken one.
            diagnostics.Add(
                new SkalaDiagnostic(
                    FormatDiagnosticIds.NotParseable,
                    SkalaSeverity.Warning,
                    "not arranged, the file does not parse: "
                    + diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture),
                    path,
                    diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1
                )
            );

            return new ArrangementResult(
                path,
                text.ToString(),
                [],
                diagnostics.ToImmutable(),
                ArrangementOutcome.NotParseable
            );
        }

        var root = tree.GetRoot(cancellation);
        var originalModel = compilation?.GetSemanticModel(tree);
        var applied = ImmutableArray.CreateBuilder<string>();
        var current = root;

        // ⚠ The model, the tree it is valid for, and the compilation it came from move together.
        // Roslyn refuses — with "Syntax node is not within syntax tree" — to answer about a node that
        // is not in the model's own tree, and every rule after the first sees a tree some earlier
        // rule rebuilt. So a semantic rule that is about to run on a *changed* tree gets a fresh
        // model first.
        var boundTree = tree;
        var boundCompilation = compilation;
        var model = originalModel;

        // ⚠ `@formatter:off`. Computed once here and recomputed only after a rule has actually moved
        // something, because the spans are into the *current* tree and every firing rule shifts
        // them. An untagged file — almost every file — pays one trivia walk and then reuses
        // `FormatterTagGuard.Open` for all twelve rules.
        var guard = FormatterTagGuard.For(current, options.Tags);

        foreach (var rule in Rules(removableUsings)) {
            cancellation.ThrowIfCancellationRequested();
            if (!rule.IsEnabled(options) || !filter.Allows(rule)) {
                continue;
            }

            // ⚠ Layer 1, at the coarsest grain: a semantic rule does not run without semantics. The
            // per-rewrite preconditions live in the rules; this is the one that keeps the syntactic
            // subset honest about what it did not do.
            if (rule.NeedsSemantics && (scope != ArrangementScope.Full || boundCompilation is null)) {
                continue;
            }

            if (rule.NeedsSemantics
                && boundCompilation is not null
                && !ReferenceEquals(current, boundTree.GetRoot(cancellation))) {
                // ⚠ Costed deliberately: a re-bind per *firing* rule, not per rule. Most rules do
                // not fire on most files, so the common case is one or two — and the alternative,
                // collecting every rule's decisions against the original tree and applying them
                // together, means each rule has to reason about rewrites it cannot see.
                var reparsed = CSharpSyntaxTree.ParseText(
                    SourceText.From(current.ToFullString()),
                    (CSharpParseOptions)boundTree.Options,
                    path,
                    cancellation
                );

                boundCompilation = boundCompilation.ReplaceSyntaxTree(boundTree, reparsed);
                boundTree = reparsed;
                current = reparsed.GetRoot(cancellation);
                model = boundCompilation.GetSemanticModel(reparsed);

                // The re-parse produced a different tree object; the guard's spans point into the
                // old one.
                if (!guard.IsEmpty) {
                    guard = FormatterTagGuard.For(current, options.Tags);
                }
            }

            var rewritten = rule.Apply(new ArrangementContext(current, model, options, guard));
            if (ReferenceEquals(rewritten, current)) {
                continue;
            }

            // ⚠ The document-level half of the escape hatch. GuardedRewriter stops the twelve
            // rewriters node by node; this stops a rule that rebuilds nodes by hand and so never
            // passes through Visit at all — UsingsRule reorders the using block itself. A rule whose
            // output no longer contains a protected region verbatim is dropped whole. Silently: the
            // tag is an instruction, not an error, and a file that says "leave this alone" should
            // not also produce a diagnostic for being obeyed.
            if (!guard.IsEmpty && !guard.PreservesAll(rewritten.ToFullString())) {
                continue;
            }

            applied.Add(rule.Id);
            current = rewritten;

            if (!guard.IsEmpty) {
                guard = FormatterTagGuard.For(current, options.Tags);
            }
        }

        if (applied.Count == 0) {
            return new ArrangementResult(
                path,
                text.ToString(),
                [],
                diagnostics.ToImmutable(),
                ArrangementOutcome.Unchanged
            );
        }

        var arranged = current.ToFullString();

        // ⚠ Safety runs against the ORIGINAL compilation, tree and model, never the intermediate
        // ones the loop rebound. The question the layers ask is "did this document's meaning change
        // between what was on disk and what is about to be written" — comparing against an
        // intermediate state would let a rewrite that broke something and a later one that changed
        // the symptom cancel each other out.
        if (compilation is not null && originalModel is not null) {
            var failure = ArrangementSafety.Check(
                path,
                compilation,
                tree,
                root,
                arranged,
                originalModel,
                crashRoot,
                options,
                text.ToString(),
                cancellation
            );

            if (failure is not null) {
                diagnostics.Add(failure);
                return new ArrangementResult(
                    path,
                    text.ToString(),
                    [],
                    diagnostics.ToImmutable(),
                    ArrangementOutcome.Reverted
                );
            }
        }

        return new ArrangementResult(
            path,
            arranged,
            applied.ToImmutable(),
            diagnostics.ToImmutable(),
            ArrangementOutcome.Arranged
        );
    }

    /// <summary>
    /// The compilation's own tree for this file, or a fresh parse when it has none.
    /// </summary>
    /// <remarks>
    /// ⚠ The compilation's tree is preferred and it is not a micro-optimisation: a semantic model is
    /// only valid for a tree the compilation actually contains, so re-parsing the text and asking
    /// for a model over the new tree would silently answer about a different tree — or throw. When
    /// the text has been edited since the compilation was built (the fixed-point loop's second pass
    /// does exactly that) the caller passes a compilation it has already updated.
    /// </remarks>
    static SyntaxTree FindTree(
        CSharpCompilation? compilation,
        string path,
        SourceText text,
        CancellationToken cancellation
    ) {
        if (compilation is not null) {
            foreach (var candidate in compilation.SyntaxTrees) {
                if (string.Equals(candidate.FilePath, path, StringComparison.Ordinal)
                    && candidate.GetText(cancellation).ContentEquals(text)) {
                    return candidate;
                }
            }
        }

        return CSharpSyntaxTree.ParseText(text, CSharpFormatter.ParseOptions, path, cancellation);
    }
}
