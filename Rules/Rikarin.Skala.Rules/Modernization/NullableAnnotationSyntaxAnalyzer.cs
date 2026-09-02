using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1094</c> — <c>[CanBeNull] string Name</c> is <c>string? Name</c>.
/// </summary>
/// <remarks>
///     <para>
///         ⚠ <b>This rule exists because of the retirement.</b> A codebase leaving ReSharper has to
///         drop its <c>JetBrains.Annotations</c> dependency, and every <c>[CanBeNull]</c> in it is a
///         nullability fact that either moves into <c>?</c> or is lost silently — the attribute stops
///         meaning anything the moment nothing reads it. No other tool reports these.
///     </para>
///     <para>
///         ⚠ <b>The annotation context is read at the declaration, not from the project.</b> In a
///         <c>#nullable disable</c> file the attribute is the only nullability signal there is, and
///         writing <c>T?</c> there produces <c>CS8632</c> and loses the fact. The rule withdraws
///         whenever the context at the site has annotations off, whichever way the compilation-level
///         default is set.
///     </para>
///     <para>
///         ⚠ <b><c>JetBrains.Annotations</c> only.</b> <c>System.Diagnostics.CodeAnalysis</c> is
///         deliberately untouched: <c>[MaybeNull] T Get&lt;T&gt;()</c> on an unconstrained <c>T</c>
///         says something <c>T?</c> genuinely cannot, which is the reason that attribute exists. Only
///         reference types are matched for the same reason — an unconstrained type parameter is not
///         one.
///     </para>
///     <para>
///         ⚠ <b><c>[NotNull]</c> on a type already written <c>T?</c> is declined.</b> The attribute and
///         the syntax disagree, and which of them the author meant is not something to guess at. The
///         three reported cases are unambiguous: <c>[CanBeNull]</c> on <c>T</c> becomes <c>T?</c>, and
///         <c>[CanBeNull]</c> on <c>T?</c> or <c>[NotNull]</c> on <c>T</c> is an attribute restating
///         what the type already says.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullableAnnotationSyntaxAnalyzer : DiagnosticAnalyzer {
    const string CanBeNull = "JetBrains.Annotations.CanBeNullAttribute";
    const string NotNull = "JetBrains.Annotations.NotNullAttribute";

    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.NullableAnnotationSyntax);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NullableAnnotationSyntax);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Attribute);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var attribute = (AttributeSyntax)context.Node;
        if (attribute.Parent is not AttributeListSyntax list
            || list.Target is not null
            || TypeOf(list.Parent) is not { } declaration) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (Kind(model, attribute, cancellation) is not { } kind) {
            return;
        }

        // ⚠ Both attributes on one declaration is a contradiction, and reporting it twice would emit
        // two fixes whose edits overlap on the same list.
        foreach (var sibling in Siblings(list.Parent!)) {
            if (sibling != attribute && Kind(model, sibling, cancellation) is not null) {
                return;
            }
        }

        // ⚠ The whole rule, in one call. `#nullable disable` at the site makes the attribute the only
        // statement of nullability in the file, and `T?` there is CS8632 and a lost fact.
        if ((model.GetNullableContext(declaration.SpanStart) & NullableContext.AnnotationsEnabled) == 0) {
            return;
        }

        if (model.GetTypeInfo(declaration, cancellation).Type is not { } type
            || type.TypeKind == TypeKind.Error
            || !type.IsReferenceType) {
            return;
        }

        var annotated = declaration is NullableTypeSyntax;
        string message;
        var edits = new List<(TextSpan Span, string Text)>();

        if (kind == CanBeNull && !annotated) {
            edits.Add((new TextSpan(declaration.Span.End, 0), "?"));
            message = "`[CanBeNull]` is `" + declaration + "?` in a nullable context";
        } else if (kind == CanBeNull || !annotated) {
            message = "The nullable context already says what `["
                + attribute.Name.ToString().Replace("Attribute", string.Empty)
                + "]` says";
        } else {
            // `[NotNull]` on a `T?`: the attribute and the syntax disagree and the repair depends on
            // which one the author meant.
            return;
        }

        if (Removal(list, attribute) is not { } removal) {
            return;
        }

        edits.Add((removal, string.Empty));
        context.ReportDiagnostic(
            Diagnostic.Create(Descriptor, attribute.GetLocation(), FixEdits.Pack(edits.ToArray()), message)
        );
    }

    /// <summary>Which of the two annotations this is, or null when it is neither.</summary>
    static string? Kind(SemanticModel model, AttributeSyntax attribute, System.Threading.CancellationToken token) {
        var name = model.GetSymbolInfo(attribute, token).Symbol?.ContainingType?.ToDisplayString();
        return name is CanBeNull or NotNull ? name : null;
    }

    /// <summary>Every attribute on the annotated declaration, across all of its lists.</summary>
    static IEnumerable<AttributeSyntax> Siblings(SyntaxNode owner) {
        foreach (var list in Lists(owner)) {
            foreach (var attribute in list.Attributes) {
                yield return attribute;
            }
        }
    }

    static SyntaxList<AttributeListSyntax> Lists(SyntaxNode owner) =>
        owner switch {
            FieldDeclarationSyntax field => field.AttributeLists,
            PropertyDeclarationSyntax property => property.AttributeLists,
            MethodDeclarationSyntax method => method.AttributeLists,
            ParameterSyntax parameter => parameter.AttributeLists,
            _ => default
        };

    /// <summary>The type syntax the annotation is talking about.</summary>
    static TypeSyntax? TypeOf(SyntaxNode? owner) =>
        owner switch {
            FieldDeclarationSyntax field => field.Declaration.Type,
            PropertyDeclarationSyntax property => property.Type,
            MethodDeclarationSyntax method => method.ReturnType,
            ParameterSyntax parameter => parameter.Type,
            _ => null
        };

    /// <summary>
    ///     The span the fix deletes to remove one attribute.
    /// </summary>
    /// <remarks>
    ///     ⚠ For a list holding nothing else, the <em>full</em> span goes: deleting only
    ///     <c>[CanBeNull]</c> would leave the indentation and the newline it sat on behind, and the
    ///     formatter is not allowed to remove a blank line the author appears to have written. For a
    ///     list holding others, the attribute and one comma go and the brackets stay.
    /// </remarks>
    static TextSpan? Removal(AttributeListSyntax list, AttributeSyntax attribute) {
        if (RewriteGuards.ContainsCommentOrDirective(list) || list.ContainsDirectives) {
            return null;
        }

        if (list.Attributes.Count == 1) {
            return list.FullSpan;
        }

        var index = list.Attributes.IndexOf(attribute);
        return index == 0
            ? TextSpan.FromBounds(attribute.SpanStart, list.Attributes[1].SpanStart)
            : TextSpan.FromBounds(list.Attributes[index - 1].Span.End, attribute.Span.End);
    }
}
