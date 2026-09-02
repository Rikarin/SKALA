using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2180</c> — a <c>foreach</c> whose loop variable narrows the sequence's element type.
/// </summary>
/// <remarks>
///     <para>
///         C# writes an explicit conversion into a <c>foreach</c> when the declared variable type is
///         narrower than what the enumerator yields. Nothing in the source shows the cast, so
///         <c>foreach (Circle c in shapes)</c> reads as a statement about what <c>shapes</c> holds and
///         is really an assertion the loop makes about every element, one element at a time, until one
///         of them fails it.
///     </para>
///     <para>
///         ⚠
///         <b>
///             This is the residue of issue #2 after the compiler has taken its share, and the share
///             was measured.
///         </b> At <c>AnalysisMode=All</c>, <c>(Sealed)derived</c>,
///         <c>(IUnrelated)sealedValue</c> and <c>(Sealed)unrelatedInterface</c> are
///         <b>
///             <c>CS0030</c>,
///             errors
///         </b> — that source never reaches an analyzer. A plain downcast <c>(Derived)b</c> and a
///         covariant array conversion produce nothing at all, but deciding either needs to know which
///         values reach the site, which is the value lattice this codebase does not have and which
///         refuted the neighbouring issue #169. The <c>foreach</c> form needs none of it: the cast is
///         unconditional, and the only question is whether the element type is narrower.
///     </para>
///     <para>
///         ⚠ <b>An <c>object</c> element type is never reported</b>, and that exclusion is most of the
///         rule's safety. A non-generic <c>IEnumerable</c>, an <c>ArrayList</c> or a
///         <c>List&lt;object&gt;</c> offers no other spelling, so the cast there is the API's doing
///         rather than the author's.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ForeachElementDowncastAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ForeachElementDowncast);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ForEachStatement);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var statement = (ForEachStatementSyntax)context.Node;
        if (statement.ContainsDiagnostics || statement.Type.IsVar) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        var info = model.GetForEachStatementInfo(statement);
        if (info.ElementType is not { } element
            || model.GetTypeInfo(statement.Type, cancellation).Type is not { } declared
            || !IsUsable(element)
            || !IsUsable(declared)) {
            return;
        }

        // ⚠ The sequence's element type is what the loop is narrowing *from*, and an `object` element
        // type is the shape with no alternative spelling. `List<object>` is excluded by the same test
        // as `ArrayList`, deliberately: both hand the loop an element the author cannot type.
        if (element.SpecialType == SpecialType.System_Object) {
            return;
        }

        if (model.Compilation is not CSharpCompilation compilation) {
            return;
        }

        // ⚠ The two *types* are classified rather than the expression, for `SK2121`'s reason: the loop
        // variable's initialisation is not an assignment in a target-typed context, so
        // `ClassifyConversion` over an expression would fold in conversions the loop is not making.
        var conversion = compilation.ClassifyConversion(element, declared);
        if (!conversion.Exists
            || conversion.IsIdentity
            || conversion.IsImplicit
            || !(conversion.IsReference || conversion.IsUnboxing)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                statement.Type.GetLocation(),
                "the sequence yields `"
                + element.ToDisplayString()
                + "`, so this loop casts every element down to `"
                + declared.ToDisplayString()
                + "` and throws on the first one that is something else"
            )
        );
    }

    /// <summary>
    ///     Types whose conversions are decided at compile time and stay decided at run time.
    /// </summary>
    /// <remarks>
    ///     ⚠ Type parameters are excluded for <c>SK2121</c>'s reason: inside a generic method a
    ///     conversion is classified against the constraint set rather than against the type the method
    ///     is instantiated with, so a narrowing that looks certain there is not certain at run time.
    /// </remarks>
    static bool IsUsable(ITypeSymbol type) =>
        type.TypeKind is not (TypeKind.Error or TypeKind.Dynamic or TypeKind.TypeParameter or TypeKind.Unknown);
}
