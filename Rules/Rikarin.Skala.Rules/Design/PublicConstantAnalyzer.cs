using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Design;

/// <summary>
///     <c>SK6034</c> — an externally visible <c>const</c>, copied into every caller at compile time.
/// </summary>
/// <remarks>
///     ⚠ <b>This rule is about a distribution property, not a syntax property.</b> The compiler does not
///     emit a field read for <c>Limits.MaxRetries</c>; it emits the literal <c>3</c>, into the consumer's
///     assembly. Ship a new version of the library with the value changed and every assembly built
///     against the old one keeps the old number — no error, no warning, no binding failure, and nothing
///     in either build says the two disagree. <c>static readonly</c> is read at run time and does not
///     have this property.
///     <para>
///         The rule is semantic because the question is whether the field escapes the assembly, and that
///         is the field's accessibility <em>and</em> every containing type's, which is a walk over
///         symbols rather than over one declaration's modifiers.
///     </para>
///     <para>
///         ⚠
///         <b>
///             A project can declare which of its constants are frozen, and the analyzer knows nothing
///             about which names those are (#330).
///         </b> The rule's own rationale already says a value that
///         "can never change" is correctly <c>public const</c> — a protocol magic number, a format
///         version — and it had no way to be told. It does now:
///         <c>dotnet_code_quality.SK6034.frozen_constant_types</c> names the containing types whose
///         constants are contract, defaults to empty, and is read from the <c>.editorconfig</c> the
///         consumer already has. ⚠
///         <b>
///             The exemption is declared by the project, never recognised by the
///             analyzer.
///         </b> The first proposal on the issue keyed it on <c>allocated-ids.txt</c> and on the
///         type names <c>RuleIds</c>/<c>ExitCodes</c> — that is one repository's layout carried inside a
///         rule that ships to repositories which have neither, and it is the thing the working
///         agreement forbids. Skala declares its own four types in its own <c>.editorconfig</c>, like
///         any other consumer.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The fix drops an initialiser that is the type's default, because keeping it is
///             <c>CA1805</c>.
///         </b> <c>public const int Ok = 0;</c> rewritten to
///         <c>public static readonly int Ok = 0;</c> is an explicit default initialiser, which is
///         redundant on a field and legitimate only on a <c>const</c> — so the one-token swap traded
///         this rule's finding for the SDK's. Only value types are dropped: <c>const string X = null;</c>
///         would become an uninitialised non-nullable field and trade the finding for CS8618 instead.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PublicConstantAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.PublicConstantField);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var field = (FieldDeclarationSyntax)context.Node;

        var keyword = default(SyntaxToken);
        foreach (var modifier in field.Modifiers) {
            if (modifier.IsKind(SyntaxKind.ConstKeyword)) {
                keyword = modifier;
            }
        }

        if (!keyword.IsKind(SyntaxKind.ConstKeyword)) {
            return;
        }

        // ⚠ The fix replaces this one token. A preprocessor directive in its trivia means the token
        // the fix names may not be the token every branch compiles — the same guard SK6003 uses.
        if (keyword.ContainsDirectives) {
            return;
        }

        var declarator = field.Declaration.Variables.Count > 0 ? field.Declaration.Variables[0] : default;
        if (declarator is null) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) is not IFieldSymbol symbol) {
            return;
        }

        // ⚠ An interface's constants are excluded. A `static readonly` field in an interface is legal
        // and is not the same declaration — it changes what implementers see and what the runtime
        // initializes — so the one-token fix would not be the repair it claims to be.
        if (symbol.ContainingType is { TypeKind: TypeKind.Interface }) {
            return;
        }

        if (!IsExternallyVisible(symbol)) {
            return;
        }

        if (IsFrozenByTheProject(
                context.Options.AnalyzerConfigOptionsProvider.GetOptions(field.SyntaxTree),
                symbol.ContainingType
            )) {
            return;
        }

        var edits = ImmutableArray.CreateBuilder<(TextSpan Span, string Text)>();
        edits.Add((keyword.Span, "static readonly"));
        foreach (var variable in field.Declaration.Variables) {
            if (DefaultInitializer(context, variable) is { } redundant) {
                edits.Add((redundant, string.Empty));
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                keyword.GetLocation(),
                FixEdits.Pack(edits.ToArray()),
                "`"
                + declarator.Identifier.ValueText
                + "` is visible outside this assembly and `const`, so its value is copied into every "
                + "caller at compile time; shipping a new value leaves every caller that is not "
                + "rebuilt on the old one, with no error anywhere"
            )
        );
    }

    /// <summary>
    ///     The <c>= …</c> a <c>static readonly</c> field must not keep, or null when it may.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>CA1805</c>: an explicit default initialiser is redundant on a field and legitimate on a
    ///     <c>const</c>, so the one-token swap turns <c>public const int Ok = 0;</c> into an SDK finding
    ///     unless the initialiser goes with it (#330).
    ///     <para>
    ///         ⚠ <b>Value types only.</b> <c>const string Name = null;</c> is also "the type's default",
    ///         and dropping <em>that</em> initialiser leaves an uninitialised non-nullable field —
    ///         CS8618, which is a worse trade than the one being repaired.
    ///     </para>
    /// </remarks>
    static TextSpan? DefaultInitializer(SyntaxNodeAnalysisContext context, VariableDeclaratorSyntax variable) {
        if (variable.Initializer is not { } initializer
            || context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken)
            is not IFieldSymbol { HasConstantValue: true, Type.IsValueType: true } field
            || !IsTheTypesDefault(field.ConstantValue)) {
            return null;
        }

        var span = TextSpan.FromBounds(variable.Identifier.Span.End, initializer.Span.End);
        return RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(variable.SyntaxTree, span) ? null : span;
    }

    /// <summary>
    ///     Whether a constant value is the one the runtime would have produced anyway.
    /// </summary>
    /// <remarks>
    ///     ⚠ Floating point is left out on purpose: <c>-0.0</c> compares equal to <c>0.0</c> and is not
    ///     the same value, so <c>==</c> is the wrong question to ask about it and
    ///     <c>float.IsNegative</c> does not exist on netstandard2.0.
    /// </remarks>
    static bool IsTheTypesDefault(object? value) =>
        value switch {
            bool flag => !flag,
            char character => character == '\0',
            sbyte number => number == 0,
            byte number => number == 0,
            short number => number == 0,
            ushort number => number == 0,
            int number => number == 0,
            uint number => number == 0,
            long number => number == 0,
            ulong number => number == 0,
            decimal number => number == 0m,
            _ => false
        };

    /// <summary>
    ///     Whether the project has declared this containing type's constants frozen.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Declared, not recognised.</b> The key defaults to empty, so the rule reports every
    ///     externally visible <c>const</c> until a project says otherwise — which is the "on purpose
    ///     rather than by default" the rationale asks for. An entry matches either the type's own name or
    ///     its fully qualified one, so <c>ExitCodes</c> and
    ///     <c>Rikarin.Skala.Core.Diagnostics.ExitCodes</c> both work and a consumer with two types of one
    ///     name can say which.
    /// </remarks>
    static bool IsFrozenByTheProject(AnalyzerConfigOptions options, INamedTypeSymbol? containing) {
        if (containing is null
            || !options.TryGetValue(
                "dotnet_code_quality." + RuleIds.PublicConstantField + ".frozen_constant_types",
                out var configured
            )
            || string.IsNullOrWhiteSpace(configured)) {
            return false;
        }

        var qualified = containing.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty);

        foreach (var entry in configured!.Split(',')) {
            var name = entry.Trim();
            if (name.Length == 0) {
                continue;
            }

            if (string.Equals(name, containing.Name, StringComparison.Ordinal)
                || string.Equals(name, qualified, StringComparison.Ordinal)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether the symbol and every type containing it are visible outside the assembly.
    /// </summary>
    /// <remarks>
    ///     ⚠ The field's own accessibility is not enough. A <c>public const</c> inside an
    ///     <c>internal</c> class never leaves the assembly, so the value is never copied anywhere that
    ///     is compiled separately and there is nothing to report. Walking the containing chain is the
    ///     whole difference between this rule and one that reports every <c>public const</c> in a
    ///     program.
    /// </remarks>
    static bool IsExternallyVisible(ISymbol symbol) {
        for (var current = symbol; current is not null and not INamespaceSymbol; current = current.ContainingSymbol) {
            switch (current.DeclaredAccessibility) {
                case Accessibility.Public:
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                    continue;

                default:
                    return false;
            }
        }

        return true;
    }
}
