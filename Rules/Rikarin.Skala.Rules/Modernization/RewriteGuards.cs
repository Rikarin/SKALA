using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     The guards the <c>SK1xxx</c> rewrites share, in one place because they are the rules.
/// </summary>
/// <remarks>
///     ⚠ Every rule in this namespace turns one shape of code into another, so each of them has to
///     answer the same three questions before it may fire: does the rewrite change how many times
///     something is <em>evaluated</em>, does it delete text a person <em>wrote</em>, and does the
///     result still <em>compile</em>. A rule that answers any of them wrong is not a noisy rule, it is
///     a wrong one — docs/plan/16 § R3's distinction — so the answers live here rather than being
///     re-derived, slightly differently, eight times.
/// </remarks>
internal static class RewriteGuards {
    /// <summary>
    ///     Whether an expression is a chain of plain names — <c>x</c>, <c>this.x</c>, <c>a.b.c</c>.
    /// </summary>
    /// <remarks>
    ///     A <em>shape</em> test and nothing more: no invocation, no element access and no <c>await</c>
    ///     anywhere in it, which is what a rewrite that repeats the text of an expression needs before
    ///     it can even ask the semantic question.
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             It does not answer "may this be evaluated a different number of times", and it used
    ///             to claim it did.
    ///         </b> The remark that stood here admitted every property as an accepted trade-off —
    ///         excluding getters would silence these rules on <c>this.Items</c>, and "a property whose
    ///         getter is not idempotent between two adjacent reads is already a bug the rule is
    ///         reporting rather than causing". #412's audit ran both versions and refuted it: the rules
    ///         report nothing about the getter, and the fix changes a working program — <c>SK1041</c>
    ///         read a counting getter twice before and once after, <c>SK1044</c> and <c>SK1052</c> turned
    ///         a <c>NullReferenceException</c> into a value (#423). A rewrite that changes how many times
    ///         a path runs asks <see cref="IsFreeToRepeat(IOperation)" /> or
    ///         <see cref="IsFreeToSkip(IOperation)" />; <see cref="IsStorageNamePath" /> is this shape
    ///         test joined to the first of them.
    ///     </para>
    /// </remarks>
    public static bool IsPlainNamePath(ExpressionSyntax expression) {
        while (true) {
            switch (expression) {
                case IdentifierNameSyntax:
                case ThisExpressionSyntax:
                case BaseExpressionSyntax:
                case PredefinedTypeSyntax:
                    return true;

                case MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access:
                    expression = access.Expression;
                    continue;

                default:
                    return false;
            }
        }
    }

    /// <summary>
    ///     Whether <paramref name="expression" /> is a plain name path every link of which is storage:
    ///     a local, parameter, field, type or namespace, <c>this</c>/<c>base</c>, or a non-virtual
    ///     auto-property declared in source — so reading it once and reading it twice are the same, and
    ///     so are writing it once and writing it zero times.
    /// </summary>
    /// <remarks>
    ///     ⚠ For the rewrites that change how many times a path is <em>evaluated</em> or
    ///     <em>written</em>, where <see cref="IsPlainNamePath" />'s admission of every property is wrong
    ///     (#412's audit, each measured by running both versions): <c>SK1030</c>'s <c>x = x ?? y</c> →
    ///     <c>x ??= y</c> stops calling a setter when <c>x</c> is non-null — the
    ///     <c>INotifyPropertyChanged</c> setter that raises on every assignment is the common case, not a
    ///     contrived one — and <c>SK1015</c>, <c>SK1031</c> and <c>SK1033</c> read a getter once where
    ///     the source read it twice. A property from metadata is declined because its body cannot be
    ///     seen; an auto-property's accessors are the compiler's and touch only its backing field.
    ///     <para>
    ///         ⚠ #423 moved the semantic half onto <see cref="IsFreeToRepeat(IOperation)" />, so this
    ///         and the evaluation-count guards of <c>SK1041</c>, <c>SK1044</c>, <c>SK1052</c>,
    ///         <c>SK2051</c>, <c>SK2064</c>, <c>SK2200</c>, <c>SK4030</c> and <c>SK4032</c> are one
    ///         definition rather than nine. Two things changed for the rules that already used it: a
    ///         <c>volatile</c> field is no longer storage, and a positional record property and three
    ///         framework getters now are.
    ///     </para>
    /// </remarks>
    public static bool IsStorageNamePath(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellation
    ) =>
        IsPlainNamePath(expression) && IsFreeToRepeat(expression, model, cancellation);

