using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

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
            || AsyncContext.IsTestMethod(assignment.Syntax)) {
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
            || AsyncContext.IsTestMethod(invocation.Syntax)) {
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
    ///     ⚠ Every case is decided from the expression itself. Nothing resolves a local, because
    ///     the allocate-then-fill shape (<c>new byte[16]</c> handed to
    ///     <c>RandomNumberGenerator.Fill</c>) is correct code that a resolving rule would report.
    ///     <para>
    ///         ⚠ The constant test itself lives in <see cref="ConstantBytes" /> and is shared with
    ///         <c>SK5041</c>, which asks the same question of a key-derivation salt. An expression that
    ///         is a constant IV is a constant salt, and the two rules must not be able to disagree about
    ///         which expressions those are. What stays here is the part that is about ciphers: the
    ///         wording, and <c>aes.IV = aes.Key</c>, which is not a constant at all.
    ///     </para>
    /// </remarks>
    static string? Predictable(IOperation value, Known known) {
        // `aes.IV = aes.Key` — not a constant, so ConstantBytes cannot see it, and as broken as one.
        if (ConstantBytes.Unwrap(value) is IPropertyReferenceOperation { Property.Name: "Key" } property
            && Inherits(property.Property.ContainingType, known.Symmetric)) {
            return "the key is used as the initialisation vector";
        }

        return ConstantBytes.Classify(value, known.Encoding, known.Convert, out var fieldName) switch {
            ConstantByteKind.ZeroArray => "the initialisation vector is an array of zeros",
            ConstantByteKind.LiteralList => "the initialisation vector is a list of constants",
            ConstantByteKind.LiteralStringBytes => "the initialisation vector is the bytes of a literal string",
            ConstantByteKind.DecodedLiteral => "the initialisation vector is decoded from a literal",
            ConstantByteKind.ConstantField => "the initialisation vector is the constant field `" + fieldName + "`",
            _ => null
        };
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
