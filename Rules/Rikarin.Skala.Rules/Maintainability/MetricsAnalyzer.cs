using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7001</c>–<c>SK7006</c> and <c>SK7010</c> — every per-member metric, from one visit.
/// </summary>
/// <remarks>
///     ⚠ <b>One analyzer, not seven.</b> docs/plan/07-analysis-host.md § "Metrics" requires the metrics
///     to be "computed in the same pass, from the same trees, because a second traversal of 1.35 M lines
///     to count things is a second traversal". Seven analyzers is seven registrations on the same node
///     kinds and therefore seven visits of every member in the repository; this one calls
///     <see cref="MemberMetrics.Compute" /> once and reports whichever thresholds that member breached.
///     <para>
///         ⚠ These are not rules that can have a false positive in the sense docs/plan/16 § R3 means: a
///         metric reports a measurement against a threshold and the measurement is either right or a bug.
///         What it can be is <em>useless</em> — a threshold low enough to fire on ordinary code teaches
///         people to switch the category off, which is R3's outcome by another route. So every default here
///         is the one rules.json documents, set well above the corpus's p99 rather than at the textbook
///         number, and every one of them is an <c>.editorconfig</c> option a repository can tighten
///         deliberately.
///     </para>
///     <para>
///         ⚠ Generated code is excluded — <see cref="GeneratedCodeAnalysisFlags.None" />, as the neighbouring
///         analyzers do. docs/plan/07 keeps generated code in the <em>aggregate</em> numbers, "because a
///         generator that emits 200 000 lines of pathological code is a fact worth having"; that is a
///         different surface, fed by <see cref="MemberMetrics" /> directly, and it is why this class is not
///         the only caller of that type.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MetricsAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Cyclomatic = SkalaRule.Descriptor(RuleIds.CyclomaticComplexity);
    static readonly DiagnosticDescriptor Cognitive = SkalaRule.Descriptor(RuleIds.CognitiveComplexity);
    static readonly DiagnosticDescriptor Statements = SkalaRule.Descriptor(RuleIds.MethodLengthInStatements);
    static readonly DiagnosticDescriptor TypeSize = SkalaRule.Descriptor(RuleIds.TypeSizeInMembers);
    static readonly DiagnosticDescriptor Parameters = SkalaRule.Descriptor(RuleIds.ParameterCount);
    static readonly DiagnosticDescriptor Nesting = SkalaRule.Descriptor(RuleIds.NestingDepth);
    static readonly DiagnosticDescriptor Undocumented = SkalaRule.Descriptor(RuleIds.PublicApiCommentDensity);

    static readonly ImmutableArray<SyntaxKind> MemberKinds = ImmutableArray.Create(
        SyntaxKind.MethodDeclaration,
        SyntaxKind.ConstructorDeclaration,
        SyntaxKind.DestructorDeclaration,
        SyntaxKind.OperatorDeclaration,
        SyntaxKind.ConversionOperatorDeclaration,
        SyntaxKind.PropertyDeclaration,
        SyntaxKind.IndexerDeclaration,
        SyntaxKind.EventDeclaration,
        SyntaxKind.DelegateDeclaration
    );

    static readonly ImmutableArray<SyntaxKind> TypeKinds = ImmutableArray.Create(
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration,
        SyntaxKind.EnumDeclaration
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(new[] { Cyclomatic, Cognitive, Statements, TypeSize, Parameters, Nesting, Undocumented });

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                // ⚠ The thresholds are per file, because a scoped `.editorconfig` section is how a
                // repository loosens a metric for `Testing/**` and tightens it for `Core/**`
                // (docs/plan/03 § "Severities" uses the same mechanism for severities). Resolving
                // them per member instead of per tree is a dictionary lookup and an allocation for
                // every member in the compilation.
                var cache = new ConcurrentDictionary<SyntaxTree, MetricThresholds>();

                start.RegisterSyntaxNodeAction(
                    context => AnalyzeMember(context, cache),
                    MemberKinds
                );

                start.RegisterSyntaxNodeAction(
                    context => AnalyzeType(context, cache),
                    TypeKinds
                );
            }
        );
    }

    static void AnalyzeMember(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, MetricThresholds> cache
    ) {
        var member = context.Node;
        var thresholds = Thresholds(context, cache);
        var metrics = MemberMetrics.Compute(member, context.SemanticModel, context.CancellationToken);
        var location = NameLocation(member);

        if (metrics.Cyclomatic > thresholds.Cyclomatic) {
            Report(
                context,
                Cyclomatic,
                location,
                metrics.Cyclomatic,
                "Cyclomatic complexity is "
                + Text(metrics.Cyclomatic)
                + ", over the threshold of "
                + Text(thresholds.Cyclomatic)
            );
        }

        if (metrics.Cognitive > thresholds.Cognitive) {
            Report(
                context,
                Cognitive,
                location,
                metrics.Cognitive,
                "Cognitive complexity is "
                + Text(metrics.Cognitive)
                + ", over the threshold of "
                + Text(thresholds.Cognitive)
            );
        }

        if (metrics.Statements > thresholds.Statements) {
            Report(
                context,
                Statements,
                location,
                metrics.Statements,
                "The member has "
                + Text(metrics.Statements)
                + " statements, over the threshold of "
                + Text(thresholds.Statements)
            );
        }

        if (metrics.Parameters > thresholds.Parameters) {
            Report(
                context,
                Parameters,
                location,
                metrics.Parameters,
                "The member takes "
                + Text(metrics.Parameters)
                + " parameters, over the threshold of "
                + Text(thresholds.Parameters)
            );
        }

        if (metrics.NestingDepth > thresholds.NestingDepth) {
            Report(
                context,
                Nesting,
                location,
                metrics.NestingDepth,
                "The member nests "
                + Text(metrics.NestingDepth)
                + " levels deep, over the threshold of "
                + Text(thresholds.NestingDepth)
            );
        }

        ReportUndocumented(context, member, location);
    }

    static void AnalyzeType(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, MetricThresholds> cache
    ) {
        var declaration = context.Node;
        var thresholds = Thresholds(context, cache);
        var location = NameLocation(declaration);

        // ⚠ Enum members are not counted: rules.json's SK7004 says "a 300-member enum is a table,
        // not a god object". An enum still reaches here for SK7010, which is about its own doc
        // comment and not about its members.
        if (declaration is TypeDeclarationSyntax type) {
            var size = MemberMetrics.ComputeTypeSize(type, context.CancellationToken);
            if (size.Members > thresholds.TypeMembers) {
                Report(
                    context,
                    TypeSize,
                    location,
                    size.Members,
                    "The type declares "
                    + Text(size.Members)
                    + " members and "
                    + Text(size.Fields)
                    + " fields, over the member threshold of "
                    + Text(thresholds.TypeMembers)
                );
            }

            // ⚠ A primary constructor is the type's constructor whatever the syntax, and docs/plan/07
            // § "Metrics" says its parameters count. Nothing else on a type declaration is a member
            // metric, so Compute is asked only for this.
            var metrics = MemberMetrics.Compute(type, null, context.CancellationToken);
            if (metrics.Parameters > thresholds.Parameters) {
                Report(
                    context,
                    Parameters,
                    location,
                    metrics.Parameters,
                    "The primary constructor takes "
                    + Text(metrics.Parameters)
                    + " parameters, over the threshold of "
                    + Text(thresholds.Parameters)
                );
            }
        }

        ReportUndocumented(context, declaration, location);
    }

    /// <summary>
    ///     <c>SK7010</c>: a publicly visible declaration with no documentation comment.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>defaultSeverity: none</c> in rules.json, so this produces nothing until a repository asks
    ///     for it per path. rules.json's rationale is the reason and it is worth repeating: as a
    ///     per-member warning on a repository that has never documented anything it produces thousands
    ///     of findings on day one and gets switched off by lunchtime. The <em>aggregate</em> is computed
    ///     either way, from <see cref="MemberMetrics.IsPublicApi" /> and
    ///     <see cref="MemberMetrics.HasDocumentation" />, which is the surface the number belongs on.
    /// </remarks>
    static void ReportUndocumented(SyntaxNodeAnalysisContext context, SyntaxNode declaration, Location location) {
        if (!MemberMetrics.IsDocumentable(declaration)
            || !MemberMetrics.IsPublicApi(declaration)
            || MemberMetrics.HasDocumentation(declaration)) {
            return;
        }

        // ⚠ The measurement here is the density itself: zero of this declaration's one doc comment is
        // present. It reads oddly on a single finding and it is the right number in the aggregate,
        // which is where comment density is a metric rather than a warning.
        Report(
            context,
            Undocumented,
            location,
            0,
            "`" + Name(declaration) + "` is public API with no documentation comment"
        );
    }

    static void Report(
        SyntaxNodeAnalysisContext context,
        DiagnosticDescriptor descriptor,
        Location location,
        int value,
        string message
    ) {
        // ⚠ The measured value travels on the diagnostic, so a reader — the SARIF, `skala explain`,
        // an agent — sees the number without re-deriving it, and cannot re-derive it differently.
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(MemberMetrics.ValueKey, value.ToString(CultureInfo.InvariantCulture));

        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, properties, message));
    }

    static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>The declaration's name, so a finding points at the name and not at 300 lines.</summary>
    static Location NameLocation(SyntaxNode declaration) =>
        declaration switch {
            MethodDeclarationSyntax method => method.Identifier.GetLocation(),
            ConstructorDeclarationSyntax constructor => constructor.Identifier.GetLocation(),
            DestructorDeclarationSyntax destructor => destructor.Identifier.GetLocation(),
            OperatorDeclarationSyntax @operator => @operator.OperatorToken.GetLocation(),
            ConversionOperatorDeclarationSyntax conversion => conversion.Type.GetLocation(),
            PropertyDeclarationSyntax property => property.Identifier.GetLocation(),
            IndexerDeclarationSyntax indexer => indexer.ThisKeyword.GetLocation(),
            EventDeclarationSyntax @event => @event.Identifier.GetLocation(),
            DelegateDeclarationSyntax @delegate => @delegate.Identifier.GetLocation(),
            BaseTypeDeclarationSyntax type => type.Identifier.GetLocation(),
            _ => declaration.GetLocation()
        };

    static string Name(SyntaxNode declaration) =>
        declaration switch {
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            IndexerDeclarationSyntax => "this[]",
            EventDeclarationSyntax @event => @event.Identifier.ValueText,
            DelegateDeclarationSyntax @delegate => @delegate.Identifier.ValueText,
            BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
            _ => declaration.Kind().ToString()
        };

    static MetricThresholds Thresholds(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, MetricThresholds> cache
    ) {
        var tree = context.Node.SyntaxTree;
        if (cache.TryGetValue(tree, out var cached)) {
            return cached;
        }

        var resolved = MetricThresholds.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        cache.TryAdd(tree, resolved);
        return resolved;
    }
}

