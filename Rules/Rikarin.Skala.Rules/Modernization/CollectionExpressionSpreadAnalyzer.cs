using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1133</c> — <c>int[] a = xs.ToArray();</c> and <c>List&lt;int&gt; b = xs.ToList();</c> are
///     <c>[..xs]</c>.
/// </summary>
/// <remarks>
///     <para>
///         ⚠
///         <b>
///             The fix is safe because the compiler emits the very call it replaces, and that is a fact
///             about the compiler, not about the language.
///         </b> Measured by decompiling and by running both forms: from Roslyn 4.14 on, a collection
///         expression holding one spread lowers to <c>Enumerable.ToArray</c>, <c>Enumerable.ToList</c>,
///         <c>List&lt;T&gt;.ToArray</c> or <c>Span&lt;T&gt;.ToArray</c> — the same method, so a null receiver
///         throws the same exception and an empty one returns the same instance. Roslyn 4.8, which
///         introduced C# 12, did not: <c>ArgumentNullException</c> became <c>NullReferenceException</c> and
///         <c>Array.Empty&lt;T&gt;()</c> became a fresh array. Roslyn 4.11 turned
///         <c>[.. (int[])null]</c> into an <em>empty array</em>. So the floor is C# 14, which no compiler
///         before 5.0 accepts, and the compiler must be <em>proved</em> — <c>latest</c> and <c>preview</c>
///         mean whatever the compiler that builds the project says, not what Skala's own compiler says.
///         Two things prove it: a version written as a number, or (#515) a <c>net10.0</c> or later
///         reference set, which no SDK before 10 can build.
///     </para>
///     <para>
///         ⚠ The lowering picks its method by the receiver's static type, so only the receivers whose
///         lowering was measured are taken: a reference type through <c>Enumerable</c>, exactly
///         <c>List&lt;T&gt;</c> through its own <c>ToArray</c>, and a span. A type deriving from
///         <c>List&lt;T&gt;</c> lowers to <c>Enumerable.ToArray</c> where the source called the list's own
///         method, a type parameter is enumerated into a fresh list, and a struct such as
///         <c>ImmutableArray&lt;T&gt;</c> is copied through its span.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CollectionExpressionSpreadAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.CollectionExpressionSpread);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CollectionExpressionSpread);

    /// <summary>
    ///     The first Roslyn whose lowering of a one-spread collection expression is the call it replaces.
    /// </summary>
    static readonly System.Version LoweringCompiler = new(4, 14);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        var registrar = PartialConstructorDefinitions.Visiting(context);
        registrar.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                var framework = new Framework(
                    start.Compilation.GetTypeByMetadataName("System.Linq.Enumerable"),
                    start.Compilation.GetTypeByMetadataName("System.Collections.Generic.List`1"),
                    start.Compilation.GetTypeByMetadataName("System.Span`1"),
                    start.Compilation.GetTypeByMetadataName("System.ReadOnlySpan`1"),
                    // ⚠ #517: the net10.0 proof is about the SDK's compiler, and a pinned
                    // `Microsoft.Net.Compilers.Toolset` replaces it with no version check. Where the
                    // binlog names one older than 4.14, the reference set proves nothing.
                    ReferencesDotNet10(start.Compilation)
                    && !CompilerIdentity.IsToolsetOlderThan(start.Options, LoweringCompiler),
                    // ⚠ #515: a `netstandard2.1;net10.0` project at `latest` proves its compiler in the
                    // `net10.0` leg and in no other, and the fix lands in a file both legs compile. The
                    // proof is asked of every sibling, as #351's rules ask their whole condition.
                    FrameworkAvailability.PathsWithout(start.Options, ProvesTheLowering)
                );
                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, framework),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    sealed record Framework(
        INamedTypeSymbol? Enumerable,
        INamedTypeSymbol? List,
        INamedTypeSymbol? Span,
        INamedTypeSymbol? ReadOnlySpan,
        bool TargetsDotNet10,
        ImmutableHashSet<string> Unproved);

    static bool WritesASpaceAfterTheSpread(SyntaxNodeAnalysisContext context) =>
        context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree)
            .TryGetValue("skala_space_within_spread_pattern", out var value)
        && string.Equals(value.Trim(), "true", System.StringComparison.OrdinalIgnoreCase);

    static void Analyze(SyntaxNodeAnalysisContext context, Framework framework) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } access
            || access.Name.Identifier.ValueText is not ("ToArray" or "ToList")
            || !(framework.TargetsDotNet10 || IsWrittenLanguageVersion(invocation.SyntaxTree))
            || framework.Unproved.Contains(invocation.SyntaxTree.FilePath)
            || WrittenTargetOf(invocation) is not { } target) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol method
            || model.GetTypeInfo(access.Expression, cancellation).Type is not { } receiver
            || !LowersToTheSameCall(method, receiver, framework)) {
            return;
        }

        // ⚠ The one comparison `SK1001` makes. A collection expression builds an object of its target
        // type; the call built one of its return type. Only where Roslyn says the two are the same type
        // is the rewrite free of choice: `IEnumerable<int> e = xs.ToArray();` would let the compiler pick
        // any implementation it likes, and `e is int[]` would stop holding.
        var info = model.GetTypeInfo(invocation, cancellation);
        if (info.Type is not { TypeKind: not TypeKind.Error } created
            || !SymbolEqualityComparer.Default.Equals(created, info.ConvertedType)) {
            return;
        }

        // ⚠ `SK1081` owns a materializer whose receiver is another one: `xs.ToList().ToArray()` copies a
        // copy, and its fix deletes the inner call. Rewriting the outer one first would hide that inside
        // a spread, where `SK1081` no longer looks. One defect, one finding; this rule fires on the
        // shortened call on the next pass.
        if (IsMaterializer(Unparenthesized(access.Expression), model, framework, cancellation)) {
            return;
        }

        // ⚠ A collection expression is not an expression tree node (CS9175).
        if (NullComparison.InsideExpressionTree(model, invocation, cancellation)) {
            return;
        }

        if (Operand(access.Expression) is not { } operand) {
            return;
        }

        // Everything outside the operand is deleted: the `.ToArray()` and any parentheses the operand
        // no longer needs. A comment or a directive there would go with it.
        var tree = invocation.SyntaxTree;
        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(
                tree,
                TextSpan.FromBounds(invocation.SpanStart, operand.SpanStart)
            )
            || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(
                tree,
                TextSpan.FromBounds(operand.Span.End, invocation.Span.End)
            )) {
            return;
        }

        // ⚠ The configured spelling of the spread's gap (#513, SK-DIV-0310): the formatter governs it
        // since then, so a fix that wrote the other one would leave `format --check` failing on the
        // line it just fixed. `false` — `[..xs]` — is the registry's default and applies when unset.
        var replacement = (WritesASpaceAfterTheSpread(context) ? "[.. " : "[..") + operand + "]";

        // ⚠ #425: the target is typed by the position, and an argument's position is chosen by overload
        // resolution — which a collection expression can steer elsewhere, because it converts to every
        // collection type at once. `M(int[])` + `M(List<int>)` is ambiguous for `[..xs]`, and
        // `M(object[])` + `M(int[])` would move. The rewrite is bound where it stands and must land where
        // the call landed, with every enclosing expression binding as it did.
        if (!FixRebind.TrySpeculate(
                model,
                invocation,
                SyntaxFactory.ParseExpression(replacement),
                out var speculative,
                out var placed
            )
            || !FixRebind.BindsTheSurroundingsAlike(model, invocation, speculative, placed, cancellation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                FixEdits.Pack((invocation.Span, replacement)),
                "The type is already written at "
                + target
                + ", so the copy is a collection expression: `"
                + RewriteGuards.Trim(replacement)
                + "`"
            )
        );
    }

    /// <summary>
    ///     ⚠ Whether the file's language version is a number someone wrote, and at least the floor.
    /// </summary>
    /// <remarks>
    ///     <c>latest</c>, <c>latestMajor</c>, <c>default</c> and <c>preview</c> are resolved by the
    ///     compiler, and the compiler Skala loads the project with is not necessarily the one that builds
    ///     it: a project pinned to SDK 8 with <c>latest</c> reads as C# 14 here and compiles as C# 12 with
    ///     Roslyn 4.8, where the rewrite changes behaviour. A written <c>14</c> is refused by every
    ///     compiler before 5.0 (4.11 and 4.14 measured), so it proves which lowering the build gets. The
    ///     other proof is <see cref="ReferencesDotNet10" />.
    /// </remarks>
    static bool IsWrittenLanguageVersion(SyntaxTree tree) =>
        tree.Options is CSharpParseOptions { SpecifiedLanguageVersion: var written }
        && written != LanguageVersion.Preview
        && written.MapSpecifiedToEffectiveVersion() == written;

    /// <summary>
    ///     ⚠ #515: whether the compilation's core library is .NET 10's reference assembly or a later one's,
    ///     which proves the compiler however the language version is spelled.
    /// </summary>
    /// <remarks>
    ///     A <c>net10.0</c> target is refused by every SDK before 10.0.100 (<c>NETSDK1045</c>), and SDK 10
    ///     ships Roslyn 5 — past the 4.14 the lowering needs. Measured on SDK 10.0.401 by replaying each
    ///     probe's recorded <c>csc</c> line: a <c>net10.0</c> unit's <c>System.Object</c> is
    ///     <c>System.Runtime, Version=10.0.0.0</c> from the targeting pack at <c>latest</c>,
    ///     <c>latestMajor</c>, <c>default</c>, <c>preview</c> and with nothing written, and still is when
    ///     the project references a <c>netstandard2.0</c> library (that library is its own unit, whose
    ///     corlib is <c>netstandard 2.0.0.0</c>). <c>net9.0</c> is <c>System.Runtime 9.0.0.0</c>,
    ///     <c>netstandard2.1</c> is <c>netstandard 2.1.0.0</c> and <c>net48</c> is <c>mscorlib 4.0.0.0</c>,
    ///     and none of them qualifies.
    ///     <para>
    ///         ⚠ <b>The reference assembly, by name — never <c>System.Private.CoreLib</c>.</b> A build
    ///         compiles against the targeting pack; an implementation corlib is what a compilation has
    ///         when nothing was built at all — <c>--load=loose</c> and the test host both reference the
    ///         running runtime's own assemblies, and the version of the runtime Skala happens to run on
    ///         says nothing about the compiler that will build the file.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>Not airtight, and the hole is stated rather than hidden.</b> A
    ///         <c>Microsoft.Net.Compilers.Toolset</c> package replaces the SDK's compiler without a version
    ///         check (SDK 10.0.401's targets have none), and Roslyn 4.8 and 4.11 both compile and emit a
    ///         <c>net10.0</c> probe from its real <c>csc</c> line with no diagnostic, resolving
    ///         <c>latest</c> to C# 12. The generated <c>build_property.*</c> set names no compiler; the
    ///         binlog's <c>csc</c> path does, and since #517 it reaches the rule
    ///         (<see cref="CompilerIdentity" />), which declines under a toolset package older than 4.14.
    ///         ⚠ So the hole is closed under <c>--load=binlog</c> only: a workspace load has no compiler
    ///         path, and such a build is still reported there (loose does not run the rule at all). A
    ///         written <c>14</c> has no hole in any mode: every compiler before 5.0 refuses it.
    ///     </para>
    /// </remarks>
    static bool ReferencesDotNet10(Compilation compilation) =>
        compilation.ObjectType.ContainingAssembly.Identity is { Name: "System.Runtime", Version.Major: >= 10 };

    /// <summary>
    ///     The whole compiler proof for one compilation, asked of the project's other target frameworks.
    /// </summary>
    /// <remarks>
    ///     ⚠ The language floor is part of it: a sibling at C# 8 cannot compile <c>[..xs]</c> at all.
    ///     <c>MultiTargetLanguageFloor</c> withholds that case centrally as well, but only from
    ///     <c>skala check</c>'s merge; a host that publishes siblings and nothing else gets it here.
    /// </remarks>
    static bool ProvesTheLowering(Compilation compilation) =>
        SkalaRule.MeetsLanguageVersion(compilation, Rule.LanguageVersion)
        && (ReferencesDotNet10(compilation) || compilation.SyntaxTrees.All(IsWrittenLanguageVersion));

    /// <summary>
    ///     Whether the compiler lowers <c>[..receiver]</c> at this call's own type to this very method.
    /// </summary>
    static bool LowersToTheSameCall(IMethodSymbol method, ITypeSymbol receiver, Framework framework) {
        if (receiver.TypeKind == TypeKind.Error) {
            return false;
        }

        // `Enumerable.ToArray(source)` / `ToList(source)`, written as an extension with nothing else.
        if (method.ReducedFrom is { Parameters.Length: 1 } reduced) {
            if (!SymbolEqualityComparer.Default.Equals(reduced.ContainingType, framework.Enumerable)
                || method.TypeArguments.Length != 1) {
                return false;
            }

            // ⚠ A struct is enumerated into a fresh `List<T>` and copied out rather than boxed into
            // `Enumerable.ToArray`, so an empty one's copy is a new array, not `Array.Empty<T>()`
            // (decompiled; `ImmutableArray<T>` is copied through its span instead).
            if (receiver.IsValueType) {
                return false;
            }

            // ⚠ The element type is the call's own `T`, exactly. `strings.ToList<object>()` on a
            // `List<string>` lowers to a counted copy loop, not to `ToList`; and a widening is a
            // different list either way. It is also what declines a type parameter, which the compiler
            // enumerates into a list unless it is constrained to a class: `ElementTypeOf` answers null
            // for one.
            return SymbolEqualityComparer.Default.Equals(
                RedundantSpreadElementAnalyzer.ElementTypeOf(receiver),
                method.TypeArguments[0]
            );
        }

        if (method.IsStatic || method.Name != "ToArray" || method.Parameters.Length != 0) {
            return false;
        }

        var declaring = method.ContainingType.OriginalDefinition;

        // ⚠ Exactly `List<T>`: a class deriving from it lowers to `Enumerable.ToArray`, which on a null
        // receiver throws `ArgumentNullException` where the list's own method threw
        // `NullReferenceException`.
        if (SymbolEqualityComparer.Default.Equals(declaring, framework.List)) {
            return SymbolEqualityComparer.Default.Equals(receiver.OriginalDefinition, framework.List);
        }

        return SymbolEqualityComparer.Default.Equals(declaring, framework.Span)
            || SymbolEqualityComparer.Default.Equals(declaring, framework.ReadOnlySpan);
    }

    static bool IsMaterializer(
        ExpressionSyntax expression,
        SemanticModel model,
        Framework framework,
        CancellationToken cancellation
    ) {
        if (expression is not InvocationExpressionSyntax
            || model.GetSymbolInfo(expression, cancellation).Symbol is not IMethodSymbol method
            || method.Name is not ("ToArray" or "ToList")) {
            return false;
        }

        var declaring = (method.ReducedFrom ?? method).ContainingType.OriginalDefinition;
        return SymbolEqualityComparer.Default.Equals(declaring, framework.Enumerable)
            || SymbolEqualityComparer.Default.Equals(declaring, framework.List);
    }

    /// <summary>
    ///     The receiver as the spread's operand, without its parentheses, or null when that does not read
    ///     back as the same expression.
    /// </summary>
    /// <remarks>
    ///     ⚠ Asked of the parser rather than of a precedence table: <c>[..</c> followed by the text is
    ///     parsed, and the operand must come back as the same tree with nothing reported. A spread's
    ///     operand is parsed as a whole expression — the same context a parenthesized expression's inner
    ///     one is parsed in — so no receiver measured needed its parentheses back: a cast, <c>??</c>,
    ///     <c>?:</c>, an assignment and a query expression all read back bare. Rather than carry a
    ///     "keep the parentheses" branch nothing can reach, a receiver that did not read back would be
    ///     declined.
    /// </remarks>
    static ExpressionSyntax? Operand(ExpressionSyntax receiver) {
        var operand = Unparenthesized(receiver);
        return ReadsBack(operand) ? operand : null;
    }

    static bool ReadsBack(ExpressionSyntax operand) {
        var parsed = SyntaxFactory.ParseExpression("[.." + operand + "]");
        return !parsed.ContainsDiagnostics
            && parsed is CollectionExpressionSyntax { Elements.Count: 1 } collection
            && collection.Elements[0] is SpreadElementSyntax spread
            && spread.Expression.IsEquivalentTo(operand, false);
    }

    static ExpressionSyntax Unparenthesized(ExpressionSyntax expression) {
        while (expression is ParenthesizedExpressionSyntax parenthesized) {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    /// <summary>
    ///     Where the target type is written down, or null when it is not written anywhere.
    /// </summary>
    /// <remarks>
    ///     ⚠ A collection expression has no natural type. <c>var a = [..xs];</c> and
    ///     <c>foreach (var x in [..xs])</c> are CS9176; a lambda's return type, a generic method's
    ///     <c>T</c> and a conditional's type are all inferred, the last from the other branch — so
    ///     rewriting both branches of one <c>?:</c>, each fine alone, stops compiling. Only the positions
    ///     <c>SK1001</c> takes are taken, plus an argument, whose overload is proved by rebinding.
    /// </remarks>
    static string? WrittenTargetOf(ExpressionSyntax value) {
        switch (value.Parent) {
            case EqualsValueClauseSyntax {
                Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration }
            }:
                // `var` is not refused here: `var a = [..xs];` is CS9176 and the rebinding refuses it.
                return declaration.Parent is LocalDeclarationStatementSyntax or FieldDeclarationSyntax
                    ? "the declaration"
                    : null;

            case EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax }:
                return "the property";

            case ReturnStatementSyntax statement:
                return CollectionExpressionAnalyzer.HasWrittenReturnType(statement) ? "the return type" : null;

            // A `void` member's arrow discards the value, and `[..xs]` is not a statement expression;
            // the rebinding refuses it, as it refuses `var`.
            case ArrowExpressionClauseSyntax arrow:
                return CollectionExpressionAnalyzer.HasWrittenReturnType(arrow) ? "the return type" : null;

            case AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment
                when ReferenceEquals(assignment.Right, value) && RewriteGuards.IsPlainNamePath(assignment.Left):
                return "the assignment target";

            case ArgumentSyntax { Parent: ArgumentListSyntax }:
                return "the parameter";

            default:
                return null;
        }
    }
}
