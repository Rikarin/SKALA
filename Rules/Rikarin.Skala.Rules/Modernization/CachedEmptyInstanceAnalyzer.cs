using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1073</c> — <c>new EventArgs()</c> is <c>EventArgs.Empty</c>.
/// </summary>
/// <remarks>
///     <para>
///         ⚠
///         <b>
///             The table is four entries long and it is a table on purpose: "this type has a cached
///             instance" is not a property that generalizes.
///         </b> Each entry carries its own reason.
///         <c>EventArgs</c> has no instance state and no member that could separate one instance from
///         another, so the singleton and a fresh one differ only by reference identity.
///         <c>Guid.Empty</c>, <c>TimeSpan.Zero</c> and <c>CancellationToken.None</c> are the default
///         value of a struct, where identity is not observable at all.
///     </para>
///     <para>
///         ⚠ <b>The zero-length-array half of this concept is deliberately absent.</b> <c>new T[0]</c>
///         is <c>CA1825</c>, which ships in the SDK, and ADR-008 hosts rather than rebuilds.
///     </para>
///     <para>
///         ⚠
///         <b>
///             A parameter's default value is the trap, and it looks exactly like the shape that is
///             not.
///         </b> <c>void M(TimeSpan t = new TimeSpan())</c> compiles and <c>= TimeSpan.Zero</c> does
///         not: a default has to be a compile-time constant and a <c>static readonly</c> field is not
///         one. An attribute argument and a constant pattern are the same story, and all three are
///         excluded by where they sit rather than by what they contain.
///     </para>
///     <para>
///         ⚠ The replacement reproduces the type <em>as the file spells it</em>, so an alias or a
///         <c>using</c>-shortened name stays spelled that way and the fix never needs a <c>using</c>
///         the file does not have. That is also why a target-typed <c>new()</c> is not matched: there
///         is no written type to build the replacement from.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CachedEmptyInstanceAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CachedEmptyInstance);

    /// <summary>
    ///     Metadata name → the member holding the instance, and whether <c>default(T)</c> is it too.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>default(EventArgs)</c> is <c>null</c>, not <c>EventArgs.Empty</c> — which is why the
    ///     second column exists rather than being assumed from the first. Only the three value types
    ///     have a default equal to their cached instance.
    /// </remarks>
    static readonly (string Type, string Member, bool DefaultIsIt)[] Cached = [
        ("System.EventArgs", "Empty", false),
        ("System.Guid", "Empty", true),
        ("System.TimeSpan", "Zero", true),
        ("System.Threading.CancellationToken", "None", true)
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var table = Resolve(start.Compilation);
                if (table.Count == 0) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, table),
                    SyntaxKind.ObjectCreationExpression,
                    SyntaxKind.DefaultExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, IReadOnlyDictionary<ISymbol, string> table) {
        var (written, isDefault) = context.Node switch {
            ObjectCreationExpressionSyntax {
                ArgumentList: null or { Arguments.Count: 0 },
                Initializer: null
            } creation => (creation.Type, false),
            DefaultExpressionSyntax defaultExpression => (defaultExpression.Type, true),
            _ => (null, false)
        };

        if (written is null) {
            return;
        }

        var node = (ExpressionSyntax)context.Node;
        if (SitsWhereAConstantIsRequired(node)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetTypeInfo(node, cancellation).Type is not { } created
            || !table.TryGetValue(created, out var member)) {
            return;
        }

        // ⚠ `default(EventArgs)` is null and `EventArgs.Empty` is not, so the reference-type entries
        // answer the creation form only.
        if (isDefault && !DefaultIsTheCachedInstance(created)) {
            return;
        }

        if (RewriteGuards.ContainsCommentOrDirective(node)) {
            return;
        }

        var replacement = written.ToString() + "." + member;
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                node.GetLocation(),
                FixEdits.Pack((node.Span, replacement)),
                "The framework already holds this value: `" + RewriteGuards.Trim(replacement) + "`"
            )
        );
    }

    /// <summary>
    ///     The entries this compilation can actually see, keyed by the framework type symbol.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two things are proved here rather than assumed, and each has burned a rule in this
    ///     repository before. The type must come from a <em>referenced assembly</em>, so a source type
    ///     of the same name is never matched — the same lookalike guard <c>SK1022</c> and
    ///     <c>SK1025</c> carry. And the member must exist, be static, and be of the type itself: a
    ///     table that assumed <c>Empty</c> is there would emit a fix that does not compile the day a
    ///     framework drops one, and nothing else in the rule would notice.
    /// </remarks>
    static IReadOnlyDictionary<ISymbol, string> Resolve(Compilation compilation) {
        var result = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        foreach (var (name, member, _) in Cached) {
            if (compilation.GetTypeByMetadataName(name) is not { } type
                || SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly)) {
                continue;
            }

            foreach (var candidate in type.GetMembers(member)) {
                if (candidate is not { IsStatic: true, DeclaredAccessibility: Accessibility.Public }) {
                    continue;
                }

                var held = candidate switch {
                    IFieldSymbol field => field.Type,
                    IPropertySymbol { GetMethod: not null } property => property.Type,
                    _ => null
                };

                if (held is not null && SymbolEqualityComparer.Default.Equals(held, type)) {
                    result[type] = member;
                    break;
                }
            }
        }

        return result;
    }

    static bool DefaultIsTheCachedInstance(ITypeSymbol type) {
        foreach (var (name, _, defaultIsIt) in Cached) {
            if (defaultIsIt && string.Equals(type.ToDisplayString(), name, System.StringComparison.Ordinal)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ Whether the expression sits where the compiler demands a constant.
    /// </summary>
    /// <remarks>
    ///     <c>new TimeSpan()</c> is a legal default for an optional parameter and <c>TimeSpan.Zero</c>
    ///     is not — a <c>static readonly</c> field is not a compile-time constant. The same holds for an
    ///     attribute argument, a constant pattern and a <c>case</c> label. This is the one position
    ///     where the fix turns compiling code into <c>CS1736</c>, so it is asked before anything else.
    /// </remarks>
    static bool SitsWhereAConstantIsRequired(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            switch (current) {
                case EqualsValueClauseSyntax { Parent: ParameterSyntax }:
                case AttributeArgumentSyntax:
                case ConstantPatternSyntax:
                case CaseSwitchLabelSyntax:
                    return true;

                case StatementSyntax:
                case MemberDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }
}
