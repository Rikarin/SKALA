using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rikarin.Skala.Rules.TestQuality;
using System;
using System.Threading;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     The questions every <c>SK3xxx</c> rule has to answer about where an expression sits.
/// </summary>
/// <remarks>
///     ⚠ Shared rather than duplicated because the answers have to agree. Two async rules that disagree
///     about whether a position can hold an <c>await</c> produce two findings on one line, at most one
///     of which has a fix that compiles.
/// </remarks>
internal static class AsyncContext {
    /// <summary>
    ///     The nearest enclosing thing that owns an <c>async</c> keyword, or null when the walk leaves
    ///     the body without meeting one.
    /// </summary>
    /// <remarks>
    ///     ⚠ Lambdas and local functions are boundaries, not transparent. A <c>.Result</c> inside a
    ///     synchronous lambda passed to <c>Select</c> is not made awaitable by the enclosing method
    ///     being <c>async</c>, and rewriting it there is CS4034.
    /// </remarks>
    public static SyntaxNode? NearestAsyncOwner(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case SimpleLambdaExpressionSyntax:
                case ParenthesizedLambdaExpressionSyntax:
                case AnonymousMethodExpressionSyntax:
                case LocalFunctionStatementSyntax:
                case BaseMethodDeclarationSyntax:
                case AccessorDeclarationSyntax:
                    return current;
            }
        }

        return null;
    }

    /// <summary>Whether the nearest owner is already <c>async</c>, so an <c>await</c> is legal here.</summary>
    public static bool IsInsideAsyncBody(SyntaxNode node) => HasAsyncModifier(NearestAsyncOwner(node));

    public static bool HasAsyncModifier(SyntaxNode? owner) =>
        owner switch {
            MethodDeclarationSyntax method => Has(method.Modifiers),
            LocalFunctionStatementSyntax local => Has(local.Modifiers),
            AnonymousFunctionExpressionSyntax lambda => lambda.AsyncKeyword.RawKind != (int)SyntaxKind.None,
            _ => false
        };

    static bool Has(SyntaxTokenList modifiers) {
        foreach (var modifier in modifiers) {
            if (modifier.IsKind(SyntaxKind.AsyncKeyword)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ Positions where an <c>await</c> is illegal, wrong, or means something a rewrite cannot
    ///     preserve — so the whole finding is withheld rather than the fix alone.
    /// </summary>
    /// <remarks>
    ///     docs/plan/10: "A fixing tool that can break the build is a tool an agent will use to break
    ///     the build." A finding an agent cannot act on teaches it to ignore the tool, which costs more
    ///     than the finding was worth.
    /// </remarks>
    public static bool IsUnawaitablePosition(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            switch (current) {
                // `await` inside a lock body is CS1996, and holding a lock across a suspension is
                // the bug SK3008 is about rather than something to introduce here.
                case LockStatementSyntax:

                // A constructor, a finalizer and an operator cannot be `async` at all.
                case ConstructorDeclarationSyntax:
                case DestructorDeclarationSyntax:
                case OperatorDeclarationSyntax:
                case ConversionOperatorDeclarationSyntax:

                // An initializer runs outside any method body.
                case EqualsValueClauseSyntax when current.Parent is PropertyDeclarationSyntax
                    or VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax }:

                // `await` in a query clause is not supported by the language.
                case QueryExpressionSyntax:

                case UnsafeStatementSyntax:
                    return true;

                // Stop at the body boundary: anything above it is a different question.
                case BaseMethodDeclarationSyntax:
                case AccessorDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }

    /// <summary>Whether the expression sits inside an expression tree, where <c>await</c> does not compile.</summary>
    public static bool InsideExpressionTree(SemanticModel model, SyntaxNode node, CancellationToken cancellation) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            if (current is not AnonymousFunctionExpressionSyntax) {
                continue;
            }

            var converted = model.GetTypeInfo(current, cancellation).ConvertedType;
            for (var type = converted; type is not null; type = type.BaseType) {
                if (type.ToDisplayString().StartsWith("System.Linq.Expressions.Expression", StringComparison.Ordinal)) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether this is test code, where blocking on a task is deliberate and harmless.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             The attribute is on the method, and the blocking call is in the helper the test
    ///             methods share.
    ///         </b> <see cref="IsTestMethod" /> answers about the <em>enclosing method</em>, so on
    ///         Skala's own tree it exempted all 346 callers of <c>RuleFixtures.Analyze</c> and missed the
    ///         one method they funnel through — the method that actually blocks ([#319]). The same walk
    ///         also declines a fixture constructor, an <c>IDisposable.Dispose</c> teardown and a field
    ///         initializer inside a real test class, none of which carries an attribute of its own.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>The question is asked of the type, which is [#303]'s rule and not a new one.</b>
    ///         <see cref="TestFrameworks.HoldsATestCase" /> is xUnit's own discovery rule — a class is a
    ///         test class when it holds a test case — and it is decidable from attributes alone. Every
    ///         member of such a class is test code, which is exactly what the constructor, the teardown
    ///         and the initializer needed.
    ///     </para>
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             What this deliberately does <em>not</em> do is recognise a helper in a separate
    ///             class, and that refuses [#319]'s own proposed remedy.
    ///         </b> #319 asked for "a non-public
    ///         helper declared in a test project". Neither half survives contact: <c>RuleFixtures</c> is
    ///         a <c>public static class</c> and <c>Analyze</c> is <c>public static</c>, so an
    ///         accessibility test would have left the finding exactly where it was — and "declared in a
    ///         test project" is the compilation-references question that #303 examined and refused, with
    ///         <c>SK2160/positive/a-helper-class-holding-no-test-case.cs</c> pinning the refusal. ⚠ It
    ///         was refused for a measured reason and not a stylistic one: the fixture harness hands every
    ///         fixture the test host's whole assembly closure, so "the compilation references xunit" is
    ///         true of every fixture in the corpus. Wiring it in turned <b>31 positive fixtures</b>
    ///         across <c>SK3002</c>, <c>SK3004</c>, <c>SK3050</c>, <c>SK3051</c>, <c>SK5020</c> and
    ///         <c>SK5021</c> silent in one run — six rules that would have passed their entire negative
    ///         sets while switched off.
    ///     </para>
    ///     <para>
    ///         ⚠ So the <c>SK3002</c> finding on <c>RuleFixtures.Analyze</c> stands, and baselining it is
    ///         the honest outcome rather than a workaround. Reaching it needs the call graph #319 rules
    ///         out, or the reference sniffing #303 already decided against.
    ///     </para>
    ///     <para>
    ///         ⚠ Asked only once a rule has a genuine candidate — every caller binds first — so the
    ///         symbol work runs about as often as a finding is reported, not per node.
    ///     </para>
    /// </remarks>
    public static bool IsTestCode(SyntaxNode node, SemanticModel? model, CancellationToken cancellation) {
        if (IsTestMethod(node)) {
            return true;
        }

        if (model is null || node.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } declaration) {
            return false;
        }

        return model.GetDeclaredSymbol(declaration, cancellation) is INamedTypeSymbol type
            && TestFrameworks.HoldsATestCase(type, TestFrameworks.Resolve(model.Compilation));
    }

    /// <summary>
    ///     Whether the nearest enclosing method carries a test framework's attribute.
    /// </summary>
    /// <remarks>
    ///     ⚠ By attribute rather than by file path. docs/plan/08 scopes the <c>SK8xxx</c> rules to test
    ///     projects "by convention (<c>*.Tests</c>) and by <c>.editorconfig</c> section", and that is
    ///     the right mechanism for a whole category — but a single rule staying silent needs to be
    ///     right in a repository whose tests live somewhere the convention does not name, and the
    ///     attribute is on the method either way.
    ///     <para>
    ///         ⚠ It answers about the <em>method</em> and nothing else, which is a narrower question
    ///         than most callers want: a fixture constructor, an <c>IDisposable.Dispose</c> teardown, a
    ///         field initializer and a shared helper are all test code and none of them carries the
    ///         attribute. <see cref="IsTestCode" /> is the question to ask unless the method really is
    ///         the unit.
    ///     </para>
    /// </remarks>
    public static bool IsTestMethod(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            if (current is not MethodDeclarationSyntax method) {
                continue;
            }

            foreach (var list in method.AttributeLists) {
                foreach (var attribute in list.Attributes) {
                    var name = attribute.Name switch {
                        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
                        SimpleNameSyntax simple => simple.Identifier.ValueText,
                        _ => string.Empty
                    };

                    switch (name) {
                        case "Fact":
                        case "FactAttribute":
                        case "Theory":
                        case "TheoryAttribute":
                        case "Test":
                        case "TestAttribute":
                        case "TestCase":
                        case "TestCaseAttribute":
                        case "TestMethod":
                        case "TestMethodAttribute":
                        case "Benchmark":
                        case "BenchmarkAttribute":
                            return true;
                    }
                }
            }

            return false;
        }

        return false;
    }

    /// <summary>
    ///     Whether replacing <paramref name="node" /> with a bare <c>await x</c> keeps the parse.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the difference between a fix and a bug. <c>x.Result.Length</c> is
    ///     <c>(await x).Length</c>; written as <c>await x.Length</c> it awaits the wrong thing, and on
    ///     a type where both compile it silently changes what the expression means.
    /// </remarks>
    public static bool NeedsParentheses(SyntaxNode node) =>
        node.Parent switch {
            ExpressionStatementSyntax => false,
            EqualsValueClauseSyntax => false,
            ReturnStatementSyntax => false,
            ArrowExpressionClauseSyntax => false,
            ArgumentSyntax => false,
            AssignmentExpressionSyntax assignment => !ReferenceEquals(assignment.Right, node),
            _ => true
        };
}
