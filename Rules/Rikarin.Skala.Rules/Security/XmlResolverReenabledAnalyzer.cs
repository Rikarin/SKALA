using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Security;

/// <summary>
///     <c>SK5040</c> — an external-fetching <c>XmlResolver</c> assigned where the platform's default is
///     <c>null</c>.
/// </summary>
/// <remarks>
///     <c>SK5009</c>'s own entry names this as a gap and says it needs its own id rather than a widening
///     (ADR-012), because the two rules ask different questions. <c>SK5009</c> needs <b>two</b> facts —
///     a resolver <em>and</em> <c>DtdProcessing.Parse</c> — because <c>XmlReaderSettings</c> defaults
///     <c>DtdProcessing</c> to <c>Prohibit</c>, so a resolver alone on that type resolves nothing. This
///     rule needs <b>one</b> fact, because its receivers have no such second switch to close.
///     <para>
///         ⚠
///         <b>
///             The receiver set is the whole argument, and it is drawn on where the safe default
///             lives.
///         </b> <c>XmlDocument</c> (and <c>XmlDataDocument</c> under it) has an
///         <c>XmlResolver</c> and no <c>DtdProcessing</c> at all, so the resolver is the only switch
///         there is. <c>XmlTextReader</c> has both, and its <c>DtdProcessing</c> defaults to
///         <c>Parse</c> — so again the resolver alone decides. On both, <c>XmlResolver</c> has defaulted
///         to <c>null</c> since .NET Framework 4.5.2, which makes an explicit <c>XmlUrlResolver</c> a
///         deliberate re-enable rather than an omission: the defect is written out rather than inferred,
///         which is the shape this range ships and the shape it declines.
///     </para>
///     <para>
///         ⚠ <b><c>XmlReaderSettings</c> is deliberately excluded.</b> It is <c>SK5009</c>'s receiver, its
///         <c>DtdProcessing</c> defaults to <c>Prohibit</c>, and a resolver on it with the default
///         processing fetches nothing. Including it would both report correct code and double-report
///         every finding <c>SK5009</c> already makes.
///     </para>
///     <para>
///         ⚠ <b>Measured unhosted, with a live control.</b> On a plain <c>net10.0</c> project outside this
///         repository — empty <c>Directory.Build.props</c>/<c>.targets</c>, <c>root = true</c>
///         <c>.editorconfig</c> — built with <c>AnalysisMode=All</c> and every <c>CA3xxx</c>/<c>CA5xxx</c>
///         raised to <c>warning</c>, all four spellings below produced <b>nothing</b>:
///         <c>new XmlDocument { XmlResolver = new XmlUrlResolver() }</c>, the same as a post-construction
///         assignment, <c>XmlTextReader.XmlResolver</c>, and <c>XmlReaderSettings.XmlResolver</c>.
///         <c>CA3075</c> and <c>CA3077</c> are titled for exactly this and deliver none of it, and
///         <c>CA3077</c> is <c>IsEnabledByDefault=True, DefaultSeverity=Hidden</c>, so it runs silently in
///         consumer builds already and still finds nothing. ⚠ A planted <c>DataSet.ReadXml</c> in the same
///         file fired <c>CA2351</c> and <c>CA5366</c>, so the zero is <b>shape present and declined</b>
///         rather than a dead analysis run.
///     </para>
///     <para>
///         ⚠ <c>hasFix: false</c>, for <c>SK5009</c>'s reason rather than a weaker one. The mitigation is
///         <c>XmlResolver = null</c>, which is the platform default — but this code went out of its way to
///         leave that default, so the edit removes a capability the author asked for. Whether the
///         documents legitimately reference external entities is a question about the data, and if they
///         do, the answer is <c>XmlSecureResolver</c> with a restricted permission set rather than
///         <c>null</c>. Neither is an edit a tool may apply unreviewed.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class XmlResolverReenabledAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.XmlResolverReenabled);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var resolver = start.Compilation.GetTypeByMetadataName("System.Xml.XmlResolver");
                if (resolver is null) {
                    return;
                }

                // ⚠ Both receivers are looked up rather than matched by name, so a type that merely
                // calls itself `XmlDocument` in somebody's own namespace is not a finding.
                var document = start.Compilation.GetTypeByMetadataName("System.Xml.XmlDocument");
                var reader = start.Compilation.GetTypeByMetadataName("System.Xml.XmlTextReader");
                if (document is null && reader is null) {
                    return;
                }

                start.RegisterOperationAction(
                    context => Analyze(context, resolver, document, reader),
                    OperationKind.SimpleAssignment
                );
            }
        );
    }

    static void Analyze(
        OperationAnalysisContext context,
        INamedTypeSymbol xmlResolver,
        INamedTypeSymbol? document,
        INamedTypeSymbol? textReader
    ) {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        if (assignment.Target is not IPropertyReferenceOperation { Property.Name: "XmlResolver" } target) {
            return;
        }

        // The receiver decides the rule, not the property name: `XmlReaderSettings` has an
        // `XmlResolver` too and belongs to SK5009.
        var receiver = target.Property.ContainingType;
        var kind = Inherits(receiver, document)
            ? "XmlDocument"
            : Inherits(receiver, textReader)
                ? "XmlTextReader"
                : null;

        if (kind is null || !FetchesExternally(assignment.Value, xmlResolver)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                assignment.Syntax.GetLocation(),
                "`XmlResolver` on an `"
                + kind
                + "` defaults to `null` on this platform, and this assignment puts an external-fetching "
                + "resolver back — so a DTD in the document can name any file the process can read and "
                + "any host it can reach, and the parser will go and get it; leave the default, or use "
                + "`XmlSecureResolver` if the documents genuinely reference entities"
            )
        );
    }

    /// <summary>
    ///     Whether the assigned resolver provably goes and gets things.
    /// </summary>
    /// <remarks>
    ///     ⚠ Only a <c>new</c> of a resolver type, written at the assignment. <c>null</c> is the safe
    ///     value and the mitigation; a variable is unknowable and following it is the inter-procedural
    ///     analysis doc 08 puts out of scope; <c>XmlSecureResolver</c> is excluded by name because
    ///     reporting it would report the mitigation. This is <c>SK5009</c>'s test, and the two are
    ///     deliberately identical so that a resolver expression cannot mean one thing to one rule and
    ///     something else to the other.
    ///     <para>
    ///         ⚠ Through the conversion. Assigning an <c>XmlUrlResolver</c> to an <c>XmlResolver?</c>
    ///         property is an implicit reference conversion and Roslyn wraps the creation in an
    ///         <c>IConversionOperation</c>, so a pattern match against the bare creation sees nothing —
    ///         the defect that once left <c>SK5009</c> silent on its own positive fixtures.
    ///     </para>
    /// </remarks>
    static bool FetchesExternally(IOperation value, INamedTypeSymbol xmlResolver) =>
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
}
