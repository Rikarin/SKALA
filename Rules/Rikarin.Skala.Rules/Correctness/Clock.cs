using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     What three rules in the <c>SK2160</c> band need to agree about: what a clock read is, what a
///     <c>DateTime</c>'s <see cref="System.DateTimeKind" /> provably is, and when a local can be read
///     through.
/// </summary>
/// <remarks>
///     ⚠ <b>The three questions are shared because getting different answers to them would make the
///     rules disagree about the same line.</b> <c>SK2160</c> reports a clock read, <c>SK2163</c> reports a
///     subtraction of two of them, and <c>SK2161</c> reports a value whose zone was never stated; if each
///     carried its own idea of what <c>DateTime.UtcNow</c> is, a shape could be reported twice with two
///     different explanations or fall between all three.
///     <para>
///         ⚠ <b>Every type test goes through <see cref="IsFrameworkType" />, which requires the symbol to
///         come from metadata rather than from source.</b> A repository's own type called
///         <c>DateTime</c> — which <c>Testing/corpus</c> contains — must never be matched, and a rule
///         that compared display strings would match it. The same guard is what
///         <c>NondeterministicAssertionAnalyzer</c> uses for <c>SK8007</c>.
///     </para>
/// </remarks>
static class Clock {
    /// <summary>
    ///     Whether an operation is a read of the machine's wall clock through a static framework
    ///     property.
    /// </summary>
    /// <remarks>
    ///     ⚠ The five members are named, and <c>MinValue</c>/<c>MaxValue</c> deliberately are not: those
    ///     are constants, not clock reads, and reporting them would make every sentinel comparison a
    ///     finding. <c>Today</c> is included because it is <c>Now.Date</c> and carries the same
    ///     dependency on the machine.
    /// </remarks>
    public static bool IsStaticRead(IOperation? operation, Compilation compilation) =>
        operation is IPropertyReferenceOperation { Property.IsStatic: true } reference
        && (reference.Property.Name == "Now"
            || reference.Property.Name == "UtcNow"
            || reference.Property.Name == "Today")
        && (IsFrameworkType(reference.Property.ContainingType, compilation, "System.DateTime")
            || IsFrameworkType(reference.Property.ContainingType, compilation, "System.DateTimeOffset"));

    /// <summary>How a clock read is spelled, for a message that quotes the code rather than a category.</summary>
    public static string NameOf(IOperation operation) =>
        operation is IPropertyReferenceOperation reference
            ? reference.Property.ContainingType.Name + "." + reference.Property.Name
            : "the clock";

    /// <summary>
    ///     Whether a creation produces a <c>DateTime</c> whose <c>Kind</c> is provably
    ///     <c>Unspecified</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The test is on the constructor's <em>parameters</em>, not on the arguments written.</b>
    ///     Every <c>DateTime</c> constructor that does not take a <c>DateTimeKind</c> produces
    ///     <c>Unspecified</c>, and that is a property of the overload rather than of the values passed —
    ///     so an overload taking a <c>Calendar</c>, a <c>TimeOnly</c> or a bare tick count is decided
    ///     correctly without any of them being listed here.
    ///     <para>
    ///         ⚠ An argument spelled <c>DateTimeKind.Unspecified</c> is <b>not</b> reported. The author
    ///         wrote the zone down; the finding is about a zone nobody stated, and reporting a stated one
    ///         would be reporting the repair.
    ///     </para>
    /// </remarks>
    public static bool IsUnspecifiedCreation(IOperation? operation, Compilation compilation) {
        if (operation is not IObjectCreationOperation { Constructor: { } constructor } creation
            || !IsFrameworkType(creation.Type, compilation, "System.DateTime")) {
            return false;
        }

        foreach (var parameter in constructor.Parameters) {
            if (IsFrameworkType(parameter.Type, compilation, "System.DateTimeKind")) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     The initializer of a local that is assigned exactly once, or <c>null</c> when that cannot be
    ///     proved.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The search is over the whole enclosing member, which is a superset of the local's
    ///     scope, and that over-approximation is the sound direction.</b> Seeing a write that does not
    ///     actually reach this local makes the rule decline; missing one would make it report a value it
    ///     had not proved anything about. Assignments, <c>ref</c>/<c>out</c> arguments and
    ///     increment/decrement are all writes.
    /// </remarks>
    public static ExpressionSyntax? SingleAssignedInitializer(
        ILocalSymbol local,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        if (local.DeclaringSyntaxReferences.Length != 1
            || local.DeclaringSyntaxReferences[0].GetSyntax(cancellation) is not VariableDeclaratorSyntax {
                Initializer.Value: { } initializer
            } declarator
            || Enclosing(declarator) is not { } body) {
            return null;
        }

        foreach (var node in body.DescendantNodes()) {
            var written = node switch {
                AssignmentExpressionSyntax assignment => assignment.Left,
                PrefixUnaryExpressionSyntax prefix when IsStep(prefix.Kind()) => prefix.Operand,
                PostfixUnaryExpressionSyntax postfix when IsStep(postfix.Kind()) => postfix.Operand,
                ArgumentSyntax { RefKindKeyword.RawKind: not (int)SyntaxKind.None } argument => argument.Expression,
                _ => null
            };

            if (written is not null
                && SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(written, cancellation).Symbol,
                    local
                )) {
                return null;
            }
        }

        return initializer;
    }

    /// <summary>How many times a local's name is bound inside its enclosing member, declaration aside.</summary>
    /// <remarks>
    ///     ⚠ Used by <c>SK2163</c>, whose fix rewrites both the declaration and the one use. A second
    ///     reader of the local would keep the old type and stop compiling, so the count is a
    ///     precondition of the rule rather than a detail of the fix.
    /// </remarks>
    public static int ReferenceCount(ILocalSymbol local, SemanticModel model, CancellationToken cancellation) {
        if (local.DeclaringSyntaxReferences.Length != 1
            || local.DeclaringSyntaxReferences[0].GetSyntax(cancellation) is not VariableDeclaratorSyntax declarator
            || Enclosing(declarator) is not { } body) {
            return int.MaxValue;
        }

        var count = 0;
        foreach (var name in body.DescendantNodes().OfType<IdentifierNameSyntax>()) {
            if (name.Identifier.ValueText == local.Name
                && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name, cancellation).Symbol, local)) {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    ///     Whether a type is the framework's, matched by symbol and required to come from metadata.
    /// </summary>
    public static bool IsFrameworkType(ITypeSymbol? type, Compilation compilation, string metadataName) =>
        type is not null
        && !type.Locations.Any(static location => location.IsInSource)
        && SymbolEqualityComparer.Default.Equals(type, compilation.GetTypeByMetadataName(metadataName));

    static bool IsStep(SyntaxKind kind) =>
        kind == SyntaxKind.PreIncrementExpression
        || kind == SyntaxKind.PreDecrementExpression
        || kind == SyntaxKind.PostIncrementExpression
        || kind == SyntaxKind.PostDecrementExpression;

    /// <summary>
    ///     The member a node sits in — the widest body that can hold a write to a local declared inside
    ///     it.
    /// </summary>
    static SyntaxNode? Enclosing(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            if (current is MemberDeclarationSyntax or CompilationUnitSyntax) {
                return current;
            }
        }

        return null;
    }
}
