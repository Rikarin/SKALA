using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Security;

/// <summary>
///     <c>SK5030</c> — <c>SignedXml.CheckSignature()</c> with no argument, which verifies the document
///     against the key the document itself carries.
/// </summary>
/// <remarks>
///     The parameterless overload takes its key from the signature's own <c>KeyInfo</c> element. So it
///     answers "was this document signed by whoever's key is written inside this document", which is a
///     question every document can answer about itself — an attacker re-signs the payload with a key
///     they generated, writes that key into <c>KeyInfo</c>, and the check passes. It reads exactly like
///     a validation and establishes nothing about who signed anything.
///     <para>
///         ⚠ <b>One fact, and it is entirely local.</b> Unlike <c>SK5009</c>, which needs two
///         assignments about the same object because <c>DtdProcessing.Parse</c> alone is not a
///         vulnerability on .NET Core, the argument list here is the whole finding: an invocation of
///         <c>CheckSignature</c> with zero arguments on something deriving from <c>SignedXml</c>. There
///         is no aliasing question and no flow to follow.
///     </para>
///     <para>
///         ⚠ <b>The one guard, and why it is not the same as no guard.</b> There is a correct-but-rare
///         shape: read <c>signed.KeyInfo</c>, check the certificate in it against a trust store, and
///         only then call <c>CheckSignature()</c>. The key has been established by then and the call is
///         sound. So the rule is silent whenever the enclosing operation block touches <c>KeyInfo</c> at
///         all — deliberately per-block rather than per-object, because at <c>error</c> severity the
///         conservative direction is the one that misses. An author who never reads <c>KeyInfo</c>
///         cannot have validated it.
///     </para>
///     <para>
///         ⚠ <b>Two neighbouring overloads are deliberately outside the rule.</b>
///         <c>CheckSignatureReturningKey(out …)</c> verifies against the embedded key and then
///         <em>hands it to the caller to judge</em>, so whether it is a bug depends on what the caller
///         does with the key — a question about a later statement, not this one.
///         <c>CheckSignature(X509Certificate2, bool)</c> takes a caller-supplied certificate, and
///         whether <c>verifySignatureOnly: true</c> is wrong depends on where that certificate came
///         from. Reporting either would be guessing at <c>error</c>.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class XmlSignatureAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.XmlSignatureUnverifiedKey);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                // ⚠ `System.Security.Cryptography.Xml` is a package rather than part of the shared
                // framework, so this is null in most compilations and the rule costs nothing there.
                var signedXml = start.Compilation.GetTypeByMetadataName("System.Security.Cryptography.Xml.SignedXml");
                if (signedXml is null) {
                    return;
                }

                start.RegisterOperationBlockAction(context => Analyze(context, signedXml));
            }
        );
    }

    static void Analyze(OperationBlockAnalysisContext context, INamedTypeSymbol signedXml) {
        var unverified = new List<IOperation>();
        var readsKeyInfo = false;

        foreach (var block in context.OperationBlocks) {
            Collect(block, signedXml, unverified, ref readsKeyInfo, context.CancellationToken);
        }

        if (readsKeyInfo) {
            return;
        }

        foreach (var invocation in unverified) {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    invocation.Syntax.GetLocation(),
                    "`CheckSignature()` with no argument verifies the document against the key the "
                    + "document itself carries in `KeyInfo`, so it passes for anything an attacker "
                    + "re-signed with a key they generated; pass the key or the certificate you "
                    + "already trust — `CheckSignature(publicKey)` — so the check establishes who "
                    + "signed it"
                )
            );
        }
    }

    static void Collect(
        IOperation operation,
        INamedTypeSymbol signedXml,
        List<IOperation> unverified,
        ref bool readsKeyInfo,
        CancellationToken cancellation
    ) {
        cancellation.ThrowIfCancellationRequested();

        switch (operation) {
            // ⚠ Any mention of `KeyInfo` on a `SignedXml`, read or written, in this block. Writing it
            // is the caller supplying the key material themselves, which is also not the bug.
            case IPropertyReferenceOperation { Property.Name: "KeyInfo" } property
                when Inherits(property.Property.ContainingType, signedXml):
                readsKeyInfo = true;
                break;

            case IInvocationOperation { TargetMethod: { Name: "CheckSignature", Parameters.Length: 0 } method } call
                when Inherits(method.ContainingType, signedXml):
                unverified.Add(call);
                break;
        }

        foreach (var child in operation.ChildOperations) {
            Collect(child, signedXml, unverified, ref readsKeyInfo, cancellation);
        }
    }

    static bool Inherits(ITypeSymbol? type, INamedTypeSymbol ancestor) {
        for (var current = type; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, ancestor)) {
                return true;
            }
        }

        return false;
    }
}
