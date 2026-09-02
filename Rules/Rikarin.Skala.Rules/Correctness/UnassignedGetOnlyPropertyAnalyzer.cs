using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2131</c> — a get-only auto-property with no initializer that nothing ever assigns.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         This is the one shape of issue #24 the compiler leaves entirely alone, and the narrowing was
///         measured rather than reasoned.
///     </b> A probe built at <c>AnalysisMode=All</c> reports
///     <c>CS0649</c> for a <c>private</c>, an <c>internal</c> and a <c>private readonly</c> field that
///     nothing assigns, so the field half of the concept is the compiler's already and there is nothing
///     to add. A <c>public</c> field gets no <c>CS0649</c> — and cannot: any consumer outside the
///     compilation may write it, so the claim is not decidable here either. What is left is the
///     property half, and it is decidable for a reason that is worth stating:
///     <b>
///         a property with only a
///         <c>get</c> accessor and no initializer can be assigned from nowhere but a constructor of its own
///         declaring type
///     </b>. Every part of that type is in this compilation — a type cannot be split
///     across assemblies, and a source generator's part is compiled source like any other — so "nothing
///     assigns it" is a fact this analysis can establish rather than a guess about callers.
///     <para>
///         ⚠
///         <b>
///             A non-nullable reference type under nullable warnings is declined, because
///             <c>CS8618</c> already reports it
///         </b> — verified on the same probe, on both an explicit and an
///         implicit constructor. ADR-008: hosting a diagnostic the platform already emits is the right
///         outcome, and reporting it a second time under a Skala id would put two findings on one
///         declaration. What survives the exclusion is everything <c>CS8618</c> cannot see: value types,
///         nullable reference types, and every property in a nullable-oblivious file — measured, again
///         on the probe, where a get-only <c>string</c> under <c>#nullable disable</c> draws nothing at
///         all.
///     </para>
///     <para>
///         ⚠ <b>The type is walked through its symbol rather than through the fixture's own file</b>, so
///         a <c>partial</c> part in generated source — the source generator that writes the constructor
///         — is read even though the analyzer is configured not to <em>report</em> in generated code. A
///         rule that skipped those parts would fire on every generated-constructor type, which is the
///         same wall that closed #114 and #115; here the wall is only in front of reflection.
///     </para>
///     <para>
///         ⚠
///         <b>
///             A positional record property is never seen, and that is by construction rather than by
///             a filter.
///         </b> This rule reads <see cref="PropertyDeclarationSyntax" />, and a positional
///         record's property has none — the parameter is where it is written down — so no test on
///         <c>IsImplicitlyDeclared</c> is needed and none is performed. The shape would be declined
///         anyway, because a positional property is <c>{ get; init; }</c> rather than <c>{ get; }</c>.
///     </para>
///     <para>
///         Report-only. The property is permanently <c>default</c> and the repair is to say what it
///         should be instead, which is the one thing the declaration does not contain: an initializer
///         needs a value, a constructor assignment needs a source, and an <c>init</c> accessor changes
///         the type's public contract.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnassignedGetOnlyPropertyAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UnassignedGetOnlyProperty);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    /// <remarks>
    ///     ⚠ <c>GeneratedCodeAnalysisFlags.Analyze</c> rather than <c>None</c>, and the difference is the
    ///     rule's correctness rather than its reach. Under <c>None</c> the assignment action would not
    ///     run in a generated <c>partial</c> part, so a property the generated constructor assigns would
    ///     look unassigned and the rule would fire on every generated-constructor type. <c>Analyze</c>
    ///     reads generated source and still reports nothing in it, because Roslyn filters a diagnostic
    ///     whose location is generated unless <c>ReportDiagnostics</c> is also set — which it is not.
    /// </remarks>
    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.EnableConcurrentExecution();
        context.RegisterSymbolStartAction(OnType, SymbolKind.NamedType);
    }

    /// <summary>
    ///     One type at a time, so that every <c>partial</c> part is in scope without an analyzer ever
    ///     asking the compilation for a semantic model (RS1030).
    /// </summary>
    static void OnType(SymbolStartAnalysisContext context) {
        // ⚠ A symbol-level pre-filter, so that the great majority of types register no syntax action
        // at all. `IsReadOnly` on a property is "has no set accessor", which is a necessary condition
        // for everything below and costs one pass over the member list.
        var type = (INamedTypeSymbol)context.Symbol;
        if (!type.GetMembers()
                .OfType<IPropertySymbol>()
                .Any(static property => property is { IsReadOnly: true, IsAbstract: false, IsExtern: false })) {
            return;
        }

        var candidates = new ConcurrentDictionary<ISymbol, (Location Location, string Name)>(
            SymbolEqualityComparer.Default
        );
        var assigned = new ConcurrentDictionary<ISymbol, byte>(SymbolEqualityComparer.Default);

        context.RegisterSyntaxNodeAction(
            node => Candidate(node, candidates),
            SyntaxKind.PropertyDeclaration
        );

        context.RegisterSyntaxNodeAction(
            node => {
                var assignment = (AssignmentExpressionSyntax)node.Node;
                if (node.SemanticModel.GetSymbolInfo(assignment.Left, node.CancellationToken).Symbol
                    is IPropertySymbol target) {
                    assigned.TryAdd(target.OriginalDefinition, 0);
                }
            },
            SyntaxKind.SimpleAssignmentExpression
        );

        context.RegisterSymbolEndAction(end => {
                foreach (var pair in candidates) {
                    if (assigned.ContainsKey(pair.Key)) {
                        continue;
                    }

                    end.ReportDiagnostic(
                        Diagnostic.Create(
                            Descriptor,
                            pair.Value.Location,
                            "`"
                            + pair.Value.Name
                            + "` has no setter, no initializer and no constructor that assigns it, so it holds "
                            + "`default` for the life of every instance"
                        )
                    );
                }
            }
        );
    }

    static void Candidate(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<ISymbol, (Location, string)> candidates
    ) {
        var property = (PropertyDeclarationSyntax)context.Node;

        // `{ get; }` and nothing else: one accessor, no body of any kind, no initializer. An
        // expression-bodied property computes its value and a `set`/`init` accessor gives somebody a
        // way to supply one, so neither can be permanently `default`.
        if (property.Initializer is not null
            || property.ExpressionBody is not null
            || property.AttributeLists.Count > 0
            || property.AccessorList is not { Accessors.Count: 1 } accessors) {
            return;
        }

        var accessor = accessors.Accessors[0];
        if (!accessor.IsKind(SyntaxKind.GetAccessorDeclaration)
            || accessor.Body is not null
            || accessor.ExpressionBody is not null) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(property, context.CancellationToken) is not IPropertySymbol symbol
            || symbol.IsAbstract
            || symbol.IsExtern
            || symbol.ContainingType is null) {
            return;
        }

        if (HostedByCs8618(context, property, symbol)) {
            return;
        }

        candidates.TryAdd(symbol.OriginalDefinition, (property.Identifier.GetLocation(), symbol.Name));
    }

    /// <summary>
    ///     ⚠ <c>CS8618</c>'s exact territory: a non-nullable reference type in a file where nullable
    ///     <em>warnings</em> are on.
    /// </summary>
    /// <remarks>
    ///     The annotation half of the nullable context is deliberately not consulted. <c>CS8618</c> is a
    ///     warning, and a file with annotations enabled and warnings disabled gets none — which is a
    ///     real configuration and one where this rule is the only thing that will say anything.
    ///     <para>
    ///         An unconstrained type parameter falls on the declined side, because
    ///         <see cref="ITypeSymbol.IsValueType" /> is false for it and the compiler treats it as
    ///         possibly-non-nullable. That is the conservative direction: a shape this rule is unsure
    ///         about is one it does not report.
    ///     </para>
    /// </remarks>
    static bool HostedByCs8618(
        SyntaxNodeAnalysisContext context,
        PropertyDeclarationSyntax property,
        IPropertySymbol symbol
    ) =>
        !symbol.Type.IsValueType
        && symbol.NullableAnnotation != NullableAnnotation.Annotated
        && (context.SemanticModel.GetNullableContext(property.SpanStart) & NullableContext.WarningsEnabled) != 0;
}
