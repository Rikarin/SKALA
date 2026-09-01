using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.TestQuality;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Design;

/// <summary>
///     <c>SK6053</c> — the method's name does not say whether the caller has to await it.
/// </summary>
/// <remarks>
///     The <c>Async</c> suffix is how a caller knows a result must be awaited, and it is what makes a
///     missing <c>await</c> visible in review rather than at run time. A <c>Task</c>-returning method
///     without it is the shape that produces the fire-and-forget bug <c>SK3005</c> reports.
///     <para>
///         ⚠
///         <b>
///             This is a naming convention, which is the most opinionated thing a linter can hold an
///             opinion about, and it ships <c>defaultSeverity: none</c> for that reason.
///         </b> The
///         severity was measured, and the measurement that decided it was not the one expected: Skala's
///         own tree contains <b>five</b> methods this rule governs at all, four of them already
///         suffixed. A population of five cannot calibrate a naming convention, and a low count on a
///         reference tree is evidence about that tree rather than about the rule. See the rule's
///         <c>falsePositives</c> in <c>rules.json</c> for the full record. It is opt-in the way
///         <c>SK7010</c> and <c>SK7101</c> are: a repository that has adopted the convention turns it on
///         per path and gets it enforced, and one that has not is not told its whole test suite is
///         wrong.
///     </para>
///     <para>
///         ⚠ <b>No fix, and that refutes what the proposal asked for.</b> The repair is a rename, and a
///         rename touches every call site. ADR-005 makes a Skala fix a minimal text-edit list against
///         one file's original <c>SourceText</c>, so an edit that renamed the declaration and nothing
///         else would break the build on the tool's advice — which is the one thing a fix may never do.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncSuffixAnalyzer : DiagnosticAnalyzer {
    const string Suffix = "Async";

    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.AsyncSuffixConvention);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var vocabulary = Vocabulary.Resolve(start.Compilation);
                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, vocabulary),
                    SyntaxKind.MethodDeclaration
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, Vocabulary vocabulary) {
        var declaration = (MethodDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not {
                MethodKind: MethodKind.Ordinary
            } method) {
            return;
        }

        var carriesSuffix = method.Name.EndsWith(Suffix, StringComparison.Ordinal);
        var isAsynchronous = vocabulary.IsAsynchronous(method.ReturnType) || method.IsAsync;
        if (carriesSuffix == isAsynchronous || vocabulary.NamedByConvention(method)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declaration.Identifier.GetLocation(),
                isAsynchronous
                    ? "`"
                    + method.Name
                    + "` returns an awaitable and its name does not say so, which is what makes a "
                    + "missing `await` invisible in review"
                    : "`" + method.Name + "` is named as if it were asynchronous and nothing about it is"
            )
        );
    }

    /// <summary>
    ///     The types and attributes that decide the question, resolved once per compilation.
    /// </summary>
    /// <remarks>
    ///     ⚠ Resolution is by metadata name against the compilation, which finds a framework referenced
    ///     as an assembly and a framework declared in source alike — the second is what a fixture is.
    ///     ASP.NET is not on the fixture harness's reference set, so a rule that additionally demanded
    ///     the symbol come from metadata would be a rule whose ASP.NET fixtures proved nothing. The same
    ///     reasoning <c>TestFrameworks</c> is written with.
    /// </remarks>
    sealed class Vocabulary {
        readonly ImmutableArray<INamedTypeSymbol> awaitables;
        readonly ImmutableArray<INamedTypeSymbol> webHosts;
        readonly INamedTypeSymbol? apiController;
        readonly INamedTypeSymbol? httpMethod;
        readonly TestFrameworks tests;

        Vocabulary(
            ImmutableArray<INamedTypeSymbol> awaitables,
            ImmutableArray<INamedTypeSymbol> webHosts,
            INamedTypeSymbol? apiController,
            INamedTypeSymbol? httpMethod,
            TestFrameworks tests
        ) {
            this.awaitables = awaitables;
            this.webHosts = webHosts;
            this.apiController = apiController;
            this.httpMethod = httpMethod;
            this.tests = tests;
        }

        public static Vocabulary Resolve(Compilation compilation) {
            var awaitables = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
            foreach (var name in new[] {
                         "System.Threading.Tasks.Task", "System.Threading.Tasks.Task`1",
                         "System.Threading.Tasks.ValueTask", "System.Threading.Tasks.ValueTask`1",
                         "System.Collections.Generic.IAsyncEnumerable`1"
                     }) {
                if (compilation.GetTypeByMetadataName(name) is { } type) {
                    awaitables.Add(type);
                }
            }

            var hosts = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
            foreach (var name in new[] {
                         "Microsoft.AspNetCore.Mvc.ControllerBase", "Microsoft.AspNetCore.Mvc.Controller",
                         "Microsoft.AspNetCore.Mvc.RazorPages.PageModel", "Microsoft.AspNetCore.SignalR.Hub"
                     }) {
                if (compilation.GetTypeByMetadataName(name) is { } type) {
                    hosts.Add(type);
                }
            }

            return new(
                awaitables.ToImmutable(),
                hosts.ToImmutable(),
                compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ApiControllerAttribute"),
                compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute"),
                TestFrameworks.Resolve(compilation)
            );
        }

        /// <summary>Whether a caller has to await what this type carries.</summary>
        /// <remarks>
        ///     ⚠ A custom awaitable counts, and it is recognised by having a <c>GetAwaiter</c> rather than
        ///     by being on the list. Without that, a method returning somebody's own awaitable and
        ///     correctly named <c>…Async</c> would be reported for carrying a suffix it has earned.
        /// </remarks>
        public bool IsAsynchronous(ITypeSymbol type) {
            foreach (var awaitable in awaitables) {
                if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, awaitable)) {
                    return true;
                }
            }

            foreach (var member in type.GetMembers("GetAwaiter")) {
                if (member is IMethodSymbol { Parameters.Length: 0 }) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Whether something other than this rule already decides what the method is called.
        /// </summary>
        /// <remarks>
        ///     ⚠
        ///     <b>
        ///         These are not edge cases; on most real trees they are the majority of the
        ///         <c>Task</c>-returning methods that carry no suffix.
        ///     </b> An <c>override</c> and an interface
        ///     implementation take their name from the declaration they satisfy, so the finding would be
        ///     made in the wrong file. An ASP.NET action, a Razor page handler and a SignalR hub method
        ///     are named by a routing convention that would break if the suffix were added. A test method
        ///     is named after what it asserts, and a suite that renamed every one of them would be worse
        ///     to read, not better. <c>Main</c> is spelled by the language.
        /// </remarks>
        public bool NamedByConvention(IMethodSymbol method) {
            if (method.IsOverride
                || method.ExplicitInterfaceImplementations.Length != 0
                || method is { Name: "Main", IsStatic: true }
                || TestFrameworks.Carries(method, tests.TestMethodAttributes)
                || TestFrameworks.Carries(method, tests.LifecycleAttributes)) {
                return true;
            }

            return ImplementsAnInterface(method) || IsWebFacing(method);
        }

        static bool ImplementsAnInterface(IMethodSymbol method) {
            var type = method.ContainingType;
            if (type is null) {
                return false;
            }

            foreach (var contract in type.AllInterfaces) {
                foreach (var member in contract.GetMembers(method.Name)) {
                    if (member is IMethodSymbol
                        && SymbolEqualityComparer.Default.Equals(
                            type.FindImplementationForInterfaceMember(member),
                            method
                        )) {
                        return true;
                    }
                }
            }

            return false;
        }

        bool IsWebFacing(IMethodSymbol method) {
            var type = method.ContainingType;
            if (type is null) {
                return false;
            }

            if (TestFrameworks.Carries(type, apiController) || TestFrameworks.Carries(method, httpMethod)) {
                return true;
            }

            for (var current = type.BaseType; current is not null; current = current.BaseType) {
                foreach (var host in webHosts) {
                    if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, host)) {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
