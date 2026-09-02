using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Security;

/// <summary>
///     <c>SK5020</c> — a block cipher configured with an initialisation vector the attacker can predict.
/// </summary>
/// <remarks>
///     docs/plan/08 § "SK5000 — Security". <c>SK5005</c> reports ECB for leaking the plaintext's
///     structure; a fixed IV in CBC leaks the same structure by a different mechanism, and
///     <c>SK5005</c> does not reach it.
///     <para>
///         ⚠
///         <b>
///             The shape is hosted by <c>CA5401</c> and the host is not usable, which is why this rule
///             exists rather than an entry in the hosted map.
///         </b> Measured on a plain <c>net10.0</c> project
///         outside this repository, <c>CA5401</c> is <b>off by default</b> and, once enabled, reports
///         <c>aes.IV = RandomNumberGenerator.GetBytes(16)</c> and
///         <c>aes.CreateEncryptor(key, RandomNumberGenerator.GetBytes(16))</c> — both of which are the
///         correct code. Its question is "is the IV non-default", not "is the IV predictable", and the
///         answer to the first is yes for every program that transmits an IV alongside its ciphertext.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The reason a Skala rule is allowed here and was refused for <c>#140</c> is that the
///             narrowing needs no judgement.
///         </b> <c>CA5394</c> is untargeted in the same way, and doc 08
///         declined to narrow it because separating a security-sensitive <c>Random</c> from a
///         statistical one means reading identifier names — the judgement that cut <c>SK5008</c>.
///         "Is this expression a compile-time constant" is not a judgement, it is a fact the compiler
///         already computed, so the narrower rule is decidable where that one was not.
///     </para>
///     <para>
///         ⚠ <b>A local is never followed, and the reason is a false positive rather than cost.</b>
///         <c>var iv = new byte[16]; RandomNumberGenerator.Fill(iv); aes.IV = iv;</c> is how correct
///         code is written, and a rule that resolved <c>iv</c> to its declaration would report it. So
///         the constant must be written at the assignment itself, or be the initialiser of a field
///         holding an explicit list of literals — <c>static readonly byte[] Iv = { 1, 2, … }</c> is a
///         hard-coded IV and cannot be anything else, while <c>= new byte[16]</c> is the allocate-then-fill
///         shape and is deliberately not followed.
///     </para>
///     <para>
///         ⚠ <b>Only the encrypting side.</b> <c>CreateDecryptor(key, iv)</c> is handed the IV the
///         message arrived with; that is what the overload is for, and reporting it would report the
///         reader of a broken format rather than its writer.
///     </para>
///     <para>
///         ⚠ <c>hasFix: false</c>. The edit is <c>GenerateIV()</c> plus writing the IV into the message
///         so the far end can read it — a change to the ciphertext format, which makes everything already
///         encrypted unreadable by the new code. That is a migration, exactly as it is for
///         <c>SK5005</c>.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PredictableInitializationVectorAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor =
        SkalaRule.Descriptor(RuleIds.PredictableCipherInitializationVector);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var symmetric = start.Compilation.GetTypeByMetadataName(
                    "System.Security.Cryptography.SymmetricAlgorithm"
                );

                // No symmetric cryptography in the compilation means nothing to say.
                if (symmetric is null) {
                    return;
                }

                var encoding = start.Compilation.GetTypeByMetadataName("System.Text.Encoding");
                var convert = start.Compilation.GetTypeByMetadataName("System.Convert");
                var known = new Known(symmetric, encoding, convert);

                start.RegisterOperationAction(
                    context => Assignment(context, known),
                    OperationKind.SimpleAssignment
                );
                start.RegisterOperationAction(context => Encryptor(context, known), OperationKind.Invocation);
            }
        );
    }

    /// <summary><c>algorithm.IV = …</c>, including inside an object initialiser.</summary>
    static void Assignment(OperationAnalysisContext context, Known known) {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        if (assignment.Target is not IPropertyReferenceOperation { Property.Name: "IV" } target
            || !Inherits(target.Property.ContainingType, known.Symmetric)
            || AsyncContext.IsTestCode(assignment.Syntax, assignment.SemanticModel, context.CancellationToken)) {
            return;
        }

        if (Predictable(assignment.Value, known) is { } why) {
            Report(context, assignment.Value, why);
        }
    }

    /// <summary><c>algorithm.CreateEncryptor(key, iv)</c> — the two-argument overload only.</summary>
    /// <remarks>
    ///     ⚠ <c>CreateDecryptor</c> is deliberately absent. Decryption must be given the IV the
    ///     ciphertext was produced with, so a constant there is a consequence of somebody else's
    ///     choice rather than this call's defect.
    /// </remarks>
    static void Encryptor(OperationAnalysisContext context, Known known) {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.Name != "CreateEncryptor"
            || invocation.Arguments.Length != 2
            || !Inherits(invocation.TargetMethod.ContainingType, known.Symmetric)
            || AsyncContext.IsTestCode(invocation.Syntax, invocation.SemanticModel, context.CancellationToken)) {
            return;
        }

        var key = invocation.Arguments[0].Value;
        var vector = invocation.Arguments[1].Value;

        // ⚠ `CreateEncryptor(k, k)` — the key doubling as the IV. Neither argument is constant, so
        // the constant test below cannot see it, and it is as broken as a literal.
        if (Referenced(Unwrap(key)) is { } left
            && Referenced(Unwrap(vector)) is { } right
            && SymbolEqualityComparer.Default.Equals(left, right)) {
            Report(context, vector, "the key is passed as the initialisation vector as well");
            return;
        }

        if (Predictable(vector, known) is { } why) {
            Report(context, vector, why);
        }
    }

    static void Report(OperationAnalysisContext context, IOperation value, string why) =>
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                value.Syntax.GetLocation(),
                why
                + ", so every message encrypted under this key begins with the same ciphertext for the "
                + "same plaintext — which is the structure `SK5005` reports ECB for leaking, and it also "
                + "makes a chosen-plaintext attack against CBC practical; call `GenerateIV()` per "
                + "message and send the IV with the ciphertext, or use `AesGcm`, which takes a nonce "
                + "and authenticates the result"
            )
        );

    /// <summary>Whether an expression's value is fixed at compile time, and how it is written.</summary>
    /// <remarks>
    ///     ⚠ Every case here is decided from the expression itself. Nothing resolves a local, because
    ///     the allocate-then-fill shape (<c>new byte[16]</c> handed to
    ///     <c>RandomNumberGenerator.Fill</c>) is correct code that a resolving rule would report.
    /// </remarks>
    static string? Predictable(IOperation value, Known known) {
        var operation = Unwrap(value);

        // ⚠ On syntax, because `[1, 2, …]` lowers to an operation kind this analyzer would have to
        // name to match, and naming it pins the Roslyn version the rule compiles against.
        if (operation.Syntax is CollectionExpressionSyntax collection && AllLiterals(collection)) {
            return "the initialisation vector is a list of constants";
        }

        switch (operation) {
            // `new byte[16]` — an array of zeros, written at the assignment, so nothing filled it.
            case IArrayCreationOperation { Initializer: null }:
                return "the initialisation vector is an array of zeros";

            // `new byte[] { 1, 2, … }` and `[1, 2, …]`.
            case IArrayCreationOperation { Initializer: { } initializer }
                when initializer.ElementValues.All(static element => Unwrap(element).ConstantValue.HasValue):
                return "the initialisation vector is a list of constants";

            case IInvocationOperation invocation:
                return FromCall(invocation, known);

            // `static readonly byte[] Iv = { 1, 2, … }`.
            case IFieldReferenceOperation { Field: { IsStatic: true } field } when ConstantArrayField(field):
                return "the initialisation vector is the constant field `" + field.Name + "`";

            // `aes.IV = aes.Key`.
            case IPropertyReferenceOperation { Property.Name: "Key" } property
                when Inherits(property.Property.ContainingType, known.Symmetric):
                return "the key is used as the initialisation vector";

            default:
                return null;
        }
    }

    static string? FromCall(IInvocationOperation invocation, Known known) {
        if (invocation.Arguments.Length != 1 || !Unwrap(invocation.Arguments[0].Value).ConstantValue.HasValue) {
            return null;
        }

        var containing = invocation.TargetMethod.ContainingType;
        if (invocation.TargetMethod.Name == "GetBytes" && Inherits(containing, known.Encoding)) {
            return "the initialisation vector is the bytes of a literal string";
        }

        if (SymbolEqualityComparer.Default.Equals(containing, known.Convert)
            && (invocation.TargetMethod.Name == "FromBase64String"
                || invocation.TargetMethod.Name == "FromHexString")) {
            return "the initialisation vector is decoded from a literal";
        }

        return null;
    }

    /// <summary>
    ///     Whether a field is declared with an explicit list of literal elements.
    /// </summary>
    /// <remarks>
    ///     ⚠ Decided on syntax, and the list must be non-empty. A field initialised
    ///     <c>= new byte[16]</c> is the allocate-then-fill shape and a static constructor may well
    ///     fill it; <c>= { 1, 2, 3 }</c> cannot be anything but a hard-coded value.
    /// </remarks>
    static bool ConstantArrayField(IFieldSymbol field) {
        foreach (var reference in field.DeclaringSyntaxReferences) {
            if (reference.GetSyntax() is not VariableDeclaratorSyntax { Initializer.Value: { } initializer }) {
                continue;
            }

            var elements = initializer switch {
                ArrayCreationExpressionSyntax { Initializer: { } list } => list.Expressions.Count,
                ImplicitArrayCreationExpressionSyntax implicitly => implicitly.Initializer.Expressions.Count,
                InitializerExpressionSyntax braces => braces.Expressions.Count,
                CollectionExpressionSyntax collection => collection.Elements.Count,
                _ => 0
            };

            if (elements > 0 && AllLiterals(initializer)) {
                return true;
            }
        }

        return false;
    }

    static bool AllLiterals(ExpressionSyntax initializer) {
        var expressions = initializer switch {
            ArrayCreationExpressionSyntax { Initializer: { } list } => list.Expressions.ToArray(),
            ImplicitArrayCreationExpressionSyntax implicitly => implicitly.Initializer.Expressions.ToArray(),
            InitializerExpressionSyntax braces => braces.Expressions.ToArray(),
            CollectionExpressionSyntax collection => collection.Elements
                .OfType<ExpressionElementSyntax>()
                .Select(static element => element.Expression)
                .ToArray(),
            _ => System.Array.Empty<ExpressionSyntax>()
        };

        return expressions.Length > 0
            && expressions.All(static expression =>
                expression is LiteralExpressionSyntax
                    or PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax }
            );
    }

    /// <summary>The symbol an expression names, for the "the key is also the IV" comparison.</summary>
    static ISymbol? Referenced(IOperation operation) =>
        operation switch {
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation parameter => parameter.Parameter,
            IFieldReferenceOperation field => field.Field,
            IPropertyReferenceOperation property => property.Property,
            _ => null
        };

    static IOperation Unwrap(IOperation operation) {
        var current = operation;
        while (current is IConversionOperation conversion) {
            current = conversion.Operand;
        }

        return current;
    }

    static bool Inherits(ITypeSymbol? type, INamedTypeSymbol? ancestor) {
        if (ancestor is null) {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, ancestor)) {
                return true;
            }
        }

        return false;
    }

    readonly struct Known {
        public Known(INamedTypeSymbol symmetric, INamedTypeSymbol? encoding, INamedTypeSymbol? convert) {
            Symmetric = symmetric;
            Encoding = encoding;
            Convert = convert;
        }

        public INamedTypeSymbol Symmetric { get; }

        public INamedTypeSymbol? Encoding { get; }

        public INamedTypeSymbol? Convert { get; }
    }
}
