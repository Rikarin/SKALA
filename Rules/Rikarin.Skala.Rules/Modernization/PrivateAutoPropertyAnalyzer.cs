using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1091</c> — <c>private int Total { get; set; }</c> is <c>private int Total;</c>.
/// </summary>
/// <remarks>
///     <para>
///         A <c>private</c> auto-property has no external contract to preserve, so the property exists
///         only to wrap a field the compiler generated anyway. The cost is not the two accessor calls
///         the JIT inlines away — it is that the field is <em>invisible</em>: <c>readonly</c> cannot be
///         applied to it, <c>ref</c> cannot be taken of it and no <c>Interlocked</c> update can be
///         written against it.
///     </para>
///     <para>
///         ⚠ <b>This is <c>SK1003</c> from the other side, and the two cannot both fire.</b>
///         <c>SK1003</c> starts from a private field with a hand-written property over it and folds
///         the field into the <c>field</c> keyword; it requires two <em>bodied</em> accessors and a
///         separate field declaration, and an auto-property has neither.
///     </para>
///     <para>
///         ⚠ <b>The fix keeps the identifier.</b> Renaming <c>Entries</c> to <c>entries</c> is a naming
///         decision this rule does not own and would spread the edit over every use site; leaving the
///         name alone is what makes the rewrite local enough to be <c>fixIsSafe: true</c>.
///     </para>
///     <para>
///         ⚠ <b>The property has to be both read and written somewhere.</b> A field never assigned is
///         <c>CS0649</c> and one assigned and never read is <c>CS0414</c>, so a fix that skipped the
///         census would turn a silent property into a warning on a <c>TreatWarningsAsErrors</c> build
///         — the exact failure <c>fixIsSafe: true</c> promises cannot happen.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrivateAutoPropertyAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.PrivateAutoProperty);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.PropertyDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var property = (PropertyDeclarationSyntax)context.Node;

        // ⚠ A record is excluded: `with` sets members by name and the synthesized equality members
        // are written against the fields, so the shape of the declaration is load-bearing there.
        if (property.Parent is not (ClassDeclarationSyntax or StructDeclarationSyntax)
            || property.Parent is not TypeDeclarationSyntax owner
            || property.AttributeLists.Count != 0
            || property.ExpressionBody is not null
            || !property.Modifiers.Any(SyntaxKind.PrivateKeyword)) {
            return;
        }

        foreach (var modifier in property.Modifiers) {
            if (modifier.IsKind(SyntaxKind.AbstractKeyword)
                || modifier.IsKind(SyntaxKind.ExternKeyword)
                || modifier.IsKind(SyntaxKind.PartialKeyword)
                || modifier.IsKind(SyntaxKind.RequiredKeyword)
                || modifier.IsKind(SyntaxKind.VirtualKeyword)
                || modifier.IsKind(SyntaxKind.OverrideKeyword)) {
                return;
            }
        }

        // ⚠ `{ get; }` or `{ get; set; }` only, and every accessor must be the auto form. `init` is
        // declined: a field cannot be set by an object initializer the way an `init` property can.
        if (property.AccessorList is not { } accessors
            || accessors.Accessors.Count is not (1 or 2)
            || !accessors.Accessors[0].IsKind(SyntaxKind.GetAccessorDeclaration)) {
            return;
        }

        var getOnly = accessors.Accessors.Count == 1;
        foreach (var accessor in accessors.Accessors) {
            if (accessor.Body is not null
                || accessor.ExpressionBody is not null
                || accessor.AttributeLists.Count != 0
                || accessor.Modifiers.Count != 0
                || !(accessor.IsKind(SyntaxKind.GetAccessorDeclaration)
                    || accessor.IsKind(SyntaxKind.SetAccessorDeclaration))) {
                return;
            }
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (model.GetDeclaredSymbol(property, cancellation) is not IPropertySymbol {
                DeclaredAccessibility: Accessibility.Private,
                IsAbstract: false,
                IsVirtual: false,
                IsOverride: false,
                RefKind: RefKind.None,
                ExplicitInterfaceImplementations.Length: 0
            } symbol
            || symbol.ContainingType.DeclaringSyntaxReferences.Length != 1
            || symbol.ContainingType.GetAttributes()
                .Any(static attribute => attribute.AttributeClass?.ToDisplayString()
                    is "System.Runtime.InteropServices.StructLayoutAttribute" or "System.SerializableAttribute"
                )) {
            return;
        }

        if (!IsReadAndWritten(model, owner, symbol, property.Initializer is not null, cancellation)) {
            return;
        }

        // ⚠ The accessor list, not the declaration. `PropertyDeclarationSyntax.DescendantTrivia`
        // reaches the property's *leading* trivia, so asking it silenced the rule on every property
        // carrying a `/// <summary>` — which is most of them in a documented codebase.
        var removed = TextSpan.FromBounds(property.Identifier.Span.End, accessors.Span.End);
        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(property.SyntaxTree, removed)) {
            return;
        }

        var edits = new List<(TextSpan Span, string Text)> {
            (removed, property.Initializer is null ? ";" : string.Empty)
        };

        // ⚠ `{ get; }` says "assignable from a constructor and nowhere else", and `readonly` is what
        // says the same thing about a field. Without it the fix would quietly widen the contract.
        if (getOnly) {
            edits.Add((new TextSpan(property.Type.SpanStart, 0), "readonly "));
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                property.Identifier.GetLocation(),
                FixEdits.Pack(edits.ToArray()),
                "Nothing outside this type can bind to `"
                + property.Identifier.ValueText
                + "`, so the generated backing field is the declaration"
            )
        );
    }

    /// <summary>
    ///     Whether the property is read at least once and written at least once inside its type.
    /// </summary>
    /// <remarks>
    ///     ⚠ A <c>nameof</c> argument is not counted as a read. It keeps working over a field, so it
    ///     cannot make the fix wrong — but it is not a read the compiler credits either, and letting
    ///     it stand in for one would produce a field that is assigned and never used.
    /// </remarks>
    static bool IsReadAndWritten(
        SemanticModel model,
        TypeDeclarationSyntax owner,
        IPropertySymbol symbol,
        bool hasInitializer,
        System.Threading.CancellationToken cancellation
    ) {
        var read = false;
        var written = hasInitializer;

        foreach (var node in owner.DescendantNodes()) {
            cancellation.ThrowIfCancellationRequested();
            if (node is not SimpleNameSyntax name
                || name.Identifier.ValueText != symbol.Name
                || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name, cancellation).Symbol, symbol)) {
                continue;
            }

            if (name.Ancestors()
                    .OfType<InvocationExpressionSyntax>()
                    .Any(static invocation => invocation.Expression is IdentifierNameSyntax {
                            Identifier.ValueText: "nameof"
                        }
                    )) {
                continue;
            }

            // The reference, plus the member access wrapping it when there is one — `this.Total`
            // and `Total` have to reach the same conclusion about which side of an `=` they are on.
            SyntaxNode reference = name;
            if (reference.Parent is MemberAccessExpressionSyntax access && access.Name == name) {
                reference = access;
            }

            if (reference.Parent is AssignmentExpressionSyntax assignment && assignment.Left == reference) {
                written = true;
                read |= !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression);
            } else if (reference.Parent is PrefixUnaryExpressionSyntax prefix
                       && IsIncrement(prefix.OperatorToken)
                       || reference.Parent is PostfixUnaryExpressionSyntax postfix
                       && IsIncrement(postfix.OperatorToken)) {
                written = true;
                read = true;
            } else {
                read = true;
            }

            if (read && written) {
                return true;
            }
        }

        return read && written;
    }

    static bool IsIncrement(SyntaxToken token) =>
        token.IsKind(SyntaxKind.PlusPlusToken) || token.IsKind(SyntaxKind.MinusMinusToken);
}
