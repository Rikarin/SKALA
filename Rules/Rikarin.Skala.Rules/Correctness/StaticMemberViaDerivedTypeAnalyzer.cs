using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2183</c> — a static member reached through a type that inherits it rather than declares it.
/// </summary>
/// <remarks>
///     <para>
///         <c>Leaf.Count</c> and <c>Root.Count</c> are one slot. Written the first way the line says
///         otherwise, and the reader who goes to <c>Leaf</c> for the declaration does not find it. It
///         costs something the day a second type derives from <c>Root</c> and the two are found to
///         share a value nobody thought they shared.
///     </para>
///     <para>
///         ⚠ <b>Nothing else reports it, and both candidates were measured rather than assumed.</b> In
///         a probe built outside this repository with empty <c>Directory.Build.props</c> above it, at
///         <c>AnalysisMode=All</c> with <c>EnforceCodeStyleInBuild</c>, <c>Leaf.Count</c>,
///         <c>Leaf.Read()</c>, <c>Leaf.Limit</c> and <c>Leaf.Total</c> produced nothing.
///         <c>CA1000</c> and <c>IDE0002</c> were each raised to <c>warning</c> and stayed silent on all
///         four — and <c>CA1000</c>'s own shape was planted alongside to prove the instrument was live,
///         which it was.
///     </para>
///     <para>
///         ⚠ <b>The declaring type must be nameable at the site or nothing is reported.</b> A
///         <c>public</c> type deriving from an <c>internal</c> base is the shape that would otherwise
///         produce a fix that does not compile. ⚠
///         <b>
///             <c>IsSymbolAccessibleWithin</c> throws for
///             anything that is not a type or an assembly
///         </b>
///         , so the <c>within</c> argument here is always
///         an <see cref="INamedTypeSymbol" /> and the check is skipped where there is none.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StaticMemberViaDerivedTypeAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.StaticMemberViaDerivedType);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        var registrar = PartialConstructorDefinitions.Visiting(context);
        registrar.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SimpleMemberAccessExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var access = (MemberAccessExpressionSyntax)context.Node;
        if (access.ContainsDiagnostics) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // The qualifier has to *be* a type. `instance.Member` is a different question, and a nested
        // type qualifier — `Outer.Inner.Member` — reaches here as its own member access with `Inner`
        // as the qualifier, which is the one that matters.
        if (model.GetSymbolInfo(access.Expression, cancellation).Symbol is not INamedTypeSymbol qualifier
            || qualifier.TypeKind is not (TypeKind.Class or TypeKind.Struct)
            || model.GetSymbolInfo(access, cancellation).Symbol is not { IsStatic: true } member) {
            return;
        }

        // ⚠ Only fields, properties, methods and events. A nested *type* reached through a derived
        // type is a different language rule with a different answer, and an extension method has no
        // meaningful declaring type here at all.
        if (member.Kind is not (SymbolKind.Field or SymbolKind.Property or SymbolKind.Method or SymbolKind.Event)
            || member is IMethodSymbol { MethodKind: not (MethodKind.Ordinary or MethodKind.ReducedExtension) }
            || member.ContainingType is not { } declaring
            || declaring.TypeKind is TypeKind.Interface) {
            return;
        }

        // ⚠ The declaring type must be a *strict* base of the qualifier, and this one test is the
        // whole discriminator. An earlier draft guarded separately against the declaring type being
        // the qualifier itself; sabotaging that clause turned nothing red, because `IsBaseOf` starts
        // at `qualifier.BaseType` and therefore already declines it — the clause was dead, and its own
        // fixtures were passing for the other reason.
        if (!IsBaseOf(declaring, qualifier)) {
            return;
        }

        if (!IsNameableAt(model.Compilation, declaring, context.ContainingSymbol)) {
            return;
        }

        var replacement = Qualifier(model, access, declaring, member, cancellation);
        var span = access.Expression.Span;
        var properties = replacement is null
            || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(access.SyntaxTree, span)
                ? null
                : FixEdits.Pack((span, replacement));

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                access.Expression.GetLocation(),
                properties,
                "`"
                + member.Name
                + "` is declared on `"
                + declaring.ToDisplayString()
                + "`, not on `"
                + qualifier.ToDisplayString()
                + "`, so the qualifier names a type that only inherits it"
            )
        );
    }

    /// <summary>
    ///     ⚠ The spelling of <paramref name="declaring" /> that, written as the qualifier, still reaches
    ///     <paramref name="member" /> — or <see langword="null" /> when none does (#424).
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="TypeNameWriting.At" /> asks <c>LookupNamespacesAndTypes</c>, which is the right
    ///     question in a <c>typeof</c> and the wrong one here: a qualifier is an <em>expression</em>, and
    ///     a local, a parameter or a member called <c>Base</c> takes <c>Base.Hello()</c> before the type
    ///     does. #412's audit measured <c>Base.Hello (static)</c> becoming <c>Other.Hello (instance)</c>
    ///     through a property named like the base type. The minimal spelling is bound in place, then the
    ///     <c>global::</c>-qualified one, and whichever reaches the same member is written.
    /// </remarks>
    static string? Qualifier(
        SemanticModel model,
        MemberAccessExpressionSyntax access,
        INamedTypeSymbol declaring,
        ISymbol member,
        System.Threading.CancellationToken cancellation
    ) {
        foreach (var candidate in new[] {
                     TypeNameWriting.At(declaring, model, access.Expression.SpanStart),
                     declaring.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                 }) {
            var written = SyntaxFactory.ParseExpression(candidate);
            if (!written.ContainsDiagnostics
                && FixRebind.TrySpeculate(model, access.Expression, written, out var speculative, out var placed)
                && FixRebind.BindsTo(speculative, placed, declaring, cancellation)
                && placed.Parent is MemberAccessExpressionSyntax rewritten
                && Reaches(speculative, rewritten, member, cancellation)) {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    ///     Whether the access binds to <paramref name="member" />, or names a method group that holds it.
    /// </summary>
    static bool Reaches(
        SemanticModel model,
        MemberAccessExpressionSyntax access,
        ISymbol member,
        System.Threading.CancellationToken cancellation
    ) {
        var info = model.GetSymbolInfo(access, cancellation);
        return FixRebind.Same(info.Symbol, member)
            || info.Symbol is null
            && info.CandidateSymbols.Length == 1
            && FixRebind.Same(info.CandidateSymbols[0], member);
    }

    static bool IsBaseOf(INamedTypeSymbol candidate, INamedTypeSymbol derived) {
        for (var current = derived.BaseType; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, candidate.OriginalDefinition)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether the declaring type can be written at the use site.
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="Compilation.IsSymbolAccessibleWithin" /> throws an
    ///     <c>ArgumentException</c> for a <c>within</c> that is neither a type nor an assembly, which
    ///     is why the argument is narrowed to an <see cref="INamedTypeSymbol" /> before the call. Where
    ///     the enclosing symbol has no containing type — a top-level statement, a compilation-level
    ///     action — the check falls back to the assembly, which the method does accept.
    /// </remarks>
    static bool IsNameableAt(Compilation compilation, INamedTypeSymbol declaring, ISymbol? enclosing) {
        var within = enclosing as INamedTypeSymbol
            ?? enclosing?.ContainingType
            ?? (ISymbol)compilation.Assembly;

        return compilation.IsSymbolAccessibleWithin(declaring, within);
    }
}
