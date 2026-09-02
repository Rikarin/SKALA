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
///     no caller can stop the work. The two are the same argument at two points in the call graph.
///     <para>
///         ⚠ <b>This rule's fix now does <c>SK3004</c>'s half too, and the handover it used to rely on
///         was the defect (#328).</b> The fix appended the parameter and stopped, leaving a signature
///         that advertised a cancellation the body dropped and a finding that had disappeared — the
///         rule looks for the <em>parameter</em>, and the parameter was there. It now emits the
///         parameter and the argument at every call in the body that can take one, in a single edit
///         list, so the two rules never disagree about a body: after this fix there is nothing left for
///         <c>SK3004</c> to say about the calls it repaired.
///     </para>
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
    ///     ⚠ A class rather than a positional record, and <b>the reason written here was false</b> —
    ///     the same refuted claim <c>AsyncVoidAnalyzer.Candidate</c> carried. <c>Compat.cs</c> in this
    ///     assembly declares <c>IsExternalInit</c>, so <c>init</c> accessors and positional records
    ///     compile; measured, not assumed. The class stays, its stated justification does not. See
    ///     doc 02 § "What netstandard2.0 costs a rule author".
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

        var forwards = Forwards(model, method, tokenType, cancellation);
        if (forwards.Count == 0) {
            return;
        }

        candidates.Add(
            new Candidate(
                method.Identifier.ValueText,
                forwards[0].Callee,
                method.Identifier.GetLocation(),
                Fix(model, method, forwards)
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
    ///     Every call in the body the new parameter would be forwarded to, in source order.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>This is both the evidence and the fix, and #328 is what made them one list.</b> The rule
    ///     used to answer "is there a call here that would have taken a token" and then emit a parameter
    ///     and nothing else, so <c>ReadMessageAsync(CancellationToken cancellationToken = default)</c>
    ///     shipped with <c>input.ReadLineAsync()</c> and <c>input.ReadAsync(…)</c> untouched — a
    ///     signature advertising a cancellation the body drops, at every call site, invisibly. An empty
    ///     list withdraws the finding, so <b>the parameter can never be added with nothing to forward
    ///     it to</b>: the count is at least one by construction.
    ///     <para>
    ///         ⚠ Calls inside a <c>catch</c> or a <c>finally</c> do not count, for the reason
    ///         <c>SK3004</c> excludes them: cleanup a cancellation can abort is worse than cleanup that
    ///         ignores one, so that call is one <c>SK3004</c> would never ask anybody to forward a token
    ///         to.
    ///     </para>
    ///     <para>
    ///         ⚠ A call inside a nested body that declares its own token is not evidence either — that
    ///         one has a token already and is <c>SK3004</c>'s to report.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>A call inside a <c>static</c> lambda or a <c>static</c> local function is skipped,
    ///         and that is CS8421 rather than taste</b> — a static anonymous function cannot capture the
    ///         enclosing method's parameter, so an argument naming it does not compile. Skipping it here
    ///         rather than only in the fix is what keeps the two halves the same list.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>A body that also awaits something no token can reach is still reported.</b> A
    ///         <c>CancellationToken</c> is a promise of cooperative cancellation at the awaits that can
    ///         honour it, not of an abort — every real body mixes the two, and <c>ConfigureAwait</c>
    ///         alone is an awaited invocation that takes no token. Declining a method because one await
    ///         cannot take a token would decline nearly all of them; the defect #328 named is a
    ///         parameter forwarded to <em>nothing</em>, and a non-empty list is what rules that out.
    ///     </para>
    /// </remarks>
    static List<CancellationTokens.Forward> Forwards(
        SemanticModel model,
        MethodDeclarationSyntax method,
        INamedTypeSymbol tokenType,
        System.Threading.CancellationToken cancellation
    ) {
        var forwards = new List<CancellationTokens.Forward>();
        foreach (var node in method.DescendantNodes()) {
            if (node is not InvocationExpressionSyntax invocation
                || IsInsideCleanup(invocation, method)
                || IsInsideANonCapturingFunction(invocation, method)
                || CancellationTokens.CountInScope(model, invocation, tokenType, cancellation) != 0) {
                continue;
            }

            if (CancellationTokens.Forwarding(model, invocation, tokenType, "cancellationToken", cancellation)
                is { } forward) {
                forwards.Add(forward);
            }
        }

        return forwards;
    }

    /// <summary>
    ///     Whether a <c>static</c> lambda or <c>static</c> local function stands between the two.
    /// </summary>
    static bool IsInsideANonCapturingFunction(SyntaxNode node, SyntaxNode stop) {
        for (var current = node.Parent;
             current is not null && !ReferenceEquals(current, stop);
             current = current.Parent) {
            var modifiers = current switch {
                AnonymousFunctionExpressionSyntax function => function.Modifiers,
                LocalFunctionStatementSyntax local => local.Modifiers,
                _ => default
            };

            foreach (var modifier in modifiers) {
                if (modifier.IsKind(SyntaxKind.StaticKeyword)) {
                    return true;
                }
            }
        }

        return false;
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
    ///     Append <c>CancellationToken cancellationToken = default</c> and forward it to every call.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>CancellationToken</c> when the simple name resolves at this position, the fully
    ///     qualified name when it does not. A fix that emits the short name into a file with no
    ///     <c>using System.Threading;</c> is CS0246, and <c>FixCommand</c>'s post-fix check is
    ///     syntactic — it re-parses and compares syntax diagnostics — so a missing using would pass
    ///     verification and break the build. It is also why the rule declares <c>fixIsSafe: false</c>:
    ///     a parameter added to a signature is a signature the author should look at.
    ///     <para>
    ///         ⚠ <b>The forwarding edits are the other half of the rewrite (#328), not a nicety.</b>
    ///         Every edit is a zero-length insertion at a distinct offset, and <c>FixCommand</c> applies
    ///         a finding's edits back to front, so the parameter — the leftmost of them — cannot move
    ///         the offsets of the arguments that follow it.
    ///     </para>
    /// </remarks>
    static ImmutableDictionary<string, string?> Fix(
        SemanticModel model,
        MethodDeclarationSyntax method,
        List<CancellationTokens.Forward> forwards
    ) {
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
        var edits = new (TextSpan Span, string Text)[forwards.Count + 1];
        edits[0] = parameters.Count == 0
            ? (new TextSpan(list.CloseParenToken.SpanStart, 0), text)
            : (new TextSpan(parameters[parameters.Count - 1].Span.End, 0), ", " + text);

        for (var i = 0; i < forwards.Count; i++) {
            edits[i + 1] = (forwards[i].Span, forwards[i].Text);
        }

        return FixEdits.Pack(edits);
    }
}