    /// <summary>
    ///     ⚠ Whether evaluating the expression twice, back to back, is indistinguishable from evaluating
    ///     it once: it runs no code anybody wrote, so the only thing it can do is produce a value or
    ///     throw — and a throw happens on the first evaluation either way.
    /// </summary>
    /// <remarks>
    ///     The question a rewrite asks when it collapses two adjacent evaluations into one —
    ///     <c>x == null || x.Length == 0</c> into <c>string.IsNullOrEmpty(x)</c>, <c>x = x + 1</c> into
    ///     <c>x += 1</c>. Admitted, recursively: literals and constants, locals, parameters,
    ///     <c>this</c>/<c>base</c>, non-<c>volatile</c> fields, the getter of a non-virtual
    ///     auto-property declared in source or of a positional record property, <c>string.Length</c>,
    ///     <c>Array.Length</c> and <c>Nullable&lt;T&gt;.HasValue</c>, and the language's own conversions
    ///     and operators over them, <c>?:</c>, <c>??</c>, <c>?.</c>, <c>is</c> and patterns. Declined:
    ///     every other getter, every user-defined operator and conversion — <c>==</c> included — an
    ///     invocation, an indexer, an allocation, an assignment, <c>await</c>, <c>dynamic</c>, a
    ///     deconstruction or list pattern, and a concatenation that would call some type's
    ///     <c>ToString</c>.
    ///     <para>
    ///         ⚠ A static field's first read may run its type's static constructor, which this does not
    ///         count as an effect: the constructor runs once per process whatever the count, and
    ///         <c>beforefieldinit</c> already leaves its timing to the runtime.
    ///     </para>
    /// </remarks>
    public static bool IsFreeToRepeat(IOperation? operation) => operation is not null && Admits(operation, true);

    /// <inheritdoc cref="IsFreeToRepeat(IOperation)" />
    public static bool IsFreeToRepeat(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellation
    ) =>
        IsFreeToRepeat(model.GetOperation(expression, cancellation));

    /// <summary>
    ///     ⚠ Whether the expression may be evaluated zero times instead of once, or moved past another
    ///     evaluation: <see cref="IsFreeToRepeat(IOperation)" />, and it also cannot throw.
    /// </summary>
    /// <remarks>
    ///     The question a rewrite asks when it deletes an evaluation (<c>x * 0</c> → <c>0</c>, an
    ///     initializer every constructor overwrites), skips it (<c>&amp;</c> → <c>&amp;&amp;</c>, a
    ///     lambda body over an empty list) or reorders it against another. On top of the repeat test it
    ///     declines a member read through a receiver that may be <c>null</c> — so <c>this.f</c>, a
    ///     static member and a member of a value-typed receiver qualify and <c>other.f</c> does not —
    ///     checked or <c>decimal</c> arithmetic, a division or remainder by anything but a constant
    ///     other than <c>0</c> and <c>-1</c>, and every explicit conversion that can fail. #412's audit
    ///     measured each of these as the program's behaviour changing: <c>b!.f * 0</c> → <c>0</c> no
    ///     longer throws (#423).
    /// </remarks>
    public static bool IsFreeToSkip(IOperation? operation) => operation is not null && Admits(operation, false);

    /// <inheritdoc cref="IsFreeToSkip(IOperation)" />
    public static bool IsFreeToSkip(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellation) =>
        IsFreeToSkip(model.GetOperation(expression, cancellation));

    /// <summary>
    ///     ⚠ Whether running <paramref name="operation" /> can execute no code but the language's own and
    ///     a handful of framework methods over text and numbers — so nothing it does can reach an object
    ///     it was not handed as storage.
    /// </summary>
    /// <remarks>
    ///     A different question from <see cref="IsFreeToRepeat(IOperation)" />: a <em>body</em> may write
    ///     locals and fields, branch, return and throw, because none of that runs a method. What it may
    ///     not do is call one — an invocation, an allocation, a getter or setter that is not storage, an
    ///     indexer, a user-defined operator or conversion, a <c>foreach</c>, a deconstruction, an
    ///     interpolated string, a local function or <c>dynamic</c> — unless the call is to a method
    ///     declared on <c>string</c>, <c>char</c>, <c>bool</c>, a numeric primitive or <c>Math</c>, is not
    ///     generic, and takes only parameters of those types or an enum. Such a method runs no user code,
    ///     since nothing it is handed has a <c>ToString</c> or <c>Equals</c> somebody wrote. The question a
    ///     rewrite asks when the code it moves into another method can only be proved to behave the
    ///     same if it cannot reach that method's state — #430's predicate that grows the list it scans.
    /// </remarks>
    public static bool RunsNoOtherCode(IOperation? operation) => RunsNoOtherCode(operation, true);

