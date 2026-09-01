using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7080</c>: how many base classes a class has above it, counting only the ones this
///     compilation declares.
/// </summary>
/// <remarks>
///     ⚠ <b>The chain stops at the first base class that is not source.</b> Raw depth penalises using a
///     framework: <c>MyControl : Button</c> is one decision somebody in the repository made and the
///     eight types above <c>Button</c> are not, so counting them would turn a maintainability metric
///     into an opinion about which framework to build on. The consequence is that a base class in a
///     *referenced project* also ends the chain, because it reaches this compilation as metadata; that
///     under-reports, which is the safe direction for a number nobody can act on from here anyway.
///     <para>
///         ⚠ <b>An unresolved base withdraws the measurement.</b> A chain that cannot be walked reads as
///         depth 1 (issue #277: the corpus slices have no dependency closure, so nearly every base is an
///         error type). Reporting the smaller number would be silently wrong rather than loudly absent,
///         so an error type anywhere on the chain means this analyzer reports nothing at all for that
///         type — and the zero it then produces is "the analysis declined", not "the code is shallow".
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InheritanceDepthAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.InheritanceDepth);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // ⚠ Classes and record classes only. An interface has a graph of bases rather than a chain and
        // the number would not mean the same thing; a struct has no base chain a person can write.
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration, SyntaxKind.RecordDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (TypeDeclarationSyntax)context.Node;

        // ⚠ A base *class* can only be named on the part of a partial type that carries the base list,
        // so this both reports a partial type once and skips the common shallow case without asking
        // the semantic model anything.
        if (declaration.BaseList is null) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } symbol) {
            return;
        }

        var depth = 0;
        for (var current = symbol.BaseType; current is not null; current = current.BaseType) {
            if (current.TypeKind == TypeKind.Error) {
                return;
            }

            if (current.SpecialType == SpecialType.System_Object) {
                break;
            }

            depth++;

            // The first base that this compilation did not declare ends the chain: it counts once,
            // and whatever is above it is the framework's business rather than the repository's.
            if (current.DeclaringSyntaxReferences.Length == 0) {
                break;
            }
        }

        var threshold = MetricThresholds
            .Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(declaration.SyntaxTree))
            .InheritanceDepth;

        if (depth <= threshold) {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(
            MemberMetrics.ValueKey,
            depth.ToString(CultureInfo.InvariantCulture)
        );

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declaration.Identifier.GetLocation(),
                properties,
                "`"
                + declaration.Identifier.ValueText
                + "` has "
                + depth.ToString(CultureInfo.InvariantCulture)
                + " base classes above it, over the threshold of "
                + threshold.ToString(CultureInfo.InvariantCulture)
            )
        );
    }
}
