using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Security;

/// <summary>
///     <c>SK5005</c> — a cipher that is broken, or a mode that leaks the plaintext's structure.
/// </summary>
/// <remarks>
///     docs/plan/08 § "SK5000 — Security".
///     <para>
///         ⚠
///         <b>
///             The id was allocated narrower than doc 08's sentence, and the reason is that the hash half
///             cannot be decided correctly.
///         </b> The catalogue's entry reads "weak hash/cipher (<c>MD5</c>,
///         <c>SHA1</c>, <c>DES</c>, ECB)". The hash half was cut, and not because it would be noisy —
///         a rule that fires often is work for the repository, not a defect in the rule. It was cut because
///         <b>the finding would frequently be wrong</b>, which is a different thing. <c>MD5</c> and
///         <c>SHA-1</c> have a large legitimate population in which they are not security controls at all:
///         cache keys, ETags, content addressing, bucket selection, and every wire protocol that froze its
///         digest choice a decade ago and specifies it normatively. An RFC 6455 WebSocket handshake, for
///         instance, is <em>defined</em> as a SHA-1 of the client key and a fixed GUID; reporting it would
///         be asserting a vulnerability in code that has none and cannot have one. Separating that
///         population from a password digest requires knowing what the hash is compared against and what
///         happens when it matches — a data-flow question about the value's use, which this rule does
///         not ask and which the intra-procedural engine could not answer if it did. ADR-012 makes an id's
///         meaning permanent and forbids widening it later, so the hash half is not this id's to claim: it
///         needs its own number, and an argument about how it will decide the question, before it is built.
///     </para>
///     <para>
///         A cipher has no such population. <c>DES</c> has a 56-bit key and is brute-forced in hours;
///         <c>RC2</c> is worse; ECB encrypts identical blocks to identical ciphertext, which is why the
///         famous penguin is still recognisable through it. None of the three has a non-security use,
///         because a cipher <em>is</em> the security control.
///     </para>
///     <para>
///         ⚠ <c>hasFix: false</c>, and this is the range's "rarely" column doing its job rather than a gap.
///         Rewriting <c>DES.Create()</c> to <c>Aes.Create()</c> compiles, and it changes the key length,
///         the block size and the ciphertext format — so every value already encrypted with the old
///         algorithm becomes unreadable. That is a migration, and a tool that performed it silently under
///         <c>--fix</c> would be destroying data on its own advice.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WeakCipherAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.WeakCipherAlgorithm);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var symmetric = start.Compilation.GetTypeByMetadataName(
                    "System.Security.Cryptography.SymmetricAlgorithm"
                );
                var mode = start.Compilation.GetTypeByMetadataName("System.Security.Cryptography.CipherMode");

                // No `System.Security.Cryptography` in the compilation means nothing to say.
                if (symmetric is null && mode is null) {
                    return;
                }

                var weak = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
                foreach (var name in new[] { "DES", "TripleDES", "RC2" }) {
                    if (start.Compilation.GetTypeByMetadataName("System.Security.Cryptography." + name) is { } type) {
                        weak.Add(type);
                    }
                }

                var algorithms = weak.ToImmutable();
                start.RegisterOperationAction(
                    context => Algorithm(context, algorithms),
                    OperationKind.ObjectCreation,
                    OperationKind.Invocation
                );

                if (symmetric is not null && mode is not null) {
                    start.RegisterOperationAction(
                        context => Mode(context, symmetric, mode),
                        OperationKind.SimpleAssignment
                    );
                }
            }
        );
    }

    /// <summary>
    ///     <c>new DESCryptoServiceProvider()</c>, <c>DES.Create()</c>, and the <c>TripleDES</c> and
    ///     <c>RC2</c> equivalents.
    /// </summary>
    /// <remarks>
    ///     ⚠ Matched by the <em>type produced</em> rather than by the name written, so
    ///     <c>DESCryptoServiceProvider</c>, <c>TripleDESCng</c> and anything else deriving from one of
    ///     the three abstract bases is covered without being listed.
    /// </remarks>
    static void Algorithm(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> weak) {
        if (weak.IsEmpty) {
            return;
        }

        var (type, name) = context.Operation switch {
            IObjectCreationOperation creation => (creation.Type, creation.Type?.Name),
            IInvocationOperation { TargetMethod: { IsStatic: true, Name: "Create" } method } =>
                (method.ContainingType, method.ContainingType?.Name),
            _ => (null, null)
        };

        if (type is null || name is null || Derived(type, weak) is not { } family) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                context.Operation.Syntax.GetLocation(),
                "`"
                + name
                + "` is "
                + Why(family)
                + "; use `Aes` — or `AesGcm`, which authenticates the ciphertext as well as hiding it"
            )
        );
    }

    static string Why(string family) =>
        family switch {
            "DES" => "a 56-bit cipher and is brute-forced in hours on commodity hardware",
            "TripleDES" => "a 64-bit-block cipher, which leaks after ~32 GB under one key (CVE-2016-2183, \"Sweet32\")",
            _ => "a broken cipher with practical key-recovery attacks"
        };

    static string? Derived(ITypeSymbol type, ImmutableArray<INamedTypeSymbol> weak) {
        for (var current = type; current is not null; current = current.BaseType) {
            foreach (var candidate in weak) {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, candidate)) {
                    return candidate.Name;
                }
            }
        }

        return null;
    }

    /// <summary>
    ///     <c>algorithm.Mode = CipherMode.ECB</c>, including inside an object initialiser.
    /// </summary>
    /// <remarks>
    ///     ⚠ Only where <c>ECB</c> is the value <em>being assigned to a cipher's</em> <c>Mode</c>. A
    ///     bare mention of the enum member is not a finding:
    ///     <c>
    /// if (algorithm.Mode == CipherMode.ECB)
    ///  throw new …
    ///     </c> is a guard against exactly this, and reporting it would mean the rule fires
    ///     on the code written to satisfy it.
    /// </remarks>
    static void Mode(OperationAnalysisContext context, INamedTypeSymbol symmetric, INamedTypeSymbol cipherMode) {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        if (assignment.Target is not IPropertyReferenceOperation { Property.Name: "Mode" } property
            || !Inherits(property.Property.ContainingType, symmetric)
            || !IsEcb(assignment.Value, cipherMode)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                assignment.Value.Syntax.GetLocation(),
                "ECB encrypts equal plaintext blocks to equal ciphertext blocks, so the structure of "
                + "the input survives encryption; use `CipherMode.CBC` with a fresh random IV per "
                + "message, or `AesGcm`, which does the IV and the authentication for you"
            )
        );
    }

    static bool IsEcb(IOperation value, INamedTypeSymbol cipherMode) =>
        value is IFieldReferenceOperation { Field: { Name: "ECB", IsStatic: true } field }
        && SymbolEqualityComparer.Default.Equals(field.ContainingType, cipherMode);

    static bool Inherits(ITypeSymbol? type, INamedTypeSymbol ancestor) {
        for (var current = type; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, ancestor)) {
                return true;
            }
        }

        return false;
    }
}