    /// <summary>
    ///     <see cref="RunsNoOtherCode(IOperation)" />, and with <paramref name="mayThrow" /> false it also
    ///     cannot throw: the receiver of every member read is <c>this</c>, a value or an already-tested
    ///     <c>?.</c> receiver, and no array element, framework call, checked or <c>decimal</c> arithmetic,
    ///     division by a non-constant, or failing conversion — <see cref="IsFreeToSkip(IOperation)" />'s
    ///     terms, over a body.
    /// </summary>
    /// <remarks>
    ///     ⚠ #431: a constructor that throws before it overwrites a field leaves the initialized value
    ///     where a finalizer or a leaked <c>this</c> reads it — measured <c>5</c> before deleting the
    ///     initializer and <c>0</c> after, both ways.
    /// </remarks>
    public static bool RunsNoOtherCode(IOperation? operation, bool mayThrow) {
        if (operation is null) {
            return false;
        }

        var pending = new Stack<IOperation>();
        pending.Push(operation);
        while (pending.Count > 0) {
            var current = pending.Pop();
            if (!IsInertStep(current, mayThrow)) {
                return false;
            }

            foreach (var child in current.ChildOperations) {
                pending.Push(child);
            }
        }

        return true;
    }

    /// <summary>One node of <see cref="RunsNoOtherCode(IOperation, bool)" />, judged without its children.</summary>
    static bool IsInertStep(IOperation operation, bool mayThrow) {
        if (operation.Type?.TypeKind is TypeKind.Dynamic or TypeKind.Pointer or TypeKind.FunctionPointer) {
            return false;
        }

        switch (operation) {
            case IBlockOperation:
            case IReturnOperation:
            case IExpressionStatementOperation:
            case IVariableDeclarationGroupOperation:
            case IVariableDeclarationOperation:
            case IVariableDeclaratorOperation:
            case IVariableInitializerOperation:
            case IConditionalOperation:
            case IBranchOperation:
            case ILabeledOperation:
            case IEmptyOperation:
            case ILiteralOperation:
            case ILocalReferenceOperation:
            case IParameterReferenceOperation:
            case IInstanceReferenceOperation:
            case IConditionalAccessInstanceOperation:
            case IConditionalAccessOperation:
            case IDefaultValueOperation:
            case ITypeOfOperation:
            case ISizeOfOperation:
            case INameOfOperation:
            case ISimpleAssignmentOperation:
            case IDiscardOperation:
            case IIsTypeOperation:
            case IIsPatternOperation:
            case ITypePatternOperation:
            case IDeclarationPatternOperation:
            case IDiscardPatternOperation:
            case IConstantPatternOperation:
            case IRelationalPatternOperation:
            case INegatedPatternOperation:
            case IBinaryPatternOperation:
            case IPropertySubpatternOperation:
            case IArgumentOperation:
                return true;

            case ICoalesceOperation coalesce:
                return IsLanguageOwn(coalesce.ValueConversion.MethodSymbol);

            case IFieldReferenceOperation field:
                return mayThrow || CannotBeNull(field.Instance);

            case IArrayElementReferenceOperation:
                return mayThrow;

            case IRecursivePatternOperation recursive:
                return recursive.DeconstructSymbol is null && recursive.DeconstructionSubpatterns.IsEmpty;

            case IPropertyReferenceOperation property:
                return property.Arguments.IsEmpty
                    && IsStorage(property.Property)
                    && (mayThrow || CannotBeNull(property.Instance));

            case IConversionOperation conversion:
                return IsLanguageOwn(conversion.OperatorMethod) && (mayThrow || !CanThrow(conversion));

            case IBinaryOperation binary:
                return IsLanguageOwn(binary.OperatorMethod)
                    && !CallsToString(binary)
                    && (mayThrow || !CanThrow(binary));

            case IUnaryOperation unary:
                return IsLanguageOwn(unary.OperatorMethod)
                    && (mayThrow || !(unary.IsChecked && unary.OperatorKind == UnaryOperatorKind.Minus));

            case IIncrementOrDecrementOperation increment:
                return increment.OperatorMethod is null && (mayThrow || !increment.IsChecked);

            case ICompoundAssignmentOperation compound:
                return IsLanguageOwn(compound.OperatorMethod)
                    && IsLanguageOwn(compound.InConversion.MethodSymbol)
                    && IsLanguageOwn(compound.OutConversion.MethodSymbol)
                    && !(compound.OperatorKind == BinaryOperatorKind.Add
                        && compound.Type?.SpecialType == SpecialType.System_String
                        && compound.Value.Type?.SpecialType is not (SpecialType.System_String
                            or SpecialType.System_Char))
                    && (mayThrow
                        || (!compound.IsChecked
                            && !IsDecimal(compound.Type)
                            && compound.OperatorKind is not (BinaryOperatorKind.Divide
                                or BinaryOperatorKind.Remainder)));

            case IInvocationOperation invocation:
                return mayThrow && IsInertFrameworkMethod(invocation.TargetMethod);

            default:
                return false;
        }
    }

    /// <summary>
    ///     A receiver a member read through cannot find <c>null</c>: none, <c>this</c>, a tested <c>?.</c> receiver or a
    ///     value.
    /// </summary>
    static bool CannotBeNull(IOperation? instance) =>
        instance is null or IInstanceReferenceOperation or IConditionalAccessInstanceOperation
        || instance.Type?.IsValueType == true;

