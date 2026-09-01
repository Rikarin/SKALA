using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Design;

/// <summary>
///     <c>SK6051</c> — a type asks whether <c>this</c> is one of its own subclasses.
/// </summary>
/// <remarks>
///     A base class that tests <c>this is Derived</c> has inverted its own dependency: adding a subclass
///     now means editing the base, and the compiler cannot say when a case is missing. It is
///     polymorphism written as a chain of <c>if</c>s, with none of the checking that makes an
///     <c>abstract</c> member work.
///     <para>
///         ⚠
///         <b>
///             The same shape is also how a closed hierarchy dispatches, and that form is declined
///             rather than reported.
///         </b> A base whose instance constructors are all <c>private</c> cannot be
///         derived from outside its own declaration, so the set of subclasses is fixed at compile time
///         and the test over them is exhaustive by construction — the property that makes the pattern a
///         discriminated union rather than a missing <c>virtual</c>. That guard is why this rule ships at
///         <c>suggestion</c> and not at the <c>warning</c> the proposal asked for: the guard is a
///         sufficient condition for the legitimate form and not a necessary one, so a sealed-by-
///         convention hierarchy is still reported and is still a judgement call.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Four guards this rule was specified to have are absent, because a sabotage pass proved
///             that none of them can fail.
///         </b> The walk in <c>DerivesFrom</c> starts one link above
///         <c>target</c> and only ever visits classes, so it alone decides "not <c>sealed</c>", "the
///         tested type is a class", "the tested type is not the containing type" and "the subclass is
///         declared in this compilation" — the last because a type in metadata cannot derive from a
///         source type, and <c>this</c> only exists in source. Each was written first, each turned
///         nothing red, and each is recorded rather than kept: a guard no fixture can exercise reads as
///         care and is untested code.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DerivedTypeTestOnThisAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.IsCheckAgainstThis);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IsExpression, SyntaxKind.IsPatternExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var (operand, tested) = context.Node switch {
            BinaryExpressionSyntax binary => (binary.Left, binary.Right),
            IsPatternExpressionSyntax pattern => (pattern.Expression, TestedType(pattern.Pattern)),
            _ => (null, null)
        };

        // ⚠ `this` written out, not "an expression whose type is the containing type". A local holding
        // the same instance is an ordinary type test on an ordinary value; what makes this a design
        // finding is that the type is asking about itself.
        if (operand is not ThisExpressionSyntax || tested is null) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetTypeInfo(operand, cancellation).Type is not INamedTypeSymbol self
            || model.GetTypeInfo(tested, cancellation).Type is not INamedTypeSymbol target) {
            return;
        }

        // ⚠ **Four guards this rule was specified to have are absent, because a sabotage pass proved
        // that none of them can fail.** The walk in `DerivesFrom` starts one link above `target` and
        // only ever visits classes, which decides all four on its own:
        //
        //   `sealed`               nothing derives from a sealed type
        //   `target` is a class    an interface and a struct have no class in the chain above them
        //   `target != self`       the walk starts one link up, so identity is already excluded
        //   `target` is in source  a type in metadata cannot derive from a source type, and `this`
        //                          only exists in source, so `self` is always one
        //
        // Each was written first and each turned nothing red. They are recorded here instead: a guard
        // no fixture can exercise reads as care and is untested code.
        if (!DerivesFrom(target, self) || IsClosed(self)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                context.Node.GetLocation(),
                "`"
                + self.Name
                + "` asks whether it is a `"
                + target.Name
                + "`; the answer belongs in a member `"
                + target.Name
                + "` overrides, where the compiler can see a case go missing"
            )
        );
    }

    /// <summary>The type a pattern tests for, where the pattern is a type test at all.</summary>
    static TypeSyntax? TestedType(PatternSyntax pattern) =>
        pattern switch {
            DeclarationPatternSyntax declaration => declaration.Type,
            TypePatternSyntax type => type.Type,
            RecursivePatternSyntax { Type: { } recursive } => recursive,
            _ => null
        };

    static bool DerivesFrom(INamedTypeSymbol target, INamedTypeSymbol self) {
        for (var current = target.BaseType; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, self.OriginalDefinition)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether nothing outside this type's own declaration can derive from it.
    /// </summary>
    /// <remarks>
    ///     ⚠ A <c>private</c> instance constructor on a non-sealed class is the closed-hierarchy idiom:
    ///     only a nested type can call it, so the subclasses are exactly the ones written inside the base
    ///     and a test over them cannot silently miss a case. The compiler is enforcing the exhaustiveness
    ///     that this rule otherwise exists to say nobody is enforcing, so the finding is withdrawn.
    ///     <para>
    ///         A class with no declared constructor has an implicit public one and is not closed. A
    ///         <c>static</c> class has no instance constructors at all and cannot be the receiver of
    ///         <c>this</c> in an instance member, so the empty case answering <c>false</c> costs nothing.
    ///     </para>
    /// </remarks>
    static bool IsClosed(INamedTypeSymbol self) {
        var any = false;
        foreach (var constructor in self.InstanceConstructors) {
            if (constructor.DeclaredAccessibility != Accessibility.Private) {
                return false;
            }

            any = true;
        }

        return any;
    }
}
