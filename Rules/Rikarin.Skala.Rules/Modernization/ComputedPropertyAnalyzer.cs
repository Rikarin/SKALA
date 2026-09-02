using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1090</c> — <c>public string Scheme { get; } = "https";</c> is
///     <c>public string Scheme =&gt; "https";</c>.
/// </summary>
/// <remarks>
///     <para>
///         ⚠
///         <b>
///             The reason this concept has failed before is that an auto-property is part of the type's
///             layout, and a serializer writes it by reflection with nothing in the source saying so.
///         </b>
///         Newtonsoft.Json writes a private setter by default and needs no attribute to do it, so
///         turning a settable auto-property into a computed one changes what deserialization produces
///         with no diagnostic anywhere. The line this rule draws is not caution, it is a different
///         fact: a <em>get-only</em> auto-property's backing field is emitted <c>initonly</c>, and
///         .NET Core's <see cref="System.Reflection.FieldInfo.SetValue(object, object)" /> refuses an
///         init-only field. There is no reflection path that writes it, so its disappearance is not
///         observable from outside the type.
///     </para>
///     <para>
///         ⚠ <b>The initializer must be a compile-time constant, decided by Roslyn's own folding.</b>
///         That is what makes evaluating it per read the same program as evaluating it once:
///         <c>=&gt; 3</c> is inlined and allocates nothing, whereas
///         <c>public List&lt;int&gt; Items { get; } = new();</c> rewritten to <c>=&gt; new()</c> hands
///         every caller a different list. A <c>typeof</c>, a <c>static readonly</c> reference and any
///         call are all declined for the same reason: none of them is a constant.
///     </para>
///     <para>
///         ⚠ <b>A get-only auto-property is assignable from the declaring type's constructors</b>, and
///         the fix turns that into <c>CS0200</c>. Every assignment in the type is looked for, which is
///         also why a type declared across more than one file is declined — the census cannot see the
///         other file.
///     </para>
///     <para>
///         ⚠ <b>Classes only.</b> In a struct the backing field <em>is</em> part of the size, so
///         removing it changes <c>sizeof</c>, interop marshalling and every blittable assumption made
///         about the layout. A record is excluded on the same principle: its synthesized members are
///         written against the fields.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ComputedPropertyAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.ComputedProperty);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ComputedProperty);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.PropertyDeclaration);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var property = (PropertyDeclarationSyntax)context.Node;

        // ⚠ A class, never a struct or a record. `ClassDeclarationSyntax` also matches nothing a
        // record declares — `RecordDeclarationSyntax` is its own node kind even for a record class.
        if (property.Parent is not ClassDeclarationSyntax owner
            || property.Initializer is not { } initializer
            || property.AttributeLists.Count != 0
            || property.ExpressionBody is not null
            || property.ContainsDirectives
            || property.SpanContainsComment()) {
            return;
        }

        foreach (var modifier in property.Modifiers) {
            if (modifier.IsKind(SyntaxKind.AbstractKeyword)
                || modifier.IsKind(SyntaxKind.ExternKeyword)
                || modifier.IsKind(SyntaxKind.PartialKeyword)
                || modifier.IsKind(SyntaxKind.RequiredKeyword)) {
                return;
            }
        }

        // ⚠ Get-only, and the accessor has to be the auto one. `{ get; private set; }` is settable
        // by reflection without the attribute a reader would look for, which is the whole wall.
        if (property.AccessorList is not { Accessors.Count: 1 } accessors
            || !accessors.Accessors[0].IsKind(SyntaxKind.GetAccessorDeclaration)
            || accessors.Accessors[0].Body is not null
            || accessors.Accessors[0].ExpressionBody is not null
            || accessors.Accessors[0].AttributeLists.Count != 0) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (!model.GetConstantValue(initializer.Value, cancellation).HasValue) {
            return;
        }

        if (model.GetDeclaredSymbol(property, cancellation) is not IPropertySymbol {
                IsAbstract: false,
                RefKind: RefKind.None
            } symbol
            || symbol.ContainingType.DeclaringSyntaxReferences.Length != 1
            || symbol.ContainingType.GetAttributes()
                .Any(static attribute => attribute.AttributeClass?.ToDisplayString()
                    is "System.Runtime.InteropServices.StructLayoutAttribute" or "System.SerializableAttribute"
                )) {
            return;
        }

        // ⚠ CS0200 if this is missed: a get-only auto-property may be assigned from the declaring
        // type's constructors, and a computed one may not be assigned at all.
        foreach (var node in owner.DescendantNodes()) {
            cancellation.ThrowIfCancellationRequested();
            var written = node switch {
                AssignmentExpressionSyntax assignment => assignment.Left,
                PrefixUnaryExpressionSyntax prefix when IsIncrement(prefix.OperatorToken) => prefix.Operand,
                PostfixUnaryExpressionSyntax postfix when IsIncrement(postfix.OperatorToken) => postfix.Operand,
                _ => null
            };

            if (written is not null
                && SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(written, cancellation).Symbol,
                    symbol
                )) {
                return;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                property.Identifier.GetLocation(),
                FixEdits.Pack(
                    (
                        TextSpan.FromBounds(accessors.SpanStart, property.SemicolonToken.Span.End),
                        "=> " + initializer.Value + ";"
                    )
                ),
                "The property can only ever hold `" + RewriteGuards.Trim(initializer.Value.ToString()) + "`"
            )
        );
    }

    static bool IsIncrement(SyntaxToken token) =>
        token.IsKind(SyntaxKind.PlusPlusToken) || token.IsKind(SyntaxKind.MinusMinusToken);
}
