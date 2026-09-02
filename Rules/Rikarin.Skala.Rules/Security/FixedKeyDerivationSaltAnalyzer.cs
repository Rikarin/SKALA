using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Security;

/// <summary>
///     <c>SK5041</c> — PBKDF2 given a salt that is fixed at compile time.
/// </summary>
/// <remarks>
///     Issue #139 arrived as two halves and only this one survived. The <b>fast-hash</b> half —
///     "a password is stored under a general-purpose digest" — is refuted: deciding that a hash is being
///     applied to a <em>password</em> means reading identifier names, which is the judgement that cut
///     <c>SK5008</c> and half of <c>SK5005</c>, and <c>HashAlgorithm</c> is the base class for checksums
///     (CRC32, xxHash and Murmur all derive from it for the streaming plumbing), so the type test cannot
///     stand in for it. This half needs neither, because <b>the receiver is a key-derivation function</b>:
///     PBKDF2's own first parameter is named <c>password</c>, so the API asserts what the input is and the
///     rule does not have to guess.
///     <para>
///         ⚠ <b>Measured unhosted, and the previously published measurement was wrong in one particular.</b>
///         On a plain <c>net10.0</c> project outside this repository with empty
///         <c>Directory.Build.props</c>/<c>.targets</c>, built under <c>AnalysisMode=All</c> with every
///         <c>CA5xxx</c> raised to <c>warning</c>: <c>CA5387</c> <em>does</em> fire on a 100-iteration
///         <c>Rfc2898DeriveBytes</c> — issue #139 recorded it producing nothing there — but it fires on
///         the <b>iteration count</b> and nothing else. It fired on a call whose salt was a perfectly good
///         <c>byte[]</c> parameter, and it was <b>silent</b> on a 100 000-iteration derivation with a
///         hard-coded eight-byte salt and on a 100 000-iteration <c>Pbkdf2</c> with an all-zero salt.
///         <c>CA5379</c> looks only at the hash algorithm. ⚠ So nothing in the SDK reports a constant salt
///         at any iteration count, and the zero is <b>shape present and declined</b> — a planted
///         <c>DataSet.ReadXml</c> in the same file fired <c>CA2351</c> and <c>CA5366</c>.
///     </para>
///     <para>
///         ⚠ <b><c>HKDF</c> is deliberately not a receiver, and this is the false positive the rule exists
///         to avoid.</b> RFC 5869 says HKDF's salt is optional and <em>may be fixed and public</em>: HKDF
///         extracts from high-entropy input keying material, not from a password, and a protocol that
///         pins its salt so both ends derive the same key is using it exactly as specified. That is the
///         "protocol-fixed key derivation" shape, and it is excluded by receiver rather than by a
///         judgement. PBKDF2 is the opposite case — it is <em>defined</em> for passwords, and a fixed salt
///         there makes rainbow tables viable again and gives two users with the same password the same
///         derived key.
///     </para>
///     <para>
///         ⚠ <b>Test methods are exempt</b>, by the attribute test six other rules already use, and here
///         it is load-bearing rather than a courtesy: <b>RFC 6070's PBKDF2 test vectors specify the salt
///         as the literal string <c>"salt"</c></b>. Without the exemption this rule would fail the build
///         of every crypto library that checks itself against the standard's own vectors, at <c>error</c>
///         severity — which is how a reviewer learns to skim past every security finding a tool makes.
///     </para>
///     <para>
///         ⚠ <b>The verifying side is silent by construction and that is the point.</b> Checking a
///         password re-derives with the salt <em>stored alongside the hash</em>, which is a variable, a
///         parameter or a field read from a record — and <see cref="ConstantBytes" /> resolves none of
///         those. So the rule reports the code that <em>creates</em> a bad credential and stays quiet on
///         the code that reads it back, which is the same asymmetry <c>SK5020</c> draws between
///         <c>CreateEncryptor</c> and <c>CreateDecryptor</c>.
///     </para>
///     <para>
///         ⚠ <c>hasFix: false</c>. The edit is to draw the salt per credential and store it beside the
///         derived key, which changes the stored record's format — every credential already written
///         becomes unverifiable by the new code. That is a migration, exactly as it is for <c>SK5005</c>
///         and <c>SK5020</c>.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FixedKeyDerivationSaltAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.FixedKeyDerivationSalt);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var pbkdf2 = start.Compilation.GetTypeByMetadataName(
                    "System.Security.Cryptography.Rfc2898DeriveBytes"
                );

                // No PBKDF2 in the compilation means nothing to say.
                if (pbkdf2 is null) {
                    return;
                }

                var encoding = start.Compilation.GetTypeByMetadataName("System.Text.Encoding");
                var convert = start.Compilation.GetTypeByMetadataName("System.Convert");

                start.RegisterOperationAction(
                    context => Creation(context, pbkdf2, encoding, convert),
                    OperationKind.ObjectCreation
                );
                start.RegisterOperationAction(
                    context => Invocation(context, pbkdf2, encoding, convert),
                    OperationKind.Invocation
                );
            }
        );
    }

    /// <summary>
    ///     <c>new Rfc2898DeriveBytes(password, salt, iterations, hash)</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ These constructors carry <c>SYSLIB0060</c> and are obsolete on by default — "use the static
    ///     <c>Pbkdf2</c> method instead" — so this half of the rule is about code that already exists
    ///     rather than code anybody is writing. The <c>Pbkdf2</c> overloads below carry no obsoletion and
    ///     are where a new fixed salt gets written today, which is why both are covered.
    /// </remarks>
    static void Creation(
        OperationAnalysisContext context,
        INamedTypeSymbol pbkdf2,
        INamedTypeSymbol? encoding,
        INamedTypeSymbol? convert
    ) {
        var creation = (IObjectCreationOperation)context.Operation;
        if (!SymbolEqualityComparer.Default.Equals(creation.Constructor?.ContainingType, pbkdf2)) {
            return;
        }

        Check(context, creation.Arguments, creation.Syntax, encoding, convert);
    }

    /// <summary><c>Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, hash, length)</c>.</summary>
    static void Invocation(
        OperationAnalysisContext context,
        INamedTypeSymbol pbkdf2,
        INamedTypeSymbol? encoding,
        INamedTypeSymbol? convert
    ) {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.Name != "Pbkdf2"
            || !SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, pbkdf2)) {
            return;
        }

        Check(context, invocation.Arguments, invocation.Syntax, encoding, convert);
    }

    /// <summary>
    ///     Finds the salt argument by the <em>API's</em> parameter name and asks whether it is constant.
    /// </summary>
    /// <remarks>
    ///     ⚠ The parameter is named <c>salt</c> by <c>System.Security.Cryptography</c>, not by the code
    ///     under analysis, so this is a fact about the overload rather than the identifier judgement that
    ///     cut <c>SK5008</c>. The overload <c>Rfc2898DeriveBytes(string password, int saltSize)</c> has no
    ///     <c>salt</c> parameter at all — it <em>generates</em> one — so it is silent here without needing
    ///     to be named.
    /// </remarks>
    static void Check(
        OperationAnalysisContext context,
        ImmutableArray<IArgumentOperation> arguments,
        SyntaxNode syntax,
        INamedTypeSymbol? encoding,
        INamedTypeSymbol? convert
    ) {
        if (AsyncContext.IsTestMethod(syntax)) {
            return;
        }

        foreach (var argument in arguments) {
            if (argument.Parameter?.Name != "salt") {
                continue;
            }

            var kind = ConstantBytes.Classify(argument.Value, encoding, convert, out var fieldName);
            if (kind == ConstantByteKind.NotConstant) {
                return;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    argument.Value.Syntax.GetLocation(),
                    Describe(kind, fieldName)
                    + ", so every password derives to the same key on every machine and in every "
                    + "installation — which is the whole thing a salt exists to prevent: a precomputed "
                    + "table built once against this salt breaks every credential at once, and two users "
                    + "who chose the same password are visibly identical in the store; draw the salt with "
                    + "`RandomNumberGenerator.GetBytes(16)` per credential and store it next to the "
                    + "derived key"
                )
            );

            return;
        }
    }

    static string Describe(ConstantByteKind kind, string? fieldName) =>
        kind switch {
            ConstantByteKind.ZeroArray => "the key-derivation salt is an array of zeros",
            ConstantByteKind.LiteralList => "the key-derivation salt is a list of constants",
            ConstantByteKind.LiteralStringBytes => "the key-derivation salt is the bytes of a literal string",
            ConstantByteKind.DecodedLiteral => "the key-derivation salt is decoded from a literal",
            _ => "the key-derivation salt is the constant field `" + fieldName + "`"
        };
}