    /// <summary>
    ///     A non-generic framework method on text, a character, a flag or a number, whose every parameter
    ///     is one of those or an enum — so it is handed nothing with code of its own to run.
    /// </summary>
    static bool IsInertFrameworkMethod(IMethodSymbol method) {
        if (method.IsGenericMethod
            || method.MethodKind != MethodKind.Ordinary
            || !method.Locations.All(static l => l.IsInMetadata)) {
            return false;
        }

        var owner = method.ContainingType;
        if (!IsPlainValue(owner)
            && !(owner is {
                Name: "Math" or "MathF",
                ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
            })) {
            return false;
        }

        foreach (var parameter in method.Parameters) {
            if (parameter.RefKind != RefKind.None || !IsPlainValue(parameter.Type)) {
                return false;
            }
        }

        return true;

        static bool IsPlainValue(ITypeSymbol type) =>
            type.TypeKind == TypeKind.Enum
            || type.SpecialType is >= SpecialType.System_Boolean and <= SpecialType.System_String;
    }

    static bool Admits(IOperation operation, bool mayThrow) {
        if (operation.ConstantValue.HasValue) {
            return true;
        }

        if (operation.Type?.TypeKind is TypeKind.Dynamic or TypeKind.Pointer or TypeKind.FunctionPointer) {
            return false;
        }

        switch (operation) {
            case ILiteralOperation:
            case ILocalReferenceOperation:
            case IParameterReferenceOperation:
            case IInstanceReferenceOperation:
            case IConditionalAccessInstanceOperation:
            case IDefaultValueOperation:
            case ITypeOfOperation:
                return true;

            case IFieldReferenceOperation field:
                return !field.Field.IsVolatile && Receiver(field.Instance, mayThrow);

            case IPropertyReferenceOperation property:
                return property.Arguments.IsEmpty
                    && IsStorage(property.Property)
                    && Receiver(property.Instance, mayThrow);

            case IConversionOperation conversion:
                return IsLanguageOwn(conversion.OperatorMethod)
                    && (mayThrow || !CanThrow(conversion))
                    && Admits(conversion.Operand, mayThrow);

            case IBinaryOperation binary:
                return IsLanguageOwn(binary.OperatorMethod)
                    && (mayThrow || !CanThrow(binary))
                    && !CallsToString(binary)
                    && Admits(binary.LeftOperand, mayThrow)
                    && Admits(binary.RightOperand, mayThrow);

            case IUnaryOperation unary:
                return IsLanguageOwn(unary.OperatorMethod)
                    && (mayThrow || !(unary.IsChecked && unary.OperatorKind == UnaryOperatorKind.Minus))
                    && Admits(unary.Operand, mayThrow);

            case IConditionalOperation conditional:
                return Admits(conditional.Condition, mayThrow)
                    && Admits(conditional.WhenTrue, mayThrow)
                    && (conditional.WhenFalse is null || Admits(conditional.WhenFalse, mayThrow));

            case ICoalesceOperation coalesce:
                return IsLanguageOwn(coalesce.ValueConversion.MethodSymbol)
                    && Admits(coalesce.Value, mayThrow)
                    && Admits(coalesce.WhenNull, mayThrow);

            case IConditionalAccessOperation access:
                return Admits(access.Operation, mayThrow) && Admits(access.WhenNotNull, mayThrow);

            case IIsTypeOperation isType:
                return Admits(isType.ValueOperand, mayThrow);

            case IIsPatternOperation isPattern:
                return Admits(isPattern.Value, mayThrow) && AdmitsPattern(isPattern.Pattern, mayThrow);

            default:
                return false;
        }
    }

