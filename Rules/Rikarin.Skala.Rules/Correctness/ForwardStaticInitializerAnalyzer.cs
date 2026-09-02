using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2130</c> — a static field initializer that reads a static field declared below it.
/// </summary>
/// <remarks>
///     Static field initializers run in <b>declaration order</b>, so an initializer that reads a field
///     declared later reads that field's <c>default</c> rather than its initialized value. Nothing
///     throws and nothing warns; the field simply holds zero, or null, for the life of the process.
///     <para>
///         ⚠ <b>Being exact about which construct this is matters more here than anywhere else in the
///         batch, because three neighbouring shapes look identical and are all correct.</b> A static
///         <em>property</em> is a method that runs when it is called, so a getter naming a field below
///         it returns whatever the field holds at that moment. A static <em>method</em> is not ordered
///         against anything. And a <c>static</c> constructor runs <em>after</em> every field
///         initializer has run, so a read from there sees every field fully initialized. Only a field
///         initializer is ordered against other field initializers, and only that ordering is a defect.
///     </para>
///     <para>
///         ⚠ <b>The referenced field must have an initializer of its own</b>, and that requirement is
///         what keeps the message honest rather than merely reducing the count. A field with no
///         initializer reads as <c>default</c> from an earlier initializer <em>and</em> from a later
///         one — the declaration order is not what makes it default, so a finding blaming the order
///         would be pointing at the wrong thing. The same is true of a field the static constructor
///         assigns: the static constructor runs last regardless of where the field is written down.
///     </para>
///     <para>
///         ⚠ <b>A <c>const</c> is excluded on both sides.</b> Its value is substituted at compile time,
///         so no ordering exists between two of them and reading one from a field initializer can never
///         see a default.
///     </para>
///     <para>
///         ⚠ <b>Reads inside a lambda or a local function are excluded</b>, and that is the exclusion
///         that would otherwise make this rule wrong on idiomatic code:
///         <c>static Func&lt;int&gt; f = () =&gt; later;</c> stores a delegate, and the body runs
///         whenever somebody invokes it — long after every initializer has finished. Deferred code is
///         the ordinary way to <em>fix</em> this defect, so firing on it would report the repair.
///     </para>
///     <para>
///         ⚠ <b>Only within one file.</b> A <c>partial</c> type's parts have no defined initializer
///         order between them — it follows the order the files reach the compiler, which is a build
///         detail rather than something the source states — so a cross-file pair is declined rather
///         than guessed at. That is the case ReSharper's own description calls out as the worst one,
///         and declining it is the honest answer: the finding would not be reproducible from the file
///         it lands in, which is exactly what <c>scope: Semantic</c> promises the analysis cache.
///     </para>
///     <para>
///         Report-only. The repair is to move a declaration, and which of the two moves — hoisting the
///         referenced field or sinking the reader — is right depends on what else in the type depends
///         on the order, which the pair alone does not say.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ForwardStaticInitializerAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ForwardStaticInitializer);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var field = (FieldDeclarationSyntax)context.Node;

        // A `const` is substituted at compile time, so it takes part in no ordering at all.
        if (!field.Modifiers.Any(SyntaxKind.StaticKeyword) || field.Modifiers.Any(SyntaxKind.ConstKeyword)) {
            return;
        }

        foreach (var declarator in field.Declaration.Variables) {
            if (declarator.Initializer is null) {
                continue;
            }

            if (context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken)
                is not IFieldSymbol reader) {
                continue;
            }

            var reported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var identifier in declarator.Initializer.Value.DescendantNodesAndSelf()
                .OfType<IdentifierNameSyntax>()) {
                if (Deferred(identifier, declarator.Initializer.Value) || InNameof(identifier)) {
                    continue;
                }

                var target = Forward(context, identifier, reader, declarator);
                if (target is null || !reported.Add(target.Name)) {
                    continue;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        identifier.GetLocation(),
                        "`"
                        + reader.Name
                        + "`'s initializer reads `"
                        + target.Name
                        + "`, which is declared below it; field initializers run in declaration order, so this "
                        + "reads `default` rather than the initialized value"
                    )
                );
            }
        }
    }

    /// <summary>
    ///     The static field of this same type, declared below this declarator in this same file, with an
    ///     initializer of its own — or null when the identifier is anything else at all.
    /// </summary>
    static IFieldSymbol? Forward(
        SyntaxNodeAnalysisContext context,
        IdentifierNameSyntax identifier,
        IFieldSymbol reader,
        VariableDeclaratorSyntax declarator
    ) {
        if (context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol
            is not IFieldSymbol target) {
            return null;
        }

        // A static property's getter runs on access and a static method is not ordered against
        // anything; only a field takes part in the initializer sequence. `IFieldSymbol` is that test,
        // and it is the whole of it.
        if (!target.IsStatic
            || target.IsConst
            || SymbolEqualityComparer.Default.Equals(target, reader)
            || !SymbolEqualityComparer.Default.Equals(target.ContainingType, reader.ContainingType)) {
            return null;
        }

        if (target.DeclaringSyntaxReferences.Length != 1) {
            return null;
        }

        var reference = target.DeclaringSyntaxReferences[0];
        if (reference.SyntaxTree != declarator.SyntaxTree) {
            // A `partial` type split across files has no stated order between its parts.
            return null;
        }

        // ⚠ The referenced field must carry an initializer. Without one it reads `default` from any
        // position, so the declaration order is not what makes the value wrong and a finding blaming
        // the order would be pointing at the wrong thing.
        return reference.GetSyntax(context.CancellationToken) is VariableDeclaratorSyntax declaration
            && declaration.Initializer is not null
            && declaration.SpanStart > declarator.SpanStart
                ? target
                : null;
    }

    /// <summary>
    ///     Whether the read sits inside a lambda, an anonymous method or a local function, and therefore
    ///     runs when that body is invoked rather than when the initializer is evaluated.
    /// </summary>
    /// <remarks>
    ///     ⚠ The boundary node is <b>tested and then stopped at</b>, not stopped at before being
    ///     tested. <c>static Func&lt;int&gt; f = () =&gt; later;</c> makes the lambda the initializer
    ///     expression itself, so a walk that halted on reaching the boundary never looked at the one
    ///     node that mattered — and the fixture that pins this exclusion is what caught it.
    /// </remarks>
    static bool Deferred(SyntaxNode node, SyntaxNode initializer) {
        for (var current = node; current is not null; current = current.Parent) {
            if (current is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax) {
                return true;
            }

            if (current == initializer) {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     <c>nameof(Later)</c> names a member and reads nothing; it is a compile-time string.
    /// </summary>
    static bool InNameof(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            if (current is InvocationExpressionSyntax {
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" }
                }) {
                return true;
            }

            if (current is MemberDeclarationSyntax) {
                return false;
            }
        }

        return false;
    }
}