/// <summary>
///     The thresholds for one file, from the <c>.editorconfig</c> chain that applies to it.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/07 § "Metrics": "All thresholds are <c>.editorconfig</c> options in Skala's own
///     namespace (<c>dotnet_code_quality.SK7002.threshold = 15</c>), which is the standard mechanism
///     Roslyn analyzers already use for configuration and therefore needs no invention." The same key
///     shape the CA rules use, read through the same provider, so a repository that already knows how to
///     configure an analyzer already knows how to configure these.
///     <para>
///         ⚠ A missing or unparseable value falls back to the documented default <em>silently</em>. A metric
///         rule that fails a build because someone wrote <c>threshold = fifteen</c> is a metric rule that
///         gets switched off; the reasonable reading of a broken threshold is "they meant the default".
///         (A key that is not a threshold at all is <c>SK9001</c>'s job, on a different surface.)
///     </para>
/// </remarks>
sealed class MetricThresholds {
    /// <summary>Defaults, exactly as rules.json documents them. Nothing here is invented.</summary>
    public static MetricThresholds Default { get; } = new();

    public int Cyclomatic { get; private set; } = 25;

    public int Cognitive { get; private set; } = 15;

    public int Statements { get; private set; } = 120;

    public int TypeMembers { get; private set; } = 80;

