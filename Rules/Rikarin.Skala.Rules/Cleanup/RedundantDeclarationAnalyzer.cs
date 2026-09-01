using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary><c>SK0244</c> — a declaration whose deletion changes nothing the compiler does.</summary>
/// <remarks>
///     <para>
///         Six shapes: an empty finalizer, an empty sole constructor, an empty namespace, a
///         <c>: base()</c> with no arguments, a member initialized to the value it already has, and an
///         <c>override</c> whose body is a call to the member it overrides.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The empty finalizer is not merely redundant and it stays here at <c>warning</c> rather
///             than moving to the performance range.
///         </b> An empty <c>~Foo() { }</c> opts the type into
///         finalization: every instance is put on the finalizer queue, survives the collection that
///         would otherwise have taken it, and is freed a generation later — for a body that does
///         nothing. The reason it is not a second id is that the finding and the edit are the same as
///         the other five: a declaration that adds nothing, deleted. The cost is in the message and in
///         the rationale, where a reader will see it, rather than in a severity that would have made
///         the concept two.
///     </para>
///     <para>
///         ⚠ <b>Every shape is decided from the declaration and its enclosing type alone.</b> That is
///         what keeps the rule syntactic, and it is also what excludes the shapes that look similar and
///         are not decidable that way — a <c>partial</c> type whose other part may hold another
///         constructor, and an <c>override</c> whose return type may be covariant with the base's.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantDeclarationAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantDeclaration);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeDestructor, SyntaxKind.DestructorDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeInitializer, SyntaxKind.BaseConstructorInitializer);
        context.RegisterSyntaxNodeAction(
            AnalyzeNamespace,
            SyntaxKind.NamespaceDeclaration,
            SyntaxKind.FileScopedNamespaceDeclaration
        );

        context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeOverride, SyntaxKind.MethodDeclaration);
    }

    static void AnalyzeDestructor(SyntaxNodeAnalysisContext context) {
        var destructor = (DestructorDeclarationSyntax)context.Node;
        if (destructor.Body is { Statements.Count: 0 }
            && destructor.AttributeLists.Count == 0
            && IsDeletable(destructor)) {
            Report(
                context,
                destructor.Identifier.GetLocation(),
                destructor.FullSpan,
                "an empty finalizer is not free: it opts every instance of this type into finalization, so each "
                + "one survives the collection that would have taken it and is freed a generation later, for a "
                + "body that does nothing"
            );
        }
    }

    /// <summary>
    ///     A parameterless, empty, sole constructor whose accessibility is the one the compiler would
    ///     have given the constructor it generates.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>A <c>static</c> constructor is never this shape, and an empty one is not redundant.</b>
    ///     Declaring one — even empty — clears <c>beforefieldinit</c> on the type, which changes when
    ///     the runtime is allowed to run the type's initialization. Deleting it is a timing change, not
    ///     a cleanup.
    ///     <para>
    ///         ⚠ Classes only, and never a <c>partial</c> one or one with a primary constructor. In a
    ///         partial type the other part may declare the constructor that makes this one meaningful,
    ///         and neither is visible from here.
    ///     </para>
    /// </remarks>
    static void AnalyzeConstructor(SyntaxNodeAnalysisContext context) {
        var constructor = (ConstructorDeclarationSyntax)context.Node;
        if (IsRedundantConstructor(constructor) && IsDeletable(constructor)) {
            Report(
                context,
                constructor.Identifier.GetLocation(),
                constructor.FullSpan,
                "the parameterless constructor is empty and is the only one, so deleting it leaves the "
                + "compiler generating exactly this"
            );
        }
    }

    static bool IsRedundantConstructor(ConstructorDeclarationSyntax constructor) {
        if (constructor.Parent is not ClassDeclarationSyntax type
            || type.ParameterList is not null
            || constructor.ParameterList.Parameters.Count > 0
            || constructor.Body is not { Statements.Count: 0 }
            || constructor.AttributeLists.Count > 0
            || Has(constructor.Modifiers, SyntaxKind.StaticKeyword)
            || Has(type.Modifiers, SyntaxKind.PartialKeyword)) {
            return false;
        }

        // `: this(…)` runs another constructor; `: base(x)` passes something on. Only the initializer
        // the compiler supplies by itself may be present.
        if (constructor.Initializer is { } initializer
            && (!initializer.IsKind(SyntaxKind.BaseConstructorInitializer)
                || initializer.ArgumentList.Arguments.Count > 0)) {
            return false;
        }

        foreach (var member in type.Members) {
            if (member is ConstructorDeclarationSyntax other
                && other != constructor
                && !Has(other.Modifiers, SyntaxKind.StaticKeyword)) {
                return false;
            }
        }

        // ⚠ The generated constructor is `protected` on an abstract class and `public` on every other,
        // so an accessibility that differs from that one is doing something and is not this shape.
        var wanted = Has(type.Modifiers, SyntaxKind.AbstractKeyword)
            ? SyntaxKind.ProtectedKeyword
            : SyntaxKind.PublicKeyword;

        return Has(constructor.Modifiers, wanted) && Accessibility(constructor.Modifiers) == 1;
    }

    static int Accessibility(SyntaxTokenList modifiers) {
        var count = 0;
        foreach (var modifier in modifiers) {
            if (modifier.IsKind(SyntaxKind.PublicKeyword)
                || modifier.IsKind(SyntaxKind.ProtectedKeyword)
                || modifier.IsKind(SyntaxKind.PrivateKeyword)
                || modifier.IsKind(SyntaxKind.InternalKeyword)) {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    ///     ⚠ Withheld where the whole constructor is already the finding, or the two would delete
    ///     overlapping spans and <c>skala fix</c> would apply one on top of the other.
    /// </summary>
    static void AnalyzeInitializer(SyntaxNodeAnalysisContext context) {
        var initializer = (ConstructorInitializerSyntax)context.Node;
        if (initializer.ArgumentList.Arguments.Count > 0
            || initializer.Parent is not ConstructorDeclarationSyntax constructor
            || IsRedundantConstructor(constructor)
            || !HasNoCommentOrDirective(initializer)) {
            return;
        }

        var previous = initializer.ColonToken.GetPreviousToken();
        if (!IsWhitespaceOnly(previous.TrailingTrivia)) {
            return;
        }

        Report(
            context,
            initializer.GetLocation(),
            TextSpan.FromBounds(previous.Span.End, initializer.Span.End),
            "`: base()` is the constructor initializer the compiler supplies when none is written"
        );
    }

    static void AnalyzeNamespace(SyntaxNodeAnalysisContext context) {
        var declaration = (BaseNamespaceDeclarationSyntax)context.Node;
        if (declaration.Members.Count == 0
            && declaration.Usings.Count == 0
            && declaration.Externs.Count == 0
            && IsDeletable(declaration)) {
            Report(
                context,
                declaration.Name.GetLocation(),
                declaration.FullSpan,
                "the namespace declares nothing, so it names no type and nothing can import it"
            );
        }
    }

    /// <summary>
    ///     ⚠ A field whose only assignment <em>is</em> the initializer is left alone, and that guard is
    ///     the difference between a safe fix and one that breaks a warnings-as-errors build.
    /// </summary>
    /// <remarks>
    ///     CS0649 — "field is never assigned to, and will always have its default value" — is silenced by
    ///     an initializer, so deleting a redundant one turns a clean file into a warning. The finding
    ///     would be correct and the fix would still be the tool telling a build to fail. What is looked
    ///     for is any assignment, compound assignment, increment, decrement or <c>ref</c>/<c>out</c> use
    ///     of the name anywhere in the containing type; a field nothing writes keeps its initializer.
    ///     <para>
    ///         ⚠ Automatically implemented properties need no such guard — CS0649 is about fields, and a
    ///         property's generated backing field is never its subject.
    ///     </para>
    /// </remarks>
    static void AnalyzeField(SyntaxNodeAnalysisContext context) {
        var field = (FieldDeclarationSyntax)context.Node;
        if (Has(field.Modifiers, SyntaxKind.ConstKeyword)
            || field.Parent is not TypeDeclarationSyntax type) {
            return;
        }

        foreach (var declarator in field.Declaration.Variables) {
            if (!IsWrittenSomewhereElse(type, declarator.Identifier.ValueText)) {
                continue;
            }

            ReportDefaultInitializer(
                context,
                field.Declaration.Type,
                declarator.Initializer,
                "a field is zero-initialized before any code runs, so this initializer sets the value it "
                + "already had"
            );
        }
    }

    static bool IsWrittenSomewhereElse(TypeDeclarationSyntax type, string name) {
        foreach (var node in type.DescendantNodes()) {
            var written = node switch {
                AssignmentExpressionSyntax assignment => Names(assignment.Left, name),
                PrefixUnaryExpressionSyntax {
                    RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression
                } prefix => Names(prefix.Operand, name),
                PostfixUnaryExpressionSyntax {
                    RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression
                } postfix => Names(postfix.Operand, name),
                ArgumentSyntax { RefKindKeyword.RawKind: not (int)SyntaxKind.None } argument =>
                    Names(argument.Expression, name),
                _ => false
            };

            if (written) {
                return true;
            }
        }

        return false;
    }

    static bool Names(ExpressionSyntax expression, string name) =>
        expression switch {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == name,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: { } member } =>
                member.Identifier.ValueText == name,
            _ => false
        };

    /// <summary>
    ///     ⚠ Automatically implemented properties only. An accessor with a body stores wherever its
    ///     code says, and what that storage starts as is not a question the declaration answers.
    /// </summary>
    static void AnalyzeProperty(SyntaxNodeAnalysisContext context) {
        var property = (PropertyDeclarationSyntax)context.Node;
        if (property.AccessorList is not { } accessors) {
            return;
        }

        foreach (var accessor in accessors.Accessors) {
            if (accessor.Body is not null || accessor.ExpressionBody is not null) {
                return;
            }
        }

        if (!IsWhitespaceOnly(property.SemicolonToken.LeadingTrivia)) {
            return;
        }

        // ⚠ The semicolon goes with the initializer. `int X { get; set; } = 0;` needs it and
        // `int X { get; set; };` is CS1597, so a fix that deleted only the `= 0` would produce text
        // that does not parse — which is the one failure a fixing tool may not have.
        ReportDefaultInitializer(
            context,
            property.Type,
            property.Initializer,
            "an automatically implemented property's storage is zero-initialized before any code runs, so "
            + "this initializer sets the value it already had",
            property.SemicolonToken.Span.End
        );
    }

    /// <summary>
    ///     The initializers that are provably the declared type's own default, read from the written
    ///     type and the written literal.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         <c>= null</c> is accepted only on a type written with a <c>?</c>, and that restriction
    ///         is what keeps the fix safe rather than merely tidy.
    ///     </b> Under an enabled nullable context
    ///     <c>string name = null;</c> warns at the initializer (CS8625) and deleting it moves the
    ///     warning to CS8618 at the constructor — a *different* warning, which a fix the catalogue marks
    ///     safe may not introduce. On <c>string? name</c> and <c>int? count</c> there is no such warning
    ///     in either direction.
    ///     <para>
    ///         ⚠ <b><c>= default</c> is restricted the same way and for the same reason.</b> On a
    ///         non-nullable reference type it is null, so deleting it moves the warning exactly as
    ///         <c>= null</c> does. The accepted set is therefore a type written with a <c>?</c> and the
    ///         value-type predefined keywords, and nothing else: <c>string</c>, <c>object</c> and every
    ///         user-written type are out, because whether one is a struct is not visible in the
    ///         declaration.
    ///     </para>
    ///     <para>
    ///         Within that set the match is on the written literal: <c>0</c> for the integral and
    ///         floating-point keywords, <c>false</c> for <c>bool</c>, <c>default</c> for all of them.
    ///         <c>0f</c>, <c>0m</c> and <c>'\0'</c> are the same value written differently and are not
    ///         matched; an enum member, a named constant and a struct's own <c>Empty</c> are not
    ///         decidable here at all.
    ///     </para>
    /// </remarks>
    static void ReportDefaultInitializer(
        SyntaxNodeAnalysisContext context,
        TypeSyntax type,
        EqualsValueClauseSyntax? initializer,
        string message,
        int? end = null
    ) {
        if (initializer is null || !HasNoCommentOrDirective(initializer)) {
            return;
        }

        var previous = initializer.EqualsToken.GetPreviousToken();
        if (!IsWhitespaceOnly(previous.TrailingTrivia) || !IsDefaultFor(type, initializer.Value)) {
            return;
        }

        Report(
            context,
            initializer.GetLocation(),
            TextSpan.FromBounds(previous.Span.End, end ?? initializer.Span.End),
            message
        );
    }

    static bool IsDefaultFor(TypeSyntax type, ExpressionSyntax value) {
        var isDefault = value.IsKind(SyntaxKind.DefaultLiteralExpression) || value is DefaultExpressionSyntax;
        if (type is NullableTypeSyntax) {
            return isDefault || value.IsKind(SyntaxKind.NullLiteralExpression);
        }

        if (type is not PredefinedTypeSyntax predefined) {
            return false;
        }

        return predefined.Keyword.Kind() switch {
            SyntaxKind.BoolKeyword =>
                isDefault || (value is LiteralExpressionSyntax f && f.Token.IsKind(SyntaxKind.FalseKeyword)),
            SyntaxKind.CharKeyword => isDefault,
            SyntaxKind.IntKeyword
                or SyntaxKind.UIntKeyword
                or SyntaxKind.LongKeyword
                or SyntaxKind.ULongKeyword
                or SyntaxKind.ShortKeyword
                or SyntaxKind.UShortKeyword
                or SyntaxKind.ByteKeyword
                or SyntaxKind.SByteKeyword
                or SyntaxKind.FloatKeyword
                or SyntaxKind.DoubleKeyword
                or SyntaxKind.DecimalKeyword =>
                isDefault || (value is LiteralExpressionSyntax zero && zero.Token.Text == "0"),
            _ => false
        };
    }

    /// <summary>
    ///     An <c>override</c> whose body is exactly a call to the member it overrides.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b><c>void</c> only, and that is the covariant-return guard.</b> C# 9 lets an override
    ///     narrow its return type, and an override that does so is the declaration carrying the narrower
    ///     type — deleting it changes what every caller sees. Comparing the two return types needs the
    ///     base symbol, which this rule does not ask for, so the whole question is excluded by matching
    ///     the <c>void</c> keyword instead.
    ///     <para>
    ///         ⚠ <c>sealed</c>, an attribute, a type parameter list and a parameter with a default value
    ///         each withdraw the finding: the first three are the declaration's reason for existing, and
    ///         the last is a value the base may not have.
    ///     </para>
    /// </remarks>
    static void AnalyzeOverride(SyntaxNodeAnalysisContext context) {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!Has(method.Modifiers, SyntaxKind.OverrideKeyword)
            || Has(method.Modifiers, SyntaxKind.SealedKeyword)
            || method.AttributeLists.Count > 0
            || method.TypeParameterList is not null
            || method.ReturnType is not PredefinedTypeSyntax returnType
            || !returnType.Keyword.IsKind(SyntaxKind.VoidKeyword)
            || !IsDeletable(method)) {
            return;
        }

        foreach (var parameter in method.ParameterList.Parameters) {
            if (parameter.Default is not null || parameter.AttributeLists.Count > 0) {
                return;
            }
        }

        // ⚠ Not a list pattern. `Rikarin.Skala.Rules` targets netstandard2.0 (ADR-006: it loads into
        // csc and into Rider), where `System.Index` does not exist and one is CS0518.
        var call = method.ExpressionBody?.Expression;
        if (call is null
            && method.Body is { Statements.Count: 1 }
            && method.Body.Statements[0] is ExpressionStatementSyntax statement) {
            call = statement.Expression;
        }

        if (call is not InvocationExpressionSyntax {
                Expression:
                MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax, Name: SimpleNameSyntax name }
            } invocation
            || name.Identifier.ValueText != method.Identifier.ValueText
            || !ForwardsEveryParameter(method, invocation)) {
            return;
        }

        Report(
            context,
            method.Identifier.GetLocation(),
            method.FullSpan,
            "the override does nothing but call the base implementation, which is what happens when there "
            + "is no override at all"
        );
    }

    /// <summary>
    ///     ⚠ Every parameter, in order, by name, with the same <c>ref</c>/<c>out</c>/<c>in</c> modifier.
    /// </summary>
    static bool ForwardsEveryParameter(MethodDeclarationSyntax method, InvocationExpressionSyntax invocation) {
        var parameters = method.ParameterList.Parameters;
        var arguments = invocation.ArgumentList.Arguments;
        if (parameters.Count != arguments.Count) {
            return false;
        }

        for (var i = 0; i < parameters.Count; i++) {
            if (arguments[i] is not { NameColon: null, Expression: IdentifierNameSyntax identifier } argument
                || identifier.Identifier.ValueText != parameters[i].Identifier.ValueText
                || !SameByRefModifier(parameters[i], argument)) {
                return false;
            }
        }

        return true;
    }

    static bool SameByRefModifier(ParameterSyntax parameter, ArgumentSyntax argument) {
        var declared = SyntaxKind.None;
        foreach (var modifier in parameter.Modifiers) {
            if (modifier.IsKind(SyntaxKind.RefKeyword)
                || modifier.IsKind(SyntaxKind.OutKeyword)
                || modifier.IsKind(SyntaxKind.InKeyword)) {
                declared = modifier.Kind();
            }
        }

        // `in` is passed without a keyword at the call site as often as with one, and both mean the
        // same thing; `ref` and `out` must match exactly.
        var passed = argument.RefKindKeyword.Kind();
        return declared == passed
            || (declared == SyntaxKind.InKeyword && passed == SyntaxKind.None);
    }

    static void Report(SyntaxNodeAnalysisContext context, Location location, TextSpan span, string message) =>
        context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, FixEdits.Pack((span, string.Empty)), message));

    /// <summary>
    ///     Whether a whole declaration may be deleted with its own leading blank line and nothing else.
    /// </summary>
    /// <remarks>
    ///     ⚠ The fix deletes the declaration's <em>full</em> span, which is the only deletion that does
    ///     not leave an orphaned blank line behind — and the full span begins at the leading trivia. So
    ///     the leading trivia has to be whitespace: a documentation comment, an ordinary comment, a
    ///     <c>#region</c> or an attribute list all mean the deletion is carrying away something the
    ///     author wrote about, and the finding is withheld rather than the fix being made smaller.
    /// </remarks>
    static bool IsDeletable(SyntaxNode declaration) =>
        IsWhitespaceOnly(declaration.GetLeadingTrivia())
        && HasNoCommentOrDirective(declaration)
        && IsWhitespaceOnly(declaration.GetTrailingTrivia());

    static bool Has(SyntaxTokenList modifiers, SyntaxKind kind) {
        foreach (var modifier in modifiers) {
            if (modifier.IsKind(kind)) {
                return true;
            }
        }

        return false;
    }

    static bool HasNoCommentOrDirective(SyntaxNode node) {
        foreach (var trivia in node.DescendantTrivia(descendIntoTrivia: true)) {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
                || trivia.IsDirective) {
                return false;
            }
        }

        return true;
    }

    static bool IsWhitespaceOnly(SyntaxTriviaList trivia) {
        foreach (var item in trivia) {
            if (!item.IsKind(SyntaxKind.WhitespaceTrivia) && !item.IsKind(SyntaxKind.EndOfLineTrivia)) {
                return false;
            }
        }

        return true;
    }
}
