using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7081</c>: how many other named types one type declaration mentions.
/// </summary>
/// <remarks>
///     ⚠ <b>The metric <c>SK7001</c>–<c>SK7006</c> do not carry.</b> A type can be short, shallow and
///     simple in every one of them and still know about forty other types, which is the thing that
///     actually makes it expensive to change: every one of those forty is a reason this file has to be
///     reopened.
///     <para>
///         ⚠ <b>Special types do not count.</b> <c>int</c>, <c>string</c>, <c>object</c>, <c>void</c>,
///         <c>IEnumerable&lt;T&gt;</c> and the rest of <see cref="SpecialType" /> are the language rather
///         than a dependency; counting them would add the same five to every type in the repository and
///         move the whole distribution without separating anything.
///     </para>
///     <para>
///         ⚠ <b>An unresolved reference is skipped, not counted.</b> That under-reports — on a source slice
///         with no dependency closure (issue #277) it under-reports to nothing — and under-reporting is
///         the direction a threshold metric may safely be wrong in. It is also why a zero from this rule
///         on the corpus says "the analysis declined", not "these types are decoupled".
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypeCouplingAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.TypeCoupling);

    static readonly ImmutableArray<SyntaxKind> TypeKinds = ImmutableArray.Create(
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, TypeKinds);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } self) {
            return;
        }

        var referenced = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var name in Names(declaration)) {
            context.CancellationToken.ThrowIfCancellationRequested();
            Collect(context.SemanticModel, name, self, referenced, context.CancellationToken);
        }

        var threshold = MetricThresholds
            .Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(declaration.SyntaxTree))
            .TypeCoupling;

        if (referenced.Count <= threshold) {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(
            MemberMetrics.ValueKey,
            referenced.Count.ToString(CultureInfo.InvariantCulture)
        );

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declaration.Identifier.GetLocation(),
                properties,
                "`"
                + declaration.Identifier.ValueText
                + "` depends on "
                + referenced.Count.ToString(CultureInfo.InvariantCulture)
                + " other types, over the threshold of "
                + threshold.ToString(CultureInfo.InvariantCulture)
            )
        );
    }

    /// <summary>
    ///     Every name written inside this declaration, without descending into a nested type.
    /// </summary>
    /// <remarks>
    ///     ⚠ A nested type has its own declaration and is measured by its own action. Descending into it
    ///     would charge the outer type for the inner one's dependencies, so a file with one wrapper class
    ///     around six others would report the union six times over — and the number a reader could act on
    ///     would be buried under one they could not.
    /// </remarks>
    static IEnumerable<SimpleNameSyntax> Names(TypeDeclarationSyntax declaration) {
        var pending = new Stack<SyntaxNode>();
        foreach (var child in declaration.ChildNodes()) {
            pending.Push(child);
        }

        while (pending.Count > 0) {
            var node = pending.Pop();
            if (node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax) {
                continue;
            }

            if (node is SimpleNameSyntax name) {
                yield return name;
            }

            foreach (var child in node.ChildNodes()) {
                pending.Push(child);
            }
        }
    }

    static void Collect(
        SemanticModel model,
        SimpleNameSyntax name,
        INamedTypeSymbol self,
        HashSet<INamedTypeSymbol> into,
        CancellationToken cancellation
    ) {
        // ⚠ The left of a qualified name resolves to a namespace and is filtered out below anyway, so
        // it is skipped here rather than paid for: `System.Collections.Generic.List<T>` is three
        // symbol lookups that can never contribute.
        if (name.Parent is QualifiedNameSyntax qualified && qualified.Left == name) {
            return;
        }

        var symbol = SymbolOf(model, name, cancellation);
        var type = symbol switch {
            INamedTypeSymbol named => named,
            IMethodSymbol method => method.ContainingType,
            IPropertySymbol property => property.ContainingType,
            IFieldSymbol field => field.ContainingType,
            IEventSymbol declaredEvent => declaredEvent.ContainingType,
            _ => null
        };

        Add(type, self, into, 0);
    }

    /// <summary>
    ///     ⚠ <c>GetSymbolInfo</c>, which can throw out of Roslyn on code that does not bind.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>The throw is inside the compiler, not inside anything this rule indexes.</b>
    ///     <c>MemberSemanticModel.GetBoundLambdaOrQuery</c> gets an empty <c>OneOrMany</c> back and
    ///     indexes it, so <c>GetSymbolInfo</c> raises <c>IndexOutOfRangeException</c> from
    ///     <c>GetEnclosingBinderInternal</c>. The shape that reaches it is a query expression in a
    ///     position the binder rejects before it ever descends —
    ///     <c>Func&lt;int&gt; v = new() { P = (from item in items select null) };</c>, which carries
    ///     both <c>CS1729</c> and <c>CS1958</c>, so the query has no bound node to return
    ///     (<c>Testing/corpus/pathological/target-typed-new-of-a-delegate-with-a-query.cs</c>, #315).
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             There is nothing to test in advance, which is why this is a <c>catch</c> and not a
    ///             guard.
    ///         </b> The question "would asking about this name throw" is answerable only by
    ///         asking, and every cheaper proxy — does the file have errors, is the name inside a query —
    ///         either costs a full <c>GetDiagnostics</c> per declaration or declines on ordinary code.
    ///         The exception type is narrow and the call it wraps is one, so nothing else can fall in
    ///         here; a cancellation is <c>OperationCanceledException</c> and passes straight through.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>Declining is already this rule's declared behaviour</b>, not a new concession: an
    ///         unresolved reference is skipped rather than counted, and the class remarks say a zero
    ///         from <c>SK7081</c> means "the analysis declined". A coupling metric has nothing to say
    ///         about code that does not bind. What it must not do is <em>throw</em> — Roslyn turns that
    ///         into <c>AD0001</c> and drops the analyzer for the rest of the compilation, so every
    ///         type-coupling finding in any project containing one such file disappears and the run
    ///         still reports success. <c>CorpusCrashTests</c> is what now notices.
    ///     </para>
    /// </remarks>
    static ISymbol? SymbolOf(SemanticModel model, SimpleNameSyntax name, CancellationToken cancellation) {
        try {
            return model.GetSymbolInfo(name, cancellation).Symbol;
        } catch (System.IndexOutOfRangeException) {
            return null;
        }
    }

    /// <summary>
    ///     Adds one type and the type arguments it was constructed with.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>Dictionary&lt;string, Widget&gt;</c> is a dependency on <c>Dictionary</c> and on
    ///     <c>Widget</c>. Counting only the written name would let a type hide an arbitrary number of
    ///     dependencies inside generic arguments; <paramref name="depth" /> bounds the recursion so a
    ///     pathologically nested generic cannot cost more than a constant.
    /// </remarks>
    static void Add(ITypeSymbol? type, INamedTypeSymbol self, HashSet<INamedTypeSymbol> into, int depth) {
        if (depth > 4) {
            return;
        }

        if (type is IArrayTypeSymbol array) {
            Add(array.ElementType, self, into, depth + 1);
            return;
        }

        if (type is IPointerTypeSymbol pointer) {
            Add(pointer.PointedAtType, self, into, depth + 1);
            return;
        }

        if (type is not INamedTypeSymbol candidate) {
            return;
        }

        foreach (var argument in candidate.TypeArguments) {
            Add(argument, self, into, depth + 1);
        }

        candidate = candidate.OriginalDefinition;
        if (candidate.TypeKind is TypeKind.Error or TypeKind.Dynamic
            || candidate.IsAnonymousType
            || candidate.SpecialType != SpecialType.None
            || IsSelfOrAFamilyMember(candidate, self)) {
            return;
        }

        into.Add(candidate);
    }

    /// <summary>
    ///     The type itself, anything nesting it, and anything it nests: none of them is a dependency
    ///     on somebody else.
    /// </summary>
    /// <remarks>
    ///     ⚠ A type naming its own nested helper is not coupled to another design — the two move
    ///     together by construction, and the file a reader opens contains both. Counting them would
    ///     charge every type that organises itself with nested types for having done so.
    /// </remarks>
    static bool IsSelfOrAFamilyMember(INamedTypeSymbol candidate, INamedTypeSymbol self) {
        for (var outer = self; outer is not null; outer = outer.ContainingType) {
            if (SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, outer.OriginalDefinition)) {
                return true;
            }
        }

        for (var outer = candidate; outer is not null; outer = outer.ContainingType) {
            if (SymbolEqualityComparer.Default.Equals(outer.OriginalDefinition, self.OriginalDefinition)) {
                return true;
            }
        }

        return false;
    }
}