    public int Parameters { get; private set; } = 8;

    public int NestingDepth { get; private set; } = 6;

    public int FileLines { get; private set; } = 1000;

    /// <summary>
    ///     <c>SK7080</c>: base classes above a class, counting only the ones this compilation declares.
    /// </summary>
    public int InheritanceDepth { get; private set; } = 4;

    /// <summary><c>SK7081</c>: distinct other named types one type declaration mentions.</summary>
    public int TypeCoupling { get; private set; } = 80;

    public static MetricThresholds Read(AnalyzerConfigOptions options) =>
        new() {
            InheritanceDepth = Value(options, RuleIds.InheritanceDepth, Default.InheritanceDepth),
            TypeCoupling = Value(options, RuleIds.TypeCoupling, Default.TypeCoupling),
            Cyclomatic = Value(options, RuleIds.CyclomaticComplexity, Default.Cyclomatic),
            Cognitive = Value(options, RuleIds.CognitiveComplexity, Default.Cognitive),
            Statements = Value(options, RuleIds.MethodLengthInStatements, Default.Statements),
            TypeMembers = Value(options, RuleIds.TypeSizeInMembers, Default.TypeMembers),
            Parameters = Value(options, RuleIds.ParameterCount, Default.Parameters),
            NestingDepth = Value(options, RuleIds.NestingDepth, Default.NestingDepth),
            FileLines = Value(options, RuleIds.FileLength, Default.FileLines)
        };

    static int Value(AnalyzerConfigOptions options, string ruleId, int fallback) {
        if (!options.TryGetValue("dotnet_code_quality." + ruleId + ".threshold", out var text)) {
            return fallback;
        }

        // ⚠ A negative or zero threshold would fire on every member in the repository, which is
        // indistinguishable from the tool being broken. Treat it as the typo it is.
        return int.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0
                ? parsed
                : fallback;
    }
}
