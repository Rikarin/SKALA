using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Design;

/// <summary>
///     <c>SK6003</c> — a <c>public</c> constructor on an <c>abstract class</c> is a <c>protected</c> one.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK6000 — API and design". An abstract class cannot be
///     instantiated, so the only caller its constructor can ever have is a derived constructor's
///     initializer — which is what <c>protected</c> means. <c>public</c> there claims an audience that
///     cannot exist, and a reader scanning the type's public surface has to work out that this member
///     is not part of it.
///     <para>
///         ⚠ Purely syntactic, and that is the whole design. The two facts the rule needs — "this
///         declaration carries <c>abstract</c>" and "this constructor carries <c>public</c>" — are both
///         tokens in the same file, so the rule runs under <c>--load=loose</c> and cannot be silenced by an
///         unresolved symbol. The cost is a miss rather than a wrong answer: a partial class whose
///         <c>abstract</c> modifier sits in another file is not reported.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AbstractTypeConstructorAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.AbstractTypePublicConstructor);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
            start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ConstructorDeclaration)
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var constructor = (ConstructorDeclarationSyntax)context.Node;

        // ⚠ `class` only. A positional record's primary constructor is a parameter list with no
        // ConstructorDeclarationSyntax to edit, and a record's copy constructor has accessibility
        // rules the compiler enforces itself (CS8878) — so the one-keyword edit is not obviously
        // legal on a record and the rule says nothing there.
        if (constructor.Parent is not ClassDeclarationSyntax declaration
            || !HasModifier(declaration.Modifiers, SyntaxKind.AbstractKeyword)) {
            return;
        }

        // A static constructor has no accessibility to change; the language forbids one.
        if (HasModifier(constructor.Modifiers, SyntaxKind.StaticKeyword)) {
            return;
        }

        if (PublicKeyword(constructor) is not { } keyword) {
            return;
        }

        // ⚠ A partial constructor's two halves must state the same accessibility (CS8799), so the fix
        // edits both or neither (#397). The implementation reports, because no syntax-node action ever
        // visits the definition; and a definition outside this type declaration is declined rather than
        // edited by half.
        var edits = new[] { (keyword.Span, "protected") };
        if (PartialMembers.IsPartialMember(constructor)) {
            if (!PartialMembers.IsImplementation(constructor)
                || PartialMembers.Sibling(constructor) is not ConstructorDeclarationSyntax definition
                || PublicKeyword(definition) is not { } definitionKeyword) {
                return;
            }

            edits = [(keyword.Span, "protected"), (definitionKeyword.Span, "protected")];
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                keyword.GetLocation(),
                FixEdits.Pack(edits),
                "`"
                + declaration.Identifier.ValueText
                + "` is abstract, so only a derived constructor can call this; make it `protected`"
            )
        );
    }

    /// <summary>
    ///     The constructor's <c>public</c>, when it is the whole of its accessibility and the fix may
    ///     replace it.
    /// </summary>
    static SyntaxToken? PublicKeyword(ConstructorDeclarationSyntax constructor) {
        // ⚠ Exactly `public`, and nothing beside it. `protected internal` and `private protected`
        // are each a deliberate statement about a different audience, and `internal` on an abstract
        // type's constructor is the documented way to close a hierarchy to one assembly.
        SyntaxToken? keyword = null;
        foreach (var modifier in constructor.Modifiers) {
            switch ((SyntaxKind)modifier.RawKind) {
                case SyntaxKind.PublicKeyword:
                    keyword = modifier;
                    break;

                case SyntaxKind.ProtectedKeyword:
                case SyntaxKind.InternalKeyword:
                case SyntaxKind.PrivateKeyword:
                    return null;
            }
        }

        // ⚠ The fix replaces one token. If a preprocessor directive lives in its trivia, the token
        // the fix names may not be the token every branch compiles, which is the same reason
        // FileScopedNamespaceAnalyzer refuses a namespace that contains directives.
        return keyword is { ContainsDirectives: false } ? keyword : null;
    }

    static bool HasModifier(SyntaxTokenList modifiers, SyntaxKind kind) {
        foreach (var modifier in modifiers) {
            if (modifier.IsKind(kind)) {
                return true;
            }
        }

        return false;
    }
}
