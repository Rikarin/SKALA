using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>SK1023: a private, readonly object used exclusively as a lock target.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DedicatedLockAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.DedicatedLock);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!Supports(start.Compilation)) {
                    return;
                }

                // ⚠ #343: `System.Threading.Lock` is net9.0+, and a multi-targeted project is opened
                // as one compilation per moniker over one set of source files. Asking only
                // `start.Compilation` answers "some framework here has Lock" and the rewrite lands in
                // a file every framework compiles — a `netstandard2.1;net10.0` library stopped
                // building with CS0234 on the netstandard2.1 leg after `skala fix --safe`. The host
                // publishes the other monikers' compilations; this asks them the same question and
                // withholds the finding on any document one of them cannot compile the answer for.
                var unavailable = FrameworkAvailability.PathsWithout(start.Options, Supports);
                start.RegisterSyntaxNodeAction(
                    node => Analyze(node, unavailable),
                    SyntaxKind.FieldDeclaration
                );
            }
        );
    }

    /// <summary>
    ///     Whether this compilation has the real <c>System.Threading.Lock</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ The whole condition, not just "the name resolves", and it is asked of every sibling
    ///     compilation unchanged (#343). A moniker that resolves a <em>source-declared</em>
    ///     <c>System.Threading.Lock</c>, or one whose shape does not match, is just as unable to
    ///     compile the rewrite as one where the namespace member does not exist at all.
    /// </remarks>
    static bool Supports(Compilation compilation) {
        var type = compilation.GetTypeByMetadataName("System.Threading.Lock");
        return SkalaRule.MeetsLanguageVersion(compilation, "13.0")
            && type is not null
            && !type.Locations.Any(static location => location.IsInSource)
            && type.InstanceConstructors.Any(static constructor =>
                constructor.DeclaredAccessibility == Accessibility.Public && constructor.Parameters.Length == 0
            )
            && type.GetMembers("EnterScope")
                .OfType<IMethodSymbol>()
                .Any(method => !method.IsStatic
                    && method.DeclaredAccessibility == Accessibility.Public
                    && method.Parameters.Length == 0
                    && method.ReturnType is INamedTypeSymbol { Name: "Scope", IsRefLikeType: true } scope
                    && SymbolEqualityComparer.Default.Equals(scope.ContainingType, type)
                    && scope.GetMembers("Dispose")
                        .OfType<IMethodSymbol>()
                        .Any(static dispose => !dispose.IsStatic
                            && dispose.DeclaredAccessibility == Accessibility.Public
                            && dispose.Parameters.Length == 0
                            && dispose.ReturnsVoid
                        )
                );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, ImmutableHashSet<string> unavailable) {
        var declaration = (FieldDeclarationSyntax)context.Node;
        if (declaration.Parent is not ClassDeclarationSyntax
            || declaration.Declaration.Variables.Count != 1
            || declaration.AttributeLists.Count != 0
            || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(declaration.SyntaxTree, declaration.Span)
            || declaration.Ancestors()
                .OfType<TypeDeclarationSyntax>()
                .Any(static type => type.Modifiers.Any(SyntaxKind.PartialKeyword))) {
            return;
        }

        // ⚠ Before any semantic work: this document is compiled by a target framework that has no
        // `System.Threading.Lock`, so the rewrite does not compile there however good it looks here.
        if (!unavailable.IsEmpty && unavailable.Contains(declaration.SyntaxTree.FilePath)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        var variable = declaration.Declaration.Variables[0];
        if (variable.Initializer?.Value is not BaseObjectCreationExpressionSyntax { Initializer: null } creation
            || model.GetDeclaredSymbol(variable, cancellation) is not IFieldSymbol {
                DeclaredAccessibility: Accessibility.Private,
                IsReadOnly: true,
                Type.SpecialType: SpecialType.System_Object
            } field
            || model.GetSymbolInfo(creation, cancellation).Symbol is not IMethodSymbol {
                Parameters.Length: 0,
                ContainingType.SpecialType: SpecialType.System_Object
            }) {
            return;
        }

        var root = declaration.SyntaxTree.GetRoot(cancellation);
        // Inactive references and conditional fields must not escape the whole-file reference proof.
        if (root.ContainsDirectives) {
            return;
        }

        if (!EveryReferenceIsALockTarget(root, model, field, cancellation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                variable.Identifier.GetLocation(),
                FixEdits.Pack(
                    (declaration.Declaration.Type.Span, TypeName(model, declaration.Declaration.Type.SpanStart)),
                    (creation.Span, "new()")
                ),
                "Use System.Threading.Lock for this private synchronization-only field"
            )
        );
    }

    /// <summary>
    ///     The whole-file proof: at least one reference, and every bound one a direct lock target.
    /// </summary>
    /// <remarks>
    ///     ⚠ Extracted rather than inlined, and the reason is measured: adding the #343 sibling guard
    ///     to <see cref="Analyze" /> took its cognitive complexity from the baselined 18 to 20, which
    ///     is a <em>new</em> <c>SK7002</c> because the number is in the message and therefore in the
    ///     fingerprint. Splitting the loop out clears the finding rather than re-baselining it.
    ///     <para>
    ///         ⚠ A reference that is not a lock target fails the whole rule, it does not merely fail to
    ///         count: the point of walking the file is that a single non-lock use means the field is
    ///         not a dedicated monitor.
    ///     </para>
    /// </remarks>
    static bool EveryReferenceIsALockTarget(
        SyntaxNode root,
        SemanticModel model,
        IFieldSymbol field,
        CancellationToken cancellation
    ) {
        var count = 0;
        foreach (var name in root.DescendantNodes().OfType<IdentifierNameSyntax>()) {
            if (name.Identifier.ValueText != field.Name
                || model.GetSymbolInfo(name, cancellation).Symbol is not IFieldSymbol reference
                || !SymbolEqualityComparer.Default.Equals(field.OriginalDefinition, reference.OriginalDefinition)) {
                continue;
            }

            SyntaxNode expression = name;
            if (name.Parent is MemberAccessExpressionSyntax access && access.Name == name) {
                expression = access;
            }

            while (expression.Parent is ParenthesizedExpressionSyntax parentheses) {
                expression = parentheses;
            }

            if (expression.Parent is not LockStatementSyntax statement
                || statement.Expression != expression
                || statement.Statement.DescendantNodesAndSelf().OfType<YieldStatementSyntax>().Any()) {
                return false;
            }

            count++;
        }

        return count > 0;
    }

    /// <summary>
    ///     ⚠ <b><c>fix --safe</c> followed by <c>verify</c> has to be a fixpoint (#343).</b>
    /// </summary>
    /// <remarks>
    ///     The rewrite used to emit
    ///     <c>readonly global::System.Threading.Lock gate = new global::System.Threading.Lock();</c> —
    ///     correct, and immediately reported by <c>arrange --check</c> as
    ///     <c>
    /// SK0203 target-typed
    ///     new
    ///     </c>, on a file <c>fix</c> had just written. So the fix is now written in the shape
    ///     arrangement would leave it in: <c>new()</c> unconditionally, because the field's declared
    ///     type is what the creation constructs and no arrangement rule expands an implicit creation
    ///     back out; and the short name where the semantic model says it binds.
    ///     <para>
    ///         ⚠ <c>global::</c> is still the fallback rather than the default, and the lookup is what
    ///         decides. <c>Lock</c> is a plausible name for a type of one's own and for a
    ///         <c>using static</c> member; emitting the short form where it binds to something else is
    ///         a silent miscompile, and this codebase's own promise is that a fix never changes what
    ///         the file means.
    ///     </para>
    /// </remarks>
    static string TypeName(SemanticModel model, int position) {
        const string qualified = "global::System.Threading.Lock";
        var declared = model.Compilation.GetTypeByMetadataName("System.Threading.Lock");
        if (declared is null) {
            return qualified;
        }

        var visible = model.LookupNamespacesAndTypes(position, name: "Lock");
        return visible.Length == 1 && SymbolEqualityComparer.Default.Equals(visible[0], declared)
            ? "Lock"
            : qualified;
    }
}
