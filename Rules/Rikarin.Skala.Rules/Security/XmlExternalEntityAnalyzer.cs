using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Security;

/// <summary>
/// <c>SK5009</c> — an XML reader configured to parse a DTD <em>and</em> to fetch what it references.
/// </summary>
/// <remarks>
/// docs/plan/08 § "SK5000 — Security" names this rule "XML reader with DTD processing enabled", and
/// the rule that shipped needs <b>two</b> facts rather than that one. The reason is a change in the
/// platform, and it is worth writing down because it is the difference between a rule and a rule
/// that is wrong.
/// <para>
/// ⚠ On .NET Framework, <c>DtdProcessing = DtdProcessing.Parse</c> was enough: the default
/// <c>XmlResolver</c> was an <c>XmlUrlResolver</c>, so a DTD could name
/// <c>file:///etc/passwd</c> or an attacker's URL and the parser would fetch it. On .NET Core and
/// later the default resolver is <c>null</c>, so parsing a DTD resolves nothing external and is
/// not XXE. A rule that fired on <c>DtdProcessing.Parse</c> alone would therefore report, at
/// <c>error</c> severity, every program that legitimately parses a document with entity
/// declarations in it — on a platform where that is not a vulnerability. So the rule fires when the
/// resolver is put back <em>and</em> the DTD is parsed, which is the combination that reopens it.
/// </para>
/// <para>
/// ⚠ Both facts must be explicit, and both must be about the same object, in the same method. A
/// resolver assigned from a variable is silence, because whether that variable is null is a
/// question about another method. The cost of the strictness is coverage — the shape occurs zero
/// times in either reference tree — and the alternative is guessing at <c>error</c>.
/// </para>
/// <para>
/// ⚠ <b>Known gap, stated rather than hidden:</b> <c>XmlDocument</c> and <c>XmlDataDocument</c>
/// have an <c>XmlResolver</c> and no <c>DtdProcessing</c>, so the two-fact rule cannot see them and
/// they are outside it. Closing that needs a different argument about what the safe default is, and
/// it needs its own id rather than a widening of this one (ADR-012).
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class XmlExternalEntityAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.XmlExternalEntityResolution);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var processing = start.Compilation.GetTypeByMetadataName("System.Xml.DtdProcessing");
                var resolver = start.Compilation.GetTypeByMetadataName("System.Xml.XmlResolver");
                if (processing is null || resolver is null) {
                    return;
                }

                start.RegisterOperationBlockAction(context => Analyze(context, processing, resolver));
            }
        );
    }

    /// <summary>
    /// ⚠ A block action rather than an operation action, because the finding is a <em>pair</em> of
    /// assignments and neither one alone is anything.
    /// </summary>
    static void Analyze(
        OperationBlockAnalysisContext context,
        INamedTypeSymbol dtdProcessing,
        INamedTypeSymbol xmlResolver
    ) {
        // Keyed by the object being configured: the local it lives in, or — inside an object
        // initialiser, where there is no local yet — the creation itself.
        var parsesDtd = new Dictionary<object, IOperation>();
        var resolves = new HashSet<object>();

        foreach (var block in context.OperationBlocks) {
            Collect(block, dtdProcessing, xmlResolver, parsesDtd, resolves, context.CancellationToken);
        }

        foreach (var entry in parsesDtd) {
            if (!resolves.Contains(entry.Key)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    entry.Value.Syntax.GetLocation(),
                    "this reader parses the DTD and has a resolver that will fetch what the DTD "
                    + "names, so a document can read any file the process can and reach any host it "
                    + "can; set `XmlResolver = null` to keep DTD parsing without external lookups, "
                    + "or `DtdProcessing = DtdProcessing.Prohibit` if the documents have no DTD"
                )
            );
        }
    }

    static void Collect(
        IOperation operation,
        INamedTypeSymbol dtdProcessing,
        INamedTypeSymbol xmlResolver,
        Dictionary<object, IOperation> parsesDtd,
        HashSet<object> resolves,
        CancellationToken cancellation
    ) {
        cancellation.ThrowIfCancellationRequested();

        if (operation is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation property } assignment
            && Owner(property) is { } owner) {
            if (property.Property.Name == "DtdProcessing"
                && SymbolEqualityComparer.Default.Equals(property.Property.Type.OriginalDefinition, dtdProcessing)
                && assignment.Value is IFieldReferenceOperation { Field.Name: "Parse" }) {
                parsesDtd[owner] = assignment.Value;
            } else if (property.Property.Name == "XmlResolver"
                       && Inherits(property.Property.Type, xmlResolver)
                       && FetchesExternally(assignment.Value, xmlResolver)) {
                resolves.Add(owner);
            }
        }

        foreach (var child in operation.ChildOperations) {
            Collect(child, dtdProcessing, xmlResolver, parsesDtd, resolves, cancellation);
        }
    }

    /// <summary>
    /// What object this property assignment configures, or <c>null</c> when that is not decidable.
    /// </summary>
    /// <remarks>
    /// ⚠ Only a local and only an object initialiser. <c>this.Settings.DtdProcessing = …</c> and
    /// <c>Cache[key].DtdProcessing = …</c> are both silence: proving that two such expressions name
    /// the same object is alias analysis, and getting it wrong in either direction is bad — a false
    /// pair is a wrong <c>error</c>, and a missed pair is a miss.
    /// </remarks>
    static object? Owner(IPropertyReferenceOperation property) {
        switch (property.Instance) {
            case ILocalReferenceOperation local:
                return local.Local;

            // Inside `new XmlReaderSettings { … }` the instance is the object under construction.
            case IInstanceReferenceOperation:
                for (var current = property.Parent; current is not null; current = current.Parent) {
                    if (current is IObjectCreationOperation creation) {
                        return creation;
                    }
                }

                return null;

            default:
                return null;
        }
    }

    /// <summary>
    /// Whether the assigned resolver provably goes and gets things.
    /// </summary>
    /// <remarks>
    /// ⚠ Only a <c>new</c> of a resolver type. <c>null</c> is the safe value and the fix;
    /// a variable is unknowable; <c>XmlSecureResolver</c> is a deliberate, restricted resolver and
    /// is excluded by name, because reporting it would be reporting the mitigation.
    /// </remarks>
    static bool FetchesExternally(IOperation value, INamedTypeSymbol xmlResolver) =>
        // ⚠ Through the conversion. Assigning an `XmlUrlResolver` to an `XmlResolver?` property is
        // an implicit reference conversion, and Roslyn wraps the creation in an
        // `IConversionOperation` — so a pattern match against the bare creation sees nothing, and
        // the rule was silent on its own positive fixtures until this unwrapped.
        Unwrap(value) is IObjectCreationOperation { Type: { } type }
        && type.Name != "XmlSecureResolver"
        && Inherits(type, xmlResolver);

    static IOperation Unwrap(IOperation operation) {
        while (true) {
            switch (operation) {
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

    static bool Inherits(ITypeSymbol? type, INamedTypeSymbol ancestor) {
        for (var current = type; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, ancestor)) {
                return true;
            }
        }

        return false;
    }
}
