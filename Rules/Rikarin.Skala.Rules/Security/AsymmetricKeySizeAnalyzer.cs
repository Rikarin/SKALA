using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Security;

/// <summary>
///     <c>SK5021</c> — an RSA or DSA key generated at a size that is no longer out of reach.
/// </summary>
/// <remarks>
///     docs/plan/08 § "SK5000 — Security". NIST SP 800-57 has put the floor for RSA and finite-field
///     keys at 2048 bits since 2014; a 1024-bit modulus has an estimated work factor of about 80 bits,
///     which is inside what a well-funded attacker can already buy.
///     <para>
///         ⚠ <b>The SDK covers exactly one spelling of this, and it is the legacy one.</b> Measured on a
///         plain <c>net10.0</c> project outside this repository, with <c>AnalysisMode=All</c> so the
///         analyzers really ran: <c>CA5385</c> fires on <c>new RSACryptoServiceProvider(1024)</c> and on
///         <b>nothing else</b> — not <c>RSA.Create(1024)</c>, which is the modern factory and the
///         spelling every current sample uses, not <c>DSA.Create(1024)</c>, and not
///         <c>rsa.KeySize = 1024</c> even on an <c>RSACryptoServiceProvider</c>, the very type
///         <c>CA5385</c>'s own message names. <c>CA5384</c> reports <c>DSACryptoServiceProvider</c> as an
///         algorithm regardless of its size and likewise misses <c>DSA.Create</c>. Both are off by
///         default: <c>analysislevelsecurity_10_default.globalconfig</c> carries no rule entries at all,
///         so a security <c>CA</c>'s default is its own descriptor's, and for this family that is off.
///     </para>
///     <para>
///         ⚠ <b>Elliptic curves are excluded deliberately, not overlooked.</b> A 256-bit <c>ECDsa</c> key
///         is stronger than a 2048-bit RSA one; applying a bit-count floor across algorithm families is
///         the mistake that would make this rule report the recommended replacement for what it reports.
///         Only types deriving from <c>RSA</c> and <c>DSA</c> are examined.
///     </para>
///     <para>
///         ⚠ <b>Test methods are exempt</b>, by the same attribute test five other rules already use.
///         Generating a deliberately small key to keep a test fast is a real and correct pattern, and a
///         security rule at <c>error</c> that breaks a test suite is how a reviewer learns to skim past
///         every security finding the tool makes.
///     </para>
///     <para>
///         ⚠ <c>fixIsSafe: false</c>, and the fix is still worth carrying. Raising the argument to 2048
///         compiles, binds and is strictly stronger — but the size is half of an agreement with whoever
///         holds the other end of the protocol, and a tool that silently changed it under <c>--fix</c>
///         would be renegotiating that agreement on its own authority.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsymmetricKeySizeAnalyzer : DiagnosticAnalyzer {
    /// <summary>NIST SP 800-57 Part 1 Rev. 5 — the floor for RSA and finite-field keys.</summary>
    const int Floor = 2048;

    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UndersizedAsymmetricKey);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var families = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
                foreach (var name in new[] { "RSA", "DSA" }) {
                    if (start.Compilation.GetTypeByMetadataName("System.Security.Cryptography." + name) is { } type) {
                        families.Add(type);
                    }
                }

                var known = families.ToImmutable();

                // Neither family in the compilation means nothing to say.
                if (known.IsEmpty) {
                    return;
                }

                start.RegisterOperationAction(
                    context => Generated(context, known),
                    OperationKind.ObjectCreation,
                    OperationKind.Invocation
                );
                start.RegisterOperationAction(context => Property(context, known), OperationKind.SimpleAssignment);
            }
        );
    }

    /// <summary>
    ///     <c>RSA.Create(1024)</c>, <c>DSA.Create(1024)</c>, <c>new RSACryptoServiceProvider(1024)</c> and
    ///     <c>new RSACryptoServiceProvider(1024, cspParameters)</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The arity is deliberately not pinned at one, and a surviving sabotage is why.</b> This
    ///     method first read <c>arguments.Length != 1</c>, which rejected nothing that
    ///     <c>IsDefaultOrEmpty</c> had not already rejected — and for arity two it silently declined
    ///     <c>new RSACryptoServiceProvider(1024, cspParameters)</c>, which is a real overload holding a
    ///     real 1024-bit key. Inverting the clause turned no test red, which is how the hole was found
    ///     rather than the clause being pronounced dead.
    ///     <para>
    ///         The test that actually separates a key size from <c>RSA.Create(RSAParameters)</c> and
    ///         <c>RSA.Create(string)</c> is that the <em>first parameter</em> is an <c>int</c>, and every
    ///         RSA and DSA overload that takes a size takes it first.
    ///     </para>
    ///     <para>
    ///         ⚠ The cheap test is asked first on purpose. This action runs on every object creation and
    ///         every static <c>Create</c> in the compilation, and <c>Family</c> walks a base-type chain;
    ///         almost nothing passes an <c>int</c> first, so the <c>SpecialType</c> read keeps the walk
    ///         off the hot path.
    ///     </para>
    /// </remarks>
    static void Generated(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> families) {
        var (type, arguments) = context.Operation switch {
            IObjectCreationOperation creation => (creation.Type, creation.Arguments),
            IInvocationOperation { TargetMethod: { IsStatic: true, Name: "Create" } method } invocation =>
                (method.ContainingType, invocation.Arguments),
            _ => (null, default)
        };

        if (type is null || arguments.IsDefaultOrEmpty) {
            return;
        }

        var size = arguments[0];
        if (size.Parameter?.Type.SpecialType != SpecialType.System_Int32
            || Family(type, families) is not { } family) {
            return;
        }

        Examine(context, family, size.Value);
    }

    /// <summary><c>rsa.KeySize = 1024</c>, including inside an object initialiser.</summary>
    static void Property(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> families) {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        if (assignment.Target is not IPropertyReferenceOperation { Property.Name: "KeySize" } target) {
            return;
        }

        // ⚠ On the *receiver's* type rather than on the property's containing type: `KeySize` is
        // declared on `AsymmetricAlgorithm`, whose other descendants are the elliptic curves this
        // rule must not report.
        var receiver = target.Instance?.Type ?? target.Property.ContainingType;
        if (Family(receiver, families) is { } family) {
            Examine(context, family, assignment.Value);
        }
    }

    static void Examine(OperationAnalysisContext context, string family, IOperation size) {
        if (Unwrap(size).ConstantValue is not { HasValue: true, Value: int bits }
            || bits <= 0
            || bits >= Floor
            || AsyncContext.IsTestMethod(size.Syntax)) {
            return;
        }

        // ⚠ The whole argument expression, so a `const` reference is replaced rather than edited in
        // place — the declaration may be shared with something this rule has not looked at.
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                size.Syntax.GetLocation(),
                FixEdits.Pack((size.Syntax.Span, Floor.ToString(CultureInfo.InvariantCulture))),
                "a "
                + bits.ToString(CultureInfo.InvariantCulture)
                + "-bit "
                + family
                + " key has an estimated work factor well below the 112 bits NIST SP 800-57 has "
                + "required since 2014, and factoring it is a budget rather than a breakthrough; "
                + "generate at least "
                + Floor.ToString(CultureInfo.InvariantCulture)
                + " bits, or move to `ECDsa`, where a 256-bit key is stronger than a 3072-bit "
                + family
                + " one"
            )
        );
    }

    static string? Family(ITypeSymbol? type, ImmutableArray<INamedTypeSymbol> families) {
        for (var current = type; current is not null; current = current.BaseType) {
            foreach (var candidate in families) {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, candidate)) {
                    return candidate.Name;
                }
            }
        }

        return null;
    }

    static IOperation Unwrap(IOperation operation) {
        var current = operation;
        while (current is IConversionOperation conversion) {
            current = conversion.Operand;
        }

        return current;
    }
}
