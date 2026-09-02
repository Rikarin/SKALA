using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2120</c> — a bitwise operator combines members of a consecutively numbered enum.
/// </summary>
/// <remarks>
///     <c>enum Color { Red, Green, Blue }</c> numbers its members <c>0, 1, 2</c>, so
///     <c>Green | Blue</c> is <c>3</c> — a value no member declares. It then matches no
///     <c>case</c>, equals no member and prints as the number. The operation is legal C# and the
///     compiler says nothing at all.
///     <para>
///         ⚠ <b>The trigger is implicit numbering, not the absence of <c>[Flags]</c>.</b> An enum whose
///         author wrote values down — <c>Read = 1, Write = 2</c> — may well be a bit set that is
///         merely missing its attribute, and the declaration alone cannot distinguish that from a
///         deliberate numbering. One explicit value anywhere declines the whole enum. What is left is
///         the shape where combination is meaningless by construction.
///     </para>
///     <para>
///         ⚠ <b>This is the use site; <c>CA1027</c> and <c>CA2217</c> are the declaration.</b> Those two
///         were probed against the SDK at <c>AnalysisMode=All</c> rather than assumed:
///         <c>CA1027</c> fires only where at least three distinct non-zero values are all powers of
///         two — it is silent on <c>{ A, B, C }</c> and on <c>{ A, B, C, D }</c>. Consecutive
///         numbering reaches three non-zero values only at <c>1, 2, 3</c>, and <c>3</c> is not a power
///         of two, so <b><c>CA1027</c> can never fire on a declaration this rule accepts</b> and the
///         two are disjoint by arithmetic rather than by a filter.
///     </para>
///     <para>
///         ⚠ Report-only. Adding <c>[Flags]</c> is a public API change and is usually the wrong answer:
///         a consecutively numbered enum is a closed choice, and the defect is normally the
///         combination rather than the declaration.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PlainEnumBitwiseAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.BitwiseOperationOnPlainEnum);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var flags = start.Compilation.GetTypeByMetadataName("System.FlagsAttribute");
                start.RegisterSyntaxNodeAction(
                    context => AnalyzeBinary(context, flags),
                    SyntaxKind.BitwiseOrExpression,
                    SyntaxKind.BitwiseAndExpression,
                    SyntaxKind.ExclusiveOrExpression
                );
                start.RegisterSyntaxNodeAction(
                    context => AnalyzeAssignment(context, flags),
                    SyntaxKind.OrAssignmentExpression,
                    SyntaxKind.AndAssignmentExpression,
                    SyntaxKind.ExclusiveOrAssignmentExpression
                );
                start.RegisterSyntaxNodeAction(
                    context => AnalyzeUnary(context, flags),
                    SyntaxKind.BitwiseNotExpression
                );
            }
        );
    }

    static void AnalyzeBinary(SyntaxNodeAnalysisContext context, INamedTypeSymbol? flags) {
        var binary = (BinaryExpressionSyntax)context.Node;

        // ⚠ `OperatorMethod: null` keeps this to the built-in enum operators. A user-defined `|`
        // on a wrapper struct is somebody's designed API, not a member combination.
        if (context.SemanticModel.GetOperation(context.Node, context.CancellationToken)
            is not IBinaryOperation { OperatorMethod: null }) {
            return;
        }

        Report(context, binary.OperatorToken, binary.Left, flags);
    }

    static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, INamedTypeSymbol? flags) {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        if (context.SemanticModel.GetOperation(context.Node, context.CancellationToken)
            is not ICompoundAssignmentOperation { OperatorMethod: null }) {
            return;
        }

        Report(context, assignment.OperatorToken, assignment.Left, flags);
    }

    static void AnalyzeUnary(SyntaxNodeAnalysisContext context, INamedTypeSymbol? flags) {
        var unary = (PrefixUnaryExpressionSyntax)context.Node;
        if (context.SemanticModel.GetOperation(context.Node, context.CancellationToken)
            is not IUnaryOperation { OperatorMethod: null }) {
            return;
        }

        Report(context, unary.OperatorToken, unary.Operand, flags);
    }

    static void Report(
        SyntaxNodeAnalysisContext context,
        SyntaxToken operatorToken,
        ExpressionSyntax operand,
        INamedTypeSymbol? flags
    ) {
        if (Enum(context.SemanticModel.GetTypeInfo(operand, context.CancellationToken).Type) is not { } type
            || flags is not null
            && type.GetAttributes()
                .Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, flags))
            || !IsConsecutivelyNumbered(type, context.CancellationToken)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                operatorToken.GetLocation(),
                "`"
                + type.Name
                + "` numbers its members consecutively and is not marked `[Flags]`, so `"
                + operatorToken.ValueText
                + "` combines them into a value that need not be any declared member"
            )
        );
    }

    /// <summary>The enum behind a type, seeing through <c>Nullable&lt;T&gt;</c>.</summary>
    static INamedTypeSymbol? Enum(ITypeSymbol? type) {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            && nullable.TypeArguments.Length == 1) {
            type = nullable.TypeArguments[0];
        }

        return type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumeration ? enumeration : null;
    }

    /// <summary>
    ///     Whether every member of the enum was left to the compiler to number.
    /// </summary>
    /// <remarks>
    ///     ⚠ This reads the <em>declaration</em>, not the values. Two enums can hold exactly the same
    ///     constants — <c>{ A, B, C }</c> and <c>{ A = 0, B = 1, C = 2 }</c> — and only the first
    ///     is evidence that nobody was laying out bits.
    ///     <para>
    ///         ⚠ <b>An enum from a referenced assembly is therefore never reported</b>, because it has no
    ///         declaring syntax to read and its constants alone cannot say whether a value was written
    ///         down. That is a stated hole, not an oversight: the alternative is to infer intent from
    ///         <c>{ 0, 1, 2 }</c>, which is both a three-member choice and a two-flag bit set.
    ///     </para>
    /// </remarks>
    static bool IsConsecutivelyNumbered(INamedTypeSymbol type, System.Threading.CancellationToken cancellation) {
        var members = type.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(static field => field.HasConstantValue && !field.IsImplicitlyDeclared)
            .ToArray();
        if (members.Length == 0) {
            return false;
        }

        foreach (var member in members) {
            var declarations = member.DeclaringSyntaxReferences;
            if (declarations.Length != 1
                || declarations[0].GetSyntax(cancellation) is not EnumMemberDeclarationSyntax { EqualsValue: null }) {
                return false;
            }
        }

        return true;
    }
}
