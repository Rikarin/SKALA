using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Security;

/// <summary>
///     <c>SK5007</c> — a TLS certificate callback that accepts every certificate.
/// </summary>
/// <remarks>
///     docs/plan/08 § "SK5000 — Security". A callback that returns <c>true</c> without reading its
///     arguments turns TLS into encryption without authentication: the connection is still encrypted,
///     to whoever answered. Every guarantee TLS makes rests on the certificate check, so this is not a
///     weakened connection, it is a connection to an unverified party — which is the entire content of
///     a man-in-the-middle attack.
///     <para>
///         ⚠ <b>Detection is by delegate type, not by member name.</b> The target has to be a delegate that
///         returns <c>bool</c> and takes a <c>System.Net.Security.SslPolicyErrors</c>. Nothing else in .NET
///         has that shape, and it covers all four spellings —
///         <c>ServicePointManager.ServerCertificateValidationCallback</c>,
///         <c>HttpClientHandler.ServerCertificateCustomValidationCallback</c>,
///         <c>SslClientAuthenticationOptions.RemoteCertificateValidationCallback</c> and the
///         <c>SslStream</c> constructor argument — without naming any of them, so a fifth spelling arrives
///         covered.
///     </para>
///     <para>
///         ⚠ <b>Only a callback that is <em>provably</em> constant-true.</b> An expression body that is the
///         literal <c>true</c>, a block whose one statement is <c>return true;</c>, or
///         <c>HttpClientHandler.DangerousAcceptAnyServerCertificateValidator</c>. A callback with a
///         condition in it — one that accepts a specific thumbprint, or that is lenient only when
///         <c>errors == SslPolicyErrors.RemoteCertificateChainErrors</c> — is a pinning implementation, and
///         this rule says nothing about it. That is the whole false-positive story, and it is short because
///         the shape is.
///     </para>
///     <para>
///         ⚠ The most common true finding is a test or a local development environment talking to a
///         self-signed certificate, and it still ships at <c>error</c>. Unlike a <c>Thread.Sleep</c> that
///         polls (docs/plan/16 § R3's "true and not what you would change"), this is never correct code —
///         it is an accepted risk, and the difference matters because the accepted risk has a way of
///         reaching production inside a <c>#if</c> that stopped being conditional. The message names the
///         two ways to accept it deliberately.
///     </para>
///     <para>
///         ⚠ This used to end "and <c>SK7050</c> already requires that a <c>#pragma warning disable</c>
///         carry a justification".
///         <b>
///             It does not: <c>SK7050</c> is allocated in docs/plan/08 and has never
///             been built.
///         </b> Nothing today requires a justification on a suppression of this rule, so the
///         visible half of "accept it deliberately and visibly" rests on review rather than on a
///         mechanism — which is worth knowing when deciding whether a baseline entry or a pragma is the
///         better disposal here. The baseline is the one with a diff somebody reads.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CertificateValidationAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CertificateValidationDisabled);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var errors = start.Compilation.GetTypeByMetadataName("System.Net.Security.SslPolicyErrors");
                if (errors is null) {
                    return;
                }

                start.RegisterOperationAction(
                    context => Analyze(context, errors),
                    OperationKind.SimpleAssignment,
                    OperationKind.CompoundAssignment,
                    OperationKind.Argument
                );
            }
        );
    }

    static void Analyze(OperationAnalysisContext context, INamedTypeSymbol sslPolicyErrors) {
        var (target, value) = context.Operation switch {
            ISimpleAssignmentOperation assignment => (assignment.Target.Type, assignment.Value),
            ICompoundAssignmentOperation compound => (compound.Target.Type, compound.Value),
            IArgumentOperation argument => (argument.Parameter?.Type, argument.Value),
            _ => (null, null)
        };

        if (value is null || !IsCertificateCallback(target, sslPolicyErrors) || !AlwaysAccepts(value)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                value.Syntax.GetLocation(),
                "this callback accepts every certificate, so the connection is encrypted but not "
                + "authenticated and any party that can answer can read it; delete the callback to "
                + "get the platform's validation back, or — if a specific self-signed certificate "
                + "really has to be trusted — compare `certificate.GetCertHashString()` against the "
                + "one thumbprint you mean and return `false` for everything else"
            )
        );
    }

    /// <summary>
    ///     Whether a type is "the shape .NET uses to ask 'is this certificate acceptable'".
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>RemoteCertificateValidationCallback</c> is a named delegate and
    ///     <c>ServerCertificateCustomValidationCallback</c> is a <c>Func&lt;…&gt;</c>; testing for
    ///     "returns <c>bool</c> and one parameter is <c>SslPolicyErrors</c>" covers both without
    ///     caring which.
    /// </remarks>
    static bool IsCertificateCallback(ITypeSymbol? type, INamedTypeSymbol sslPolicyErrors) {
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Delegate } delegateType
            || delegateType.DelegateInvokeMethod is not { } invoke
            || invoke.ReturnType.SpecialType != SpecialType.System_Boolean) {
            return false;
        }

        foreach (var parameter in invoke.Parameters) {
            if (SymbolEqualityComparer.Default.Equals(parameter.Type.OriginalDefinition, sslPolicyErrors)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether the callback returns <c>true</c> for every input, provably and without reading one.
    /// </summary>
    /// <remarks>
    ///     ⚠ Everything this cannot prove is silence. A lambda with a branch, a method group pointing
    ///     at a named method, a field holding a delegate — none of them is reported, because following
    ///     any of them is the inter-procedural analysis doc 08 puts out of scope, and a wrong
    ///     <c>error</c> here fails somebody's build over a pinning implementation.
    /// </remarks>
    static bool AlwaysAccepts(IOperation value) {
        var operation = Unwrap(value);

        // `HttpClientHandler.DangerousAcceptAnyServerCertificateValidator` — the framework's own
        // name for it, and the name is not an accident.
        //
        // ⚠ A *property*, not a field. Matching only `IFieldReferenceOperation` here made the rule
        // miss the single most explicit way of writing this finding, and it missed it silently:
        // the corpus caught it, reading the code did not.
        if (operation is IPropertyReferenceOperation { Property.Name: "DangerousAcceptAnyServerCertificateValidator" }
            or IFieldReferenceOperation { Field.Name: "DangerousAcceptAnyServerCertificateValidator" }) {
            return true;
        }

        if (operation is not IAnonymousFunctionOperation { Body: { } body }) {
            return false;
        }

        // An expression-bodied lambda is a block holding one `return`, so both spellings —
        // `(a, b, c, d) => true` and `delegate { return true; }` — arrive here identically.
        IOperation? single = null;
        foreach (var statement in body.Operations) {
            if (single is not null) {
                return false;
            }

            single = statement;
        }

        return single is IReturnOperation { ReturnedValue: { } returned }
            && returned.ConstantValue is { HasValue: true, Value: true };
    }

    static IOperation Unwrap(IOperation operation) {
        while (true) {
            switch (operation) {
                case IDelegateCreationOperation delegateCreation:
                    operation = delegateCreation.Target;
                    continue;
                case IConversionOperation conversion:
                    operation = conversion.Operand;
                    continue;
                case IParenthesizedOperation parenthesized:
                    operation = parenthesized.Operand;
                    continue;
                default:
                    return operation;
            }
        }
    }
}
