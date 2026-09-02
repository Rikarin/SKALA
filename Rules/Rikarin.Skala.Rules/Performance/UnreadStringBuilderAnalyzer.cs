using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>
///     <c>SK4041</c> — a local <c>StringBuilder</c> that is appended to and whose text is never read.
/// </summary>
/// <remarks>
///     <para>
///         Every <c>Append</c> copies characters into a buffer that is then collected. The work, the
///         allocation and the growth are all real and the result is discarded — usually because a
///         <c>return builder.ToString();</c> was never written, occasionally because the code that
///         read it was deleted and the code that filled it was not.
///     </para>
///     <para>
///         ⚠ <b>A builder handed to anything else escapes, and the rule then stands down.</b>
///         A method taking one may read it, store it, or return its text, and
///         nothing in this compilation is required to say so. So a reference that is not the receiver
///         of a mutating member — an argument, an assignment, a return, a capture inside a lambda or a
///         local function — ends the analysis for that local rather than being reasoned about.
///     </para>
///     <para>
///         ⚠ <b>The initializer has to be <c>new StringBuilder(…)</c>.</b> A builder obtained from a
///         pool or from a factory is shared with whatever handed it over, and "nothing in this method
///         reads it" says nothing at all about it.
///     </para>
///     <para>
///         ⚠ <b>No fix, and the reason is the finding.</b> The edit that repairs this is the read that
///         was never written, and no analysis knows what the author meant to do with the text. Deleting
///         the builder is the other candidate and is wrong whenever the arguments to the appends have
///         side effects — <c>builder.Append(Next())</c> — so the rule reports and stops, in the same
///         way <c>SK4024</c> does.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnreadStringBuilderAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UnreadStringBuilder);

    /// <summary>
    ///     ⚠ The members that only <em>write</em> to the buffer. Everything else is a read.
    /// </summary>
    /// <remarks>
    ///     The list is deliberately short and the default is "this is a read", so a member added to
    ///     <c>StringBuilder</c> in a later framework silences the rule rather than making it wrong.
    ///     <c>Length</c> and <c>Capacity</c> are absent because reading either is a read, and the rule
    ///     cannot tell a read of a property from a write to it by name alone.
    /// </remarks>
    static readonly string[] Writes = [
        "Append", "AppendLine", "AppendFormat", "AppendJoin", "Insert", "Remove", "Replace", "Clear",
        "EnsureCapacity"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var builder = start.Compilation.GetTypeByMetadataName("System.Text.StringBuilder");
                if (builder is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, builder),
                    SyntaxKind.LocalDeclarationStatement
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol stringBuilder) {
        var declaration = (LocalDeclarationStatementSyntax)context.Node;

        // ⚠ One declarator. `var a = new StringBuilder(); var b = a;` is two names for one buffer,
        // and `StringBuilder a = new(), b = new();` would need the references split between them.
        if (declaration.Declaration.Variables.Count != 1) {
            return;
        }

        var declarator = declaration.Declaration.Variables[0];
        if (declarator.Initializer is not { Value: { } initializer }
            || initializer is not BaseObjectCreationExpressionSyntax) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (model.GetDeclaredSymbol(declarator, cancellation) is not ILocalSymbol local
            || !SymbolEqualityComparer.Default.Equals(local.Type, stringBuilder)
            || !SymbolEqualityComparer.Default.Equals(
                model.GetTypeInfo(initializer, cancellation).Type,
                stringBuilder
            )) {
            return;
        }

        // ⚠ The whole member, not the enclosing block. A builder filled inside an `if` and read after
        // it is read, and a scan that stopped at the block would report it.
        var scope = Scope(declaration);
        if (scope is null) {
            return;
        }

        var appended = false;
        foreach (var node in scope.DescendantNodes()) {
            if (node is not IdentifierNameSyntax name
                || !string.Equals(name.Identifier.ValueText, local.Name, StringComparison.Ordinal)
                || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name, cancellation).Symbol, local)) {
                continue;
            }

            // ⚠ Inside a lambda or a local function the reference outlives the syntax around it: the
            // delegate may be stored, returned or run later, and whether the buffer is read is then
            // a question about the delegate's callers.
            if (Deferred(name, scope)) {
                return;
            }

            switch (Classify(name)) {
                case Use.Write:
                    appended = true;
                    continue;

                case Use.Ignorable:
                    continue;

                default:
                    return;
            }
        }

        if (!appended) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declarator.Identifier.GetLocation(),
                "`"
                + local.Name
                + "` is filled and never read: nothing in this member calls `ToString`, indexes it, "
                + "or hands it to anything that could"
            )
        );
    }

    enum Use {
        /// <summary>A mutating member called on the local, whose result is discarded or chained.</summary>
        Write,

        /// <summary>The declaration itself.</summary>
        Ignorable,

        /// <summary>Anything else, which may read the buffer or let somebody else read it.</summary>
        Read
    }

    static Use Classify(IdentifierNameSyntax name) {
        if (name.Parent is VariableDeclaratorSyntax) {
            return Use.Ignorable;
        }

        if (name.Parent is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } access
            || access.Expression != name
            || access.Parent is not InvocationExpressionSyntax invocation
            || Array.IndexOf(Writes, access.Name.Identifier.ValueText) < 0) {
            return Use.Read;
        }

        // ⚠ The call's *result* is the builder again, so `builder.Append(a).Append(b);` is one
        // reference and two writes — but `var text = builder.Append(a).ToString();` reads it through
        // the same chain. Walking up until the expression stops being a call on the builder is what
        // separates the two.
        return Discarded(invocation) ? Use.Write : Use.Read;
    }

    /// <summary>Whether the value a chain of mutating calls produces is thrown away.</summary>
    static bool Discarded(ExpressionSyntax expression) {
        while (true) {
            switch (expression.Parent) {
                case ExpressionStatementSyntax:
                    return true;

                case MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
                } access when access.Expression == expression: {
                    if (Array.IndexOf(Writes, access.Name.Identifier.ValueText) < 0
                        || access.Parent is not InvocationExpressionSyntax chained) {
                        return false;
                    }

                    expression = chained;
                    continue;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>Whether the reference sits inside a lambda or local function nested in the scope.</summary>
    static bool Deferred(SyntaxNode reference, SyntaxNode scope) {
        for (var current = reference.Parent; current is not null && current != scope; current = current.Parent) {
            if (current is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax) {
                return true;
            }
        }

        return false;
    }

    /// <summary>The member body a local lives in — as far as a reference to it can reach.</summary>
    static SyntaxNode? Scope(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case BaseMethodDeclarationSyntax method:
                    return (SyntaxNode?)method.Body ?? method.ExpressionBody;

                case AccessorDeclarationSyntax accessor:
                    return (SyntaxNode?)accessor.Body ?? accessor.ExpressionBody;

                case LocalFunctionStatementSyntax function:
                    return (SyntaxNode?)function.Body ?? function.ExpressionBody;

                case AnonymousFunctionExpressionSyntax lambda:
                    return lambda.Body;

                // ⚠ A top-level program has no member declaration; the compilation unit is the body.
                case GlobalStatementSyntax { Parent: { } unit }:
                    return unit;
            }
        }

        return null;
    }
}
