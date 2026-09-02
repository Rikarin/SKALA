using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3060</c> — a lock is entered and its release is not on every path.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime".
///     <c>Monitor.Enter</c> followed by work followed by <c>Monitor.Exit</c> holds the lock forever the
///     first time the work throws: the exception propagates normally, nothing is logged, and the
///     deadlock happens later, in another thread, in another file. There is no diagnostic anywhere that
///     connects the two, which is why the shape has to be caught where it is written.
///     <para>
///         ⚠ The <c>lock</c> keyword is out of scope <em>by construction</em>, not by a guard.
///         <c>lock (x) { }</c> lowers to exactly <c>Monitor.Enter</c> plus <c>try</c>/<c>finally</c>
///         <c>Monitor.Exit</c>, so it is always correct — and it produces no <c>Monitor.Enter</c>
///         invocation in the syntax tree at all. This rule registers on
///         <see cref="SyntaxKind.InvocationExpression" /> only, so a <c>lock</c> statement can never
///         reach it. <c>SK3060/negative/the-lock-keyword.cs</c> pins that anyway, because "cannot
///         happen" is a claim and an unasserted claim is worth nothing.
///     </para>
///     <para>
///         ⚠ Where the release is <em>allowed</em> to be is a much smaller question than where it
///         <em>runs</em>. The rule asks only whether some matching release sits lexically inside a
///         <c>finally</c> of this body; it does not try to prove the <c>finally</c> is the one that
///         guards the critical section. Anything stronger needs a flow analysis, and the wrong answer
///         from one is a finding on threading code that was correct — the most expensive reading there
///         is (docs/plan/16 § R3).
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnreleasedLockAnalyzer : DiagnosticAnalyzer {
    /// <summary>
    ///     The enter calls the rule knows, each with the one release that matches it.
    /// </summary>
    /// <remarks>
    ///     ⚠ The three <c>ReaderWriterLockSlim</c> rows are what makes the mismatched-release bug fall
    ///     out of the same mechanism instead of needing a branch of its own. Pairing is keyed on the
    ///     enter method and not on the type, so an <c>EnterWriteLock</c> whose <c>finally</c> calls
    ///     <c>ExitReadLock</c> has no matching <c>ExitWriteLock</c> anywhere: it is reported by the
    ///     ordinary "no matching release" path, and the fact that the author did write <em>a</em> release
    ///     in <em>a</em> <c>finally</c> never enters into it. Collapsing these rows to "an Exit* call"
    ///     would lose exactly that finding — and <c>ExitReadLock</c> on a write lock throws
    ///     <c>SynchronizationLockException</c>, so the lock is then held forever <em>and</em> the finally
    ///     block throws over whatever was propagating.
    ///     <para>
    ///         ⚠ Deliberately not a list of primitives. <c>SemaphoreSlim.Wait</c>/<c>Release</c> and
    ///         <c>Mutex.WaitOne</c>/<c>ReleaseMutex</c> are the same <em>shape</em> and a different
    ///         <em>fact</em>: a semaphore acquired in one method and released in another is how a
    ///         semaphore is normally used, so the missing <c>finally</c> is not evidence of anything
    ///         there. The limit is stated rather than hidden —
    ///         <c>SK3060/negative/a-semaphore-without-a-finally.cs</c> is the record of it.
    ///     </para>
    /// </remarks>
    static readonly (string Owner, string Enter, string Release)[] Protocols = {
        ("System.Threading.Monitor", "Enter", "Exit"), ("System.Threading.Monitor", "TryEnter", "Exit"),
        ("System.Threading.ReaderWriterLockSlim", "EnterReadLock", "ExitReadLock"),
        ("System.Threading.ReaderWriterLockSlim", "EnterWriteLock", "ExitWriteLock"),
        ("System.Threading.ReaderWriterLockSlim", "EnterUpgradeableReadLock", "ExitUpgradeableReadLock")
    };

    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UnreleasedLock);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // ⚠ Resolved once per compilation, never per node. This rule is registered on every invocation
        // expression in every file, which is the densest node kind there is, so the only work allowed
        // before the name check is a dictionary probe. A compilation that references neither type
        // registers nothing at all.
        context.RegisterCompilationStartAction(static start => {
                var builder = ImmutableDictionary.CreateBuilder<string, Protocol>(StringComparer.Ordinal);
                foreach (var (owner, enter, release) in Protocols) {
                    if (start.Compilation.GetTypeByMetadataName(owner) is { } type) {
                        builder[enter] = new(type, release);
                    }
                }

                if (builder.Count == 0) {
                    return;
                }

                var protocols = builder.ToImmutable();
                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, protocols),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, ImmutableDictionary<string, Protocol> protocols) {
        var enter = (InvocationExpressionSyntax)context.Node;

        // The cheap gate: the written name, which is free, before anything that binds.
        if (Called(enter) is not { } name
            || !protocols.TryGetValue(name, out var protocol)
            || !IsCallTo(enter, name, protocol.Owner, context.SemanticModel, context.CancellationToken)) {
            return;
        }

        // ⚠ The unit is the enclosing function body, not the method. An enter inside a lambda is judged
        // against that lambda, because the lambda is what runs — a `finally` in the method around it
        // does not wrap the delegate's invocation, and a release after the lambda does not follow it.
        if (Body(enter) is not { } body) {
            return;
        }

        var found = false;
        var deferred = false;
        foreach (var node in body.DescendantNodes()) {
            if (node is not InvocationExpressionSyntax release
                || !string.Equals(Called(release), protocol.Release, StringComparison.Ordinal)
                || !IsCallTo(
                    release,
                    protocol.Release,
                    protocol.Owner,
                    context.SemanticModel,
                    context.CancellationToken
                )) {
                continue;
            }

            // A delegate's release does not run on this path — it runs whenever somebody invokes the
            // delegate, holding whatever they hold. It cannot count as the release, and it also cannot
            // be counted against the author: see `deferred` below.
            if (Between<AnonymousFunctionExpressionSyntax, LocalFunctionStatementSyntax>(release, body)) {
                deferred = true;

                continue;
            }

            // Exception-safe, and that is the whole question the rule asks.
            if (Between<FinallyClauseSyntax, FinallyClauseSyntax>(release, body)) {
                return;
            }

            found = true;
        }

        if (deferred || HasProtocolElsewhere(enter, protocol, context.SemanticModel, context.CancellationToken)) {
            return;
        }

        var qualified = protocol.Owner.Name + "." + name;
        var releaseName = protocol.Owner.Name + "." + protocol.Release;

        // ⚠ Two messages, because the two cases read differently to whoever gets the finding. "There is
        // no Exit" sends a reader looking for one; "the Exit you wrote is on the happy path" sends them
        // to the one they already wrote and cannot see the problem with.
        //
        // ⚠ Neither message says "here" of the release and neither says "method", and both of those
        // were wrong in the first draft. The finding is reported at the *enter*, so "`Monitor.Exit`
        // here" underlined one call and named another — the reader looks at the squiggle, not at the
        // sentence. And the unit of analysis is the enclosing function, which is a lambda often
        // enough that "in this method" names nothing the reader can point at: an enter in a field
        // initializer's lambda is the case that made it obvious.
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                enter.GetLocation(),
                found
                    ? "`"
                    + qualified
                    + "` is released by a `"
                    + releaseName
                    + "` that is not inside a `finally`, so it runs only when nothing throws and the lock stays held for the life of the process if the critical section does"
                    : "`"
                    + qualified
                    + "` has no matching `"
                    + releaseName
                    + "` on this path, so the lock is never released"
            )
        );
    }

    /// <summary>
    ///     Whether the type holding this enter runs an enter/release protocol across its members.
    /// </summary>
    /// <remarks>
    ///     ⚠ The escape that keeps the rule off deliberate designs. A type with <c>Acquire()</c> and
    ///     <c>Release()</c> has split the pairing across two members on purpose, and there is no
    ///     <c>finally</c> that could span them — reporting it would be an argument about a convention
    ///     rather than a bug, which is exactly the finding that gets the analysis half switched off.
    ///     <para>
    ///         ⚠ Nested type declarations are not walked, for the reason
    ///         <c>InconsistentlySynchronizedFieldAnalyzer.Own</c> gives: a nested type's members are
    ///         descendants of the outer type's declaration, so walking into them would let an unrelated
    ///         inner class silence its container.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>A <c>partial</c> type silences the rule outright, and that gate is here because the
    ///         shape was tested rather than reasoned about.</b> The walk starts from the
    ///         <em>syntactic</em> declaration holding the enter, so it sees one part and not the others
    ///         — and the parts are usually in different files. A partial type with <c>Acquire()</c> in
    ///         one part and <c>Release()</c> in the other produced a false positive;
    ///         <c>a-protocol-split-across-partial-parts.cs</c> is what holds the fix. Walking every
    ///         <c>DeclaringSyntaxReference</c> instead would make the answer for one file depend on
    ///         files the cache key does not name, which is what <c>scope: Compilation</c> costs and what
    ///         this rule declines to pay for a shape this rare.
    ///     </para>
    ///     <para>
    ///         ⚠ Runs only for a genuine candidate, which is close to never. Everything above it is a
    ///         name probe and a bind; this is a walk of a whole type declaration.
    ///     </para>
    /// </remarks>
    static bool HasProtocolElsewhere(
        InvocationExpressionSyntax enter,
        Protocol protocol,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        if (enter.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } declaration) {
            return false;
        }

        if (declaration.Modifiers.Any(SyntaxKind.PartialKeyword)) {
            return true;
        }

        var member = enter.FirstAncestorOrSelf<MemberDeclarationSyntax>();
        foreach (var node in declaration.DescendantNodes(node => node == declaration
                     || node is not TypeDeclarationSyntax
                 )) {
            if (node is not InvocationExpressionSyntax release
                || !string.Equals(Called(release), protocol.Release, StringComparison.Ordinal)
                || release.FirstAncestorOrSelf<MemberDeclarationSyntax>() == member) {
                continue;
            }

            if (IsCallTo(release, protocol.Release, protocol.Owner, model, cancellation)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>The written name of an invoked method, without binding anything.</summary>
    static string? Called(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => null
        };

    /// <summary>
    ///     ⚠ The symbol, never the written name. Somebody's own type called <c>Monitor</c> with its own
    ///     <c>Enter</c> is entirely plausible — <see cref="LockOverSynchronizationPrimitiveAnalyzer" />
    ///     makes the same point about <c>Semaphore</c> — and a finding on one of those sends a reader to
    ///     threading code that does not exist.
    /// </summary>
    static bool IsCallTo(
        InvocationExpressionSyntax invocation,
        string name,
        INamedTypeSymbol owner,
        SemanticModel model,
        CancellationToken cancellation
    ) =>
        model.GetSymbolInfo(invocation, cancellation).Symbol is IMethodSymbol method
        && string.Equals(method.Name, name, StringComparison.Ordinal)
        && SymbolEqualityComparer.Default.Equals(method.ContainingType, owner);

    /// <summary>The nearest enclosing function, or <c>null</c> where there is none.</summary>
    /// <remarks>
    ///     ⚠ Stops at the type declaration rather than running to the root, so an enter in a field
    ///     initializer or in a top-level statement declines instead of being judged against some outer
    ///     function it does not run inside.
    /// </remarks>
    static SyntaxNode? Body(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case MethodDeclarationSyntax:
                case ConstructorDeclarationSyntax:
                case DestructorDeclarationSyntax:
                case AccessorDeclarationSyntax:
                case OperatorDeclarationSyntax:
                case ConversionOperatorDeclarationSyntax:
                case LocalFunctionStatementSyntax:
                case AnonymousFunctionExpressionSyntax:
                    return current;
                case TypeDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }

    /// <summary>Whether a node of either kind sits between <paramref name="node" /> and its body.</summary>
    /// <remarks>
    ///     The walk stops at the body rather than at the root: a <c>finally</c> outside the lambda that
    ///     holds the enter does not wrap the lambda's invocation, and a local function outside the
    ///     enter's own local function is not nested inside it.
    /// </remarks>
    static bool Between<TFirst, TSecond>(SyntaxNode node, SyntaxNode body)
        where TFirst : SyntaxNode
        where TSecond : SyntaxNode {
        for (var current = node.Parent; current is not null && current != body; current = current.Parent) {
            if (current is TFirst or TSecond) {
                return true;
            }
        }

        return false;
    }

    /// <summary>One enter call's owning type and the release that matches it.</summary>
    readonly record struct Protocol(INamedTypeSymbol Owner, string Release);
}
