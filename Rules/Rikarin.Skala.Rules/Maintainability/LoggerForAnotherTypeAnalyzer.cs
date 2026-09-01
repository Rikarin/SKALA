using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7110</c> — a type declaring an <c>ILogger&lt;T&gt;</c> for a <c>T</c> that is not itself.
/// </summary>
/// <remarks>
///     The type argument is the log <em>category</em>. A class holding an <c>ILogger&lt;Other&gt;</c>
///     files every message it ever writes under another class's name, so a filter that selects this
///     class misses them and a filter that selects the other one collects messages it never sent.
///     Nothing fails, nothing is slow, and every log query about either class is quietly wrong — which
///     is worse than noise, because noise is visible.
///     <para>
///         ⚠
///         <b>
///             This is the objective quarter of its issue, and the other three are declined on
///             purpose.
///         </b> <c>S6669</c> (the field's <em>name</em>) and <c>S1312</c> (whether it is
///         <c>private static readonly</c>) are naming and declaration conventions — the most
///         opinionated thing a linter can hold, and a repository's to settle rather than a defect. This
///         one has a consequence a person can be shown: the category is wrong, and it is wrong in a way
///         no reader of the file can see. That is also why it ships enabled rather than at
///         <c>none</c> — the opt-in default that <c>SK7010</c> and <c>SK7101</c> use is for rules whose
///         finding is a preference.
///     </para>
///     <para>
///         ⚠ <b>A base type of the declaring type is accepted.</b> A hierarchy that files its whole
///         family under the base class's category is a deliberate grouping, and the type argument still
///         names something the reader can find the declaration in. Only an unrelated type is reported.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LoggerForAnotherTypeAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.LoggerDeclaredForAnotherType);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var logger = start.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.ILogger`1");
                if (logger is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(context => Analyze(context, logger), SyntaxKind.GenericName);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol loggerOfT) {
        var name = (GenericNameSyntax)context.Node;
        if (name.TypeArgumentList.Arguments.Count != 1 || !IsDeclaredMemberType(name)) {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol
            is not INamedTypeSymbol { IsGenericType: true } constructed
            || !SymbolEqualityComparer.Default.Equals(constructed.OriginalDefinition, loggerOfT)) {
            return;
        }

        // ⚠ A type *parameter* is not a wrong category, it is a generic helper deciding one at its
        // use site, and there is no name a fix could write in its place.
        if (constructed.TypeArguments[0] is not INamedTypeSymbol argument) {
            return;
        }

        if (EnclosingType(name) is not { } declaration
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
            is not { } enclosing
            || IsSelfOrBase(enclosing, argument)) {
            return;
        }

        var replacement = declaration.Identifier.ValueText
            + (declaration.TypeParameterList?.ToString() ?? string.Empty);
        var span = name.TypeArgumentList.Arguments[0].Span;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                name.TypeArgumentList.Arguments[0].GetLocation(),
                FixEdits.Pack((new TextSpan(span.Start, span.Length), replacement)),
                "`"
                + enclosing.Name
                + "` declares an `ILogger<"
                + argument.Name
                + ">`, so every message it writes is filed under `"
                + argument.Name
                + "`'s category rather than its own"
            )
        );
    }

    /// <summary>
    ///     Whether this <c>ILogger&lt;T&gt;</c> is the declared type of a member rather than a mention.
    /// </summary>
    /// <remarks>
    ///     ⚠ Fields, properties and parameters only — the places a type <em>keeps</em> a logger. A
    ///     <c>typeof(ILogger&lt;Other&gt;)</c>, a local variable in a composition root and a factory's
    ///     return type all name another class's logger on purpose, and reporting them would turn a rule
    ///     about one type's own category into a rule against dependency injection.
    /// </remarks>
    static bool IsDeclaredMemberType(GenericNameSyntax name) =>
        name.Parent switch {
            VariableDeclarationSyntax variable => variable.Type == name && variable.Parent is FieldDeclarationSyntax,
            PropertyDeclarationSyntax property => property.Type == name,
            ParameterSyntax parameter => parameter.Type == name && IsConstructorParameter(parameter),
            _ => false
        };

    /// <summary>
    ///     ⚠ A constructor parameter — ordinary or primary — is the one parameter that <em>becomes</em>
    ///     a member. An ordinary method parameter is a logger passed through, and the type that
    ///     declares the method is not the one whose category it names.
    /// </summary>
    static bool IsConstructorParameter(ParameterSyntax parameter) =>
        parameter.Parent?.Parent is ConstructorDeclarationSyntax or TypeDeclarationSyntax;

    /// <summary>
    ///     The type declaration this syntax sits in, which for a primary-constructor parameter is the
    ///     declaration itself rather than a constructor body.
    /// </summary>
    static TypeDeclarationSyntax? EnclosingType(SyntaxNode node) => node.FirstAncestorOrSelf<TypeDeclarationSyntax>();

    static bool IsSelfOrBase(ISymbol enclosing, INamedTypeSymbol argument) {
        for (var type = enclosing as INamedTypeSymbol; type is not null; type = type.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, argument.OriginalDefinition)) {
                return true;
            }
        }

        return false;
    }
}