    /// <summary>
    ///     A pattern tests its input without throwing — a property subpattern is skipped on <c>null</c>
    ///     — so only what it reads matters: the members of a property pattern, and no
    ///     <c>Deconstruct</c>, <c>ITuple</c>, indexer or <c>Length</c>/<c>Count</c> of a list pattern.
    /// </summary>
    static bool AdmitsPattern(IPatternOperation pattern, bool mayThrow) {
        switch (pattern) {
            case IConstantPatternOperation constant:
                return Admits(constant.Value, mayThrow);

            case IRelationalPatternOperation relational:
                return Admits(relational.Value, mayThrow);

            case ITypePatternOperation:
            case IDeclarationPatternOperation:
            case IDiscardPatternOperation:
                return true;

            case INegatedPatternOperation negated:
                return AdmitsPattern(negated.Pattern, mayThrow);

            case IBinaryPatternOperation binary:
                return AdmitsPattern(binary.LeftPattern, mayThrow) && AdmitsPattern(binary.RightPattern, mayThrow);

            case IRecursivePatternOperation recursive:
                if (recursive.DeconstructSymbol is not null || !recursive.DeconstructionSubpatterns.IsEmpty) {
                    return false;
                }

                foreach (var subpattern in recursive.PropertySubpatterns) {
                    if (!Admits(subpattern.Member, mayThrow) || !AdmitsPattern(subpattern.Pattern, mayThrow)) {
                        return false;
                    }
                }

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    ///     A static member has no receiver; an instance member's receiver is evaluated first, and may be
    ///     <c>null</c> unless it is <c>this</c>, the already-tested receiver of a <c>?.</c>, or a value.
    /// </summary>
    static bool Receiver(IOperation? instance, bool mayThrow) =>
        instance is null
        || (Admits(instance, mayThrow)
            && (mayThrow
                || instance is IInstanceReferenceOperation or IConditionalAccessInstanceOperation
                || instance.Type?.IsValueType == true));

    /// <summary>
    ///     A getter that runs no code a person wrote: an auto-property's, a positional record
    ///     property's, and three framework getters that only read a length or a flag.
    /// </summary>
    static bool IsStorage(IPropertySymbol property) {
        if (property.IsIndexer || property.GetMethod is null) {
            return false;
        }

        switch (property.ContainingType.SpecialType) {
            case SpecialType.System_String:
                return property.Name == "Length";
            case SpecialType.System_Array:
                return property.Name is "Length" or "LongLength";
        }

        if (property.ContainingType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) {
            return property.Name == "HasValue";
        }

        return IsSourceAutoProperty(property, CancellationToken.None);
    }

    static bool IsSourceAutoProperty(IPropertySymbol property, CancellationToken cancellation) =>
        property is { IsVirtual: false, IsAbstract: false, IsOverride: false, IsExtern: false, IsIndexer: false }
        && property.ContainingType.TypeKind != TypeKind.Interface
        && property.DeclaringSyntaxReferences.Length == 1
        && property.DeclaringSyntaxReferences[0].GetSyntax(cancellation) switch {
            PropertyDeclarationSyntax { ExpressionBody: null, AccessorList.Accessors: { Count: > 0 } accessors } =>
                accessors.All(static accessor => accessor is { Body: null, ExpressionBody: null }),
            // ⚠ A positional record property: `IsImplicitlyDeclared` is false for it, and the parameter
            // is where it is written down. The compiler writes its accessors.
            ParameterSyntax => property.ContainingType.IsRecord,
            _ => false
        };

    /// <summary>
    ///     ⚠ Built in, for this purpose: no method at all, or one of the two framework types whose
    ///     operators the compiler routes through a method — <c>string</c>'s equality and
    ///     <c>decimal</c>'s arithmetic — and which run no code anybody else wrote.
    /// </summary>
    static bool IsLanguageOwn(IMethodSymbol? method) =>
        method is null || method.ContainingType.SpecialType is SpecialType.System_String or SpecialType.System_Decimal;

    static bool CanThrow(IBinaryOperation binary) {
        var isDecimal = IsDecimal(binary.Type)
            || IsDecimal(binary.LeftOperand.Type)
            || IsDecimal(binary.RightOperand.Type);
        switch (binary.OperatorKind) {
            case BinaryOperatorKind.Add:
            case BinaryOperatorKind.Subtract:
            case BinaryOperatorKind.Multiply:
                return binary.IsChecked || isDecimal;

            case BinaryOperatorKind.Divide:
            case BinaryOperatorKind.Remainder:
                if (binary.Type?.SpecialType is SpecialType.System_Single or SpecialType.System_Double) {
                    return false;
                }

                // ⚠ `int.MinValue / -1` throws `OverflowException` unchecked as well, and so does `%`.
                return isDecimal
                    || binary.RightOperand.ConstantValue is not { HasValue: true, Value: { } divisor }
                    || System.Convert.ToDecimal(divisor, System.Globalization.CultureInfo.InvariantCulture) is 0m
                        or -1m;

            default:
                return false;
        }
    }

    static bool CanThrow(IConversionOperation conversion) {
        var common = conversion.Conversion;
        if (common.IsIdentity) {
            return false;
        }

        // ⚠ An explicit conversion to or from `decimal` is always checked; an implicit one cannot fail.
        if (IsDecimal(conversion.Type) || IsDecimal(conversion.Operand.Type)) {
            return !common.IsImplicit;
        }

        if (common.IsImplicit) {
            return false;
        }

        // Explicit numeric narrows silently unless checked; an explicit reference conversion, an unboxing
        // and `T?` → `T` can all throw.
        return !common.IsNumeric || conversion.IsChecked;
    }

    /// <summary>
    ///     ⚠ A concatenation calls <c>ToString</c> on each operand that is not already text — an
    ///     override somebody wrote, for any type but the primitives.
    /// </summary>
    static bool CallsToString(IBinaryOperation binary) {
        if (binary.OperatorKind != BinaryOperatorKind.Add || binary.Type?.SpecialType != SpecialType.System_String) {
            return false;
        }

        return !IsText(binary.LeftOperand) || !IsText(binary.RightOperand);

        static bool IsText(IOperation operand) {
            while (operand is IConversionOperation { IsImplicit: true } conversion) {
                operand = conversion.Operand;
            }

            return operand.Type is null
                || operand.ConstantValue.HasValue
                || operand.Type.SpecialType is >= SpecialType.System_Boolean and <= SpecialType.System_String;
        }
    }

    static bool IsDecimal(ITypeSymbol? type) =>
        type is not null
        && (type.SpecialType == SpecialType.System_Decimal
            || (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
                && nullable.TypeArguments[0].SpecialType == SpecialType.System_Decimal));

    /// <summary>Whether two expressions are the same text, ignoring trivia.</summary>
    public static bool Same(ExpressionSyntax left, ExpressionSyntax right) =>
        SyntaxFactory.AreEquivalent(left, right, false);

    /// <summary>
    ///     ⚠ Whether the text a fix is about to delete or rewrite contains something a person wrote.
    /// </summary>
    /// <remarks>
    ///     A comment inside the text a rewrite removes is content, and a fix that silently deletes it
    ///     is a fix nobody can review. A preprocessor directive is worse: removing one half of an
    ///     <c>#if</c> does not merely lose text, it stops the file parsing under the other symbol set.
    ///     <para>
    ///         ⚠ <b>This asks over exactly the span it is given and nothing else</b>, which is the
    ///         question all but a handful of call sites actually have. Its counterpart
    ///         <see cref="ContainsCommentOrDirectiveAroundTheDeclaration" /> reaches <c>FullSpan</c>
    ///         and therefore the comment written ABOVE the node; the two are different questions, and
    ///         picking the wrong one is silent in both directions — see that method's remarks for
    ///         which call site needs which, and <c>RewriteGuardTests</c> for the pinned list.
    ///     </para>
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             It walks trivia rather than scanning text, and that is a fix rather than a
    ///             refactor (#325).
    ///         </b> This was <c>IndexOf("//") || IndexOf("/*") || IndexOf('#')</c> over
    ///         the span's characters, which cannot tell a comment from a <em>string literal</em>
    ///         containing one. <c>SK4034</c> went silent on
    ///         <c>entries.OrderBy(…).Where(e =&gt; e != "https://example.com")</c> — the `//` of a URL
    ///         read as a comment — and every one of the ten call sites #302 moved here inherited the
    ///         same blind spot. ⚠ <b>That is #302's own failure shape wearing the other mask</b>: a
    ///         rule dead on ordinary code, with every negative still passing, because a rule that
    ///         never fires declines everything it is supposed to decline. A `#` in a literal, a `/*`
    ///         in a regex and a Windows path all did it too.
    ///     </para>
    /// </remarks>
    public static bool ContainsCommentOrDirectiveWithinTheEdit(SyntaxTree tree, TextSpan edit) {
        foreach (var trivia in tree.GetRoot().DescendantTrivia(edit, descendIntoTrivia: true)) {
            if (IsCommentOrDirective(trivia) && trivia.FullSpan.OverlapsWith(edit)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ The same question widened to the trivia ABOVE the node, for a fix that carries it away.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>This is not the defect #302 describes, and it is not the default either.</b> It asks
    ///     over <c>FullSpan</c>, so it sees the doc comment written above a declaration — text that
    ///     most fixes never touch. A fix that rewrites a span <em>inside</em> the node must not ask
    ///     this question: it then declines on documented code, silently, with every negative still
    ///     passing, which is exactly how #302 survived. Use
    ///     <see cref="ContainsCommentOrDirectiveWithinTheEdit" /> there.
    ///     <para>
    ///         ⚠ <b>Only two shapes of fix may ask it</b>, and #325 audited all 33 call sites to find
    ///         them. The first is a fix that deletes the node's whole <em>line</em> — anything
    ///         reaching for <see cref="LineSpanOf" />, or deleting <c>FullSpan</c> outright — where
    ///         the leading comment really is inside the edit. The second is a fix that deletes the
    ///         node entirely by its <c>Span</c>: the comment above is not deleted but is
    ///         <em>orphaned</em>, left introducing whatever member follows, which is worse than
    ///         losing it. <c>SK1003</c> is the second shape and
    ///         <c>fixtures/SK1003/negative/comments.cs</c> pins it.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>The name is the whole point.</b> Both questions used to be spelled
    ///         <c>ContainsCommentOrDirective</c> and told apart only by arity, so copying a line from
    ///         a line-deleting rule into a span-rewriting one compiled and was wrong — which is how
    ///         the idiom spread to four hand-written copies, one of them carrying this doc comment's
    ///         ancestor verbatim (#302, #325). A call site now has to say which question it means.
    ///     </para>
    /// </remarks>
    public static bool ContainsCommentOrDirectiveAroundTheDeclaration(SyntaxNode node) {
        foreach (var trivia in node.DescendantTrivia(descendIntoTrivia: true)) {
            if (IsCommentOrDirective(trivia)) {
                return true;
            }
        }

        return false;
    }

    static bool IsCommentOrDirective(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
        || trivia.IsDirective;

    /// <summary>
    ///     The full span of a statement including the trivia that would be orphaned by deleting it.
    /// </summary>
    /// <remarks>
    ///     ⚠ Deleting <c>statement.Span</c> alone leaves the indentation and the newline behind, and
    ///     while <c>skala fix</c> re-formats every file it touches (docs/plan/08 § FixEdits), a blank
    ///     line is not something the formatter is allowed to remove — <c>keep_blank_lines_in_code</c>
    ///     preserves what the author wrote. So the fix removes the line rather than the statement.
    /// </remarks>
    public static TextSpan LineSpanOf(StatementSyntax statement) =>
        TextSpan.FromBounds(statement.FullSpan.Start, statement.FullSpan.End);

    /// <summary>
    ///     Whether introducing a local called <paramref name="name" /> at <paramref name="position" />
    ///     would collide with something already in scope.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the guard that stops a rewrite producing <c>CS0128</c>/<c>CS0136</c>. Two of the
    ///     rules here move a declaration outwards — <c>SK1006</c> lifts a <c>using</c> block's
    ///     statements into the enclosing block, <c>SK1015</c> and <c>SK1033</c> move a declaration into
    ///     an enclosing condition — and C# forbids a local that shadows a local of an enclosing local
    ///     scope in the same member. <c>LookupSymbols</c> at the destination is exactly the set that
    ///     would conflict, which is why the question is asked there rather than at the source.
    /// </remarks>
    public static bool WouldCollide(
        SemanticModel model,
        int position,
        string name,
        CancellationToken cancellation
    ) {
        cancellation.ThrowIfCancellationRequested();
        foreach (var symbol in model.LookupSymbols(position, name: name)) {
            if (symbol is ILocalSymbol or IParameterSymbol or IRangeVariableSymbol) {
                return true;
            }

            if (symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction }) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether a name being lifted out of <paramref name="moved" /> is declared by any other local
    ///     scope of the same member.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the second half of the scoping guard and the half that <c>LookupSymbols</c> cannot
    ///     answer. Three rules here move a declaration one scope outwards — <c>SK1006</c> lifts a
    ///     <c>using</c> block's statements, <c>SK1015</c> and <c>SK1033</c> move a declaration into a
    ///     condition, where C# scopes the pattern variable to the <em>enclosing block</em>. A name in a
    ///     scope that merely <em>neighbours</em> the destination is not in scope at the destination and
    ///     so is invisible to a lookup, yet it is exactly what <c>CS0136</c> is about:
    ///     <code>
    /// if (c) { var t = 1; }            // legal today: two cousins
    /// if (x is T) { var t = (T)x; }    // CS0136 once `t` is lifted into the enclosing block
    ///     </code>
    ///     <para>
    ///         ⚠ It over-bails, deliberately. Scanning the whole member counts names that could never
    ///         conflict — two sibling lambdas, a name inside a nested type's initializer — and each one
    ///         costs a finding. The alternative is a fix that does not compile, and docs/plan/10 is explicit
    ///         that a fixing tool which can break the build is one an agent will use to break the build.
    ///     </para>
    /// </remarks>
    public static bool DeclaredElsewhereInMember(SyntaxNode moved, string name) {
        var root = ScopeRoot(moved);
        foreach (var node in root.DescendantNodes()) {
            if (node.Span.OverlapsWith(moved.Span)) {
                continue;
            }

            foreach (var declared in DeclaredNames(node)) {
                if (string.Equals(declared, name, System.StringComparison.Ordinal)) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether anything <em>inside</em> <paramref name="scope" /> declares <paramref name="name" />.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         The inward question, and the one neither guard above can answer. Both of those were
    ///         built for a declaration moving <em>outwards</em>.
    ///     </b>
    ///     <see cref="WouldCollide" /> asks
    ///     <c>LookupSymbols</c> at the destination, which by definition cannot see a name scoped to a
    ///     block nested below that position; <see cref="DeclaredElsewhereInMember" /> skips every node
    ///     overlapping the span being moved. A rule that introduces a binding into a scope it does not
    ///     own — a <c>foreach</c> variable around an existing body, a declaration pushed down into the
    ///     narrower block that uses it — collides in the opposite direction, and both guards pass it.
    ///     <para>
    ///         ⚠ <b>The failure is not a false positive, it is a fix that does not compile.</b>
    ///         <c>SK1083</c> on Serilog's <c>MessageTemplate.GetElementsOfTypeToArray</c> would have
    ///         written <c>foreach (var token in tokens)</c> around a body already declaring
    ///         <c>if (tokens[i] is TResult token)</c> — <c>CS0136</c>, and token-equivalent, so
    ///         <c>SK9099</c>'s promise says nothing about it. Found by a corpus sweep and not by any
    ///         fixture, because no hand-written fixture happens to declare a name inside the loop it
    ///         rewrites (#304).
    ///     </para>
    ///     <para>
    ///         ⚠ <b>Named separately rather than folded into the two above</b>, so the direction is
    ///         visible at the call site instead of being implicit in which helper somebody reached for.
    ///         A rule introducing a name needs this one; a rule lifting a declaration out needs the
    ///         other two; a rule doing both — <c>SK1083</c> is one — needs all three.
    ///     </para>
    ///     <para>
    ///         <paramref name="ignore" /> is the span the fix itself deletes, whose declarations are
    ///         therefore not going to be there to collide with. <c>SK1083</c> passes the <c>for</c>
    ///         header's own declarator.
    ///     </para>
    /// </remarks>
    public static bool DeclaredWithin(SyntaxNode scope, string name, TextSpan? ignore = null) {
        foreach (var node in scope.DescendantNodes()) {
            if (ignore is { } skipped && node.Span.OverlapsWith(skipped)) {
                continue;
            }

            foreach (var declared in DeclaredNames(node)) {
                if (string.Equals(declared, name, System.StringComparison.Ordinal)) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Every name a syntax node declares into some local scope.</summary>
    public static IEnumerable<string> DeclaredNames(SyntaxNode node) {
        switch (node) {
            case VariableDeclaratorSyntax declarator:
                yield return declarator.Identifier.ValueText;
                break;

            case SingleVariableDesignationSyntax designation:
                yield return designation.Identifier.ValueText;
                break;

            case ForEachStatementSyntax forEach:
                yield return forEach.Identifier.ValueText;
                break;

            case LocalFunctionStatementSyntax function:
                yield return function.Identifier.ValueText;
                break;

            case CatchDeclarationSyntax { Identifier.ValueText.Length: > 0 } declaration:
                yield return declaration.Identifier.ValueText;
                break;

            case ParameterSyntax { Identifier.ValueText.Length: > 0 } parameter:
                yield return parameter.Identifier.ValueText;
                break;
        }
    }

    /// <summary>The member a statement lives in — as far outwards as a local name can conflict.</summary>
    /// <remarks>
    ///     ⚠ It deliberately does <b>not</b> stop at a lambda or a local function. A lambda body is a
    ///     nested local scope of the method containing it, so a local of that method conflicts with one
    ///     lifted into the lambda's block, and a root that stopped at the lambda would never see it.
    /// </remarks>
    public static SyntaxNode ScopeRoot(SyntaxNode node) {
        var current = node;
        while (current.Parent is not null) {
            current = current.Parent;
            if (current is BaseMethodDeclarationSyntax
                or AccessorDeclarationSyntax
                or PropertyDeclarationSyntax
                or IndexerDeclarationSyntax
                or BaseFieldDeclarationSyntax
                or CompilationUnitSyntax) {
                return current;
            }
        }

        return current;
    }

    /// <summary>
    ///     Whether the local is named anywhere in its scope other than its own declaration and one
    ///     node the caller is about to fold it into.
    /// </summary>
    /// <remarks>
    ///     ⚠ Shared rather than duplicated: <c>SK1024</c> asked it as "mentioned outside" and
    ///     <c>SK1042</c> as "referenced only within", the same walk written twice with opposite
    ///     polarity. Both rules move a declaration into another construct, and both are only safe when
    ///     the answer is no — so the two copies had to agree, and nothing made them.
    ///     <para>
    ///         The name is compared before the symbol on purpose. Resolving every identifier in the
    ///         scope is what this would otherwise cost, and a local's name is a cheap, exact filter.
    ///     </para>
    /// </remarks>
    public static bool ReferencedOutside(
        SemanticModel model,
        ILocalSymbol local,
        SyntaxNode allowed,
        SyntaxNode declaration,
        CancellationToken cancellation
    ) {
        foreach (var node in ScopeRoot(declaration).DescendantNodes()) {
            cancellation.ThrowIfCancellationRequested();
            if (node is not IdentifierNameSyntax identifier
                || !string.Equals(identifier.Identifier.ValueText, local.Name, System.StringComparison.Ordinal)
                || allowed.Span.Contains(identifier.Span)
                || declaration.Span.Contains(identifier.Span)) {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellation).Symbol, local)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>A message fragment that will not run off the end of a terminal.</summary>
    public static string Trim(string value) => value.Length <= 48 ? value : value.Substring(0, 48) + "…";
}
