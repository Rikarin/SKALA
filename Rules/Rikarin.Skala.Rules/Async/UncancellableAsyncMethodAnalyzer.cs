using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3051</c> — an <c>async</c> method with no <c>CancellationToken</c> to forward.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000". <c>SK3004</c> reports a token accepted and not passed
///     on. This reports the step before it: the method accepts none, so there is nothing to pass on and
///     no caller can stop the work. The two are the same argument at two points in the call graph, and
///     applying this rule's fix is what makes <c>SK3004</c> reachable on the same body.
///     <para>
///         ⚠ <b>The two are disjoint by construction, and the construction is the count.</b>
///         <c>SK3004</c> fires only where <em>exactly one</em> token is in scope; this fires only where
///         there are <em>none</em>. No body can satisfy both, which is why neither declares
///         <c>supersedes</c> — and why the pair cannot double-report on one line.
///     </para>
///     <para>
///         ⚠ <b>The finding needs a call that would have taken a token</b>, and that is the exemption
///         the issue asked for. "Every async method should accept a token" fires on every leaf in every
///         codebase and is advice rather than a finding; "this method calls something that offers a
///         token and has none to give it" names a concrete cancellation that is broken, and the callee
///         is in the message.
///     </para>
///     <para>
///         ⚠
///         <b>
///             This rule is <see cref="RuleScope.Compilation" />-scoped for the same reason
///             <c>SK3001</c> is, and it is the fix that costs it.
///         </b> Appending a parameter — even an
///         optional one — breaks a method group conversion: <c>Func&lt;Task&gt; f = LoadAsync;</c> is
///         CS0123 the moment <c>LoadAsync</c> gains a parameter, because optional parameters do not
///         participate in delegate conversion. Whether a method is used that way is not visible in the
///         file that declares it, so the rule collects every name used as a method group across the
///         compilation and reports only the methods no such use names. The price is that
///         <see cref="RuleInfo.IsCacheable" /> is false for <c>SK3051</c>.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UncancellableAsyncMethodAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.AsyncMethodWithoutCancellation);

    static readonly string[] TaskTypes = [
        "System.Threading.Tasks.Task", "System.Threading.Tasks.Task`1", "System.Threading.Tasks.ValueTask",
        "System.Threading.Tasks.ValueTask`1"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var tokenType = start.Compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
                if (tokenType is null) {
                    return;
                }

                var tasks = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                foreach (var name in TaskTypes) {
                    if (start.Compilation.GetTypeByMetadataName(name) is { } type) {
                        tasks.Add(type);
                    }
                }

                if (tasks.Count == 0) {
                    return;
                }

                var candidates = new ConcurrentBag<Candidate>();

                // ⚠ Every identifier that is *not* the callee of an invocation — the same set, and the
                // same reasoning, as SK3001's. See the type's remarks for what it is guarding.
                var referenced = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

                start.RegisterSyntaxNodeAction(
                    context => AsyncSignature.RecordReference(context, referenced),
                    SyntaxKind.IdentifierName
                );

                start.RegisterSyntaxNodeAction(
                    context => Collect(context, candidates, tokenType, tasks),
                    SyntaxKind.MethodDeclaration
                );

                start.RegisterCompilationEndAction(context => {
                        foreach (var candidate in candidates) {
                            if (referenced.ContainsKey(candidate.Name)) {
                                continue;
                            }

                            context.ReportDiagnostic(
                                Diagnostic.Create(
                                    Descriptor,
                                    candidate.Location,
                                    candidate.Fix,
                                    "`"
                                    + candidate.Name
                                    + "` calls `"
                                    + candidate.Callee
                                    + "`, which takes a `CancellationToken`, and accepts none to give it"
                                )
                            );
                        }
                    }
                );
            }
        );
    }

    /// <summary>An <c>async</c> method that survived every local guard, pending the name check.</summary>
    /// <remarks>
    ///     ⚠ A class rather than a positional record: this assembly is <c>netstandard2.0</c> (ADR-006)
    ///     and <c>init</c> accessors need an <c>IsExternalInit</c> the target framework does not carry.
    /// </remarks>
    sealed class Candidate {
        public Candidate(string name, string callee, Location location, ImmutableDictionary<string, string?> fix) {
            Name = name;
            Callee = callee;
            Location = location;
            Fix = fix;
        }

        public string Name { get; }

        public string Callee { get; }

        public Location Location { get; }

        public ImmutableDictionary<string, string?> Fix { get; }
    }

    static void Collect(
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<Candidate> candidates,
        INamedTypeSymbol tokenType,
        HashSet<INamedTypeSymbol> tasks
    ) {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!IsAsync(method) || method.Body is null && method.ExpressionBody is null) {
            return;
        }

        // ⚠ Each of these is a shape where the parameter list is somebody else's contract rather than
        // this author's choice, so appending to it is not a repair the author can take. `virtual` and
        // `new` sit beside `override` for the reason SK3001's fixture set found: a virtual method is
        // the *top* of a dispatch chain, so its signature binds every override in every assembly that
        // derives from it.
        foreach (var modifier in method.Modifiers) {
            if (modifier.IsKind(SyntaxKind.OverrideKeyword)
                || modifier.IsKind(SyntaxKind.VirtualKeyword)
                || modifier.IsKind(SyntaxKind.AbstractKeyword)
                || modifier.IsKind(SyntaxKind.NewKeyword)
                || modifier.IsKind(SyntaxKind.PartialKeyword)
                || modifier.IsKind(SyntaxKind.ExternKeyword)) {
                return;
            }
        }

        // An attribute is nearly always a framework binding the signature is part of — a controller
        // action, a message handler, a benchmark — and `async Task Main` is the entry point, whose
        // signature the runtime chooses.
        if (method.AttributeLists.Count > 0
            || string.Equals(method.Identifier.ValueText, "Main", StringComparison.Ordinal)
            || AsyncContext.IsTestCode(method, context.SemanticModel, context.CancellationToken)) {
            return;
        }

        // ⚠ CS0231: an optional parameter cannot follow a `params` one, so there is no edit here.
        // A parameter already called `cancellationToken` would be CS0100 whatever its type is.
        foreach (var parameter in method.ParameterList.Parameters) {
            if (string.Equals(parameter.Identifier.ValueText, "cancellationToken", StringComparison.Ordinal)) {
                return;
            }

            foreach (var modifier in parameter.Modifiers) {
                if (modifier.IsKind(SyntaxKind.ParamsKeyword)) {
                    return;
                }
            }
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // ⚠ There is no method-level "declares no CancellationToken" check here, and its absence is
        // measured rather than assumed. The draft carried one; sabotaging it turned nothing red,
        // because `Evidence` already requires *zero* tokens in scope at the call — and the scopes
        // enclosing a call inside this body are a superset of the ones enclosing the declaration, so
        // a body with a token parameter can never produce an invocation with none. The check was dead
        // code no sabotage could kill. `negative/the-method-already-takes-a-token.cs` stays as the pin
        // on the behaviour, which is now `Evidence`'s to keep.
        if (model.GetDeclaredSymbol(method, cancellation) is not { } declared
            || declared.ReturnType is not INamedTypeSymbol returned
            || !tasks.Contains(returned.OriginalDefinition)
            || AsyncSignature.ImplementsAnInterface(declared)) {
            return;
        }

        if (Evidence(model, method, tokenType, cancellation) is not { } callee) {
            return;
        }

        candidates.Add(
            new Candidate(
                method.Identifier.ValueText,
                callee,
                method.Identifier.GetLocation(),
                Fix(model, method)
            )
        );
    }

    static bool IsAsync(MethodDeclarationSyntax method) {
        foreach (var modifier in method.Modifiers) {
            if (modifier.IsKind(SyntaxKind.AsyncKeyword)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     The first call in the body that would have taken a token, or null when there is none.
    /// </summary>
    /// <remarks>
    ///     ⚠ Calls inside a <c>catch</c> or a <c>finally</c> do not count, for the reason <c>SK3004</c>
    ///     excludes them: cleanup a cancellation can abort is worse than cleanup that ignores one, so
    ///     that call is one <c>SK3004</c> would never ask anybody to forward a token to. Counting it as
    ///     evidence would report a method whose only repair the sibling rule then declines to make.
    ///     <para>
    ///         ⚠ A call inside a nested body that declares its own token is not evidence either — that
    ///         one has a token already and is <c>SK3004</c>'s to report.
    ///     </para>
    /// </remarks>
    static string? Evidence(
        SemanticModel model,
        MethodDeclarationSyntax method,
        INamedTypeSymbol tokenType,
        System.Threading.CancellationToken cancellation
    ) {
        foreach (var node in method.DescendantNodes()) {
            if (node is not InvocationExpressionSyntax invocation
                || IsInsideCleanup(invocation, method)
                || CancellationTokens.CountInScope(model, invocation, tokenType, cancellation) != 0
                || !CancellationTokens.WantsAToken(model, invocation, tokenType, cancellation)) {
                continue;
            }

            return invocation.Expression switch {
                MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
                SimpleNameSyntax simple => simple.Identifier.ValueText,
                _ => invocation.Expression.ToString()
            };
        }

        return null;
    }

    static bool IsInsideCleanup(SyntaxNode node, SyntaxNode stop) {
        for (var current = node.Parent;
             current is not null && !ReferenceEquals(current, stop);
             current = current.Parent) {
            if (current is CatchClauseSyntax or FinallyClauseSyntax) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Append <c>CancellationToken cancellationToken = default</c>, spelled the way it binds here.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>CancellationToken</c> when the simple name resolves at this position, the fully
    ///     qualified name when it does not. A fix that emits the short name into a file with no
    ///     <c>using System.Threading;</c> is CS0246, and <c>FixCommand</c>'s post-fix check is
    ///     syntactic — it re-parses and compares syntax diagnostics — so a missing using would pass
    ///     verification and break the build. It is also why the rule declares <c>fixIsSafe: false</c>:
    ///     a parameter added to a signature is a signature the author should look at.
    /// </remarks>
    static ImmutableDictionary<string, string?> Fix(SemanticModel model, MethodDeclarationSyntax method) {
        var list = method.ParameterList;
        var name = "System.Threading.CancellationToken";
        foreach (var symbol in model.LookupNamespacesAndTypes(list.SpanStart, name: "CancellationToken")) {
            if (symbol is INamedTypeSymbol { Arity: 0 } type
                && string.Equals(type.ToDisplayString(), name, StringComparison.Ordinal)) {
                name = "CancellationToken";
                break;
            }
        }

        var text = name + " cancellationToken = default";
        var parameters = list.Parameters;
        return FixEdits.Pack(
            parameters.Count == 0
                ? (new TextSpan(list.CloseParenToken.SpanStart, 0), text)
                : (new TextSpan(parameters[parameters.Count - 1].Span.End, 0), ", " + text)
        );
    }
}
