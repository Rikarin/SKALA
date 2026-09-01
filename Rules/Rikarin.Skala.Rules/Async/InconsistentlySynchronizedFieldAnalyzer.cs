using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3044</c> — a private field is written without the lock that guards it everywhere else.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime".
///     A field taken under a lock in nine places and written bare in the tenth is a race that testing
///     will not find, because the tenth path is usually the one added last and the window is usually
///     nanoseconds wide.
///     <para>
///         ⚠ The rule is deliberately far narrower than the question in its title, and the reason is
///         docs/plan/16 § R3: a wrong concurrency finding sends somebody to read threading code that
///         was correct, which is the most expensive reading there is. Every gate below exists to buy
///         precision at the cost of recall, and the price is paid knowingly — <c>SK3044</c> is not a
///         claim that the fields it stays silent about are synchronized.
///     </para>
///     <list type="number">
///         <item>
///             The type must have <b>exactly one lock object</b>, and every <c>lock</c> in it must be
///             taken over that one field. Two gates means a hierarchy, and which of them was meant to
///             guard a given field is not decidable from the shape.
///         </item>
///         <item>
///             The type must use <b>no other synchronization at all</b> — no
///             <c>Interlocked</c>, <c>Volatile</c>, <c>Monitor</c>, semaphore, reader-writer lock or
///             <c>Lazy</c>. Any of those is ordering the rule cannot see, and an access it would
///             therefore call unguarded.
///         </item>
///         <item>
///             The unguarded access must be a <b>write</b>. A bare read of a field that is otherwise
///             guarded is a real hazard and is also the shape of a deliberate best-effort snapshot
///             (<c>public int Count => count;</c>), and the two are indistinguishable here.
///         </item>
///         <item>
///             The write must sit in a member that is
///             <b>
///                 callable from outside the type and is never
///                 called from inside the lock
///             </b>. A private helper, or a public method the type itself
///             only ever invokes while holding the lock, is the "caller holds the lock" contract, and
///             it is extremely common.
///         </item>
///         <item>
///             Any access to the field <b>inside a lambda or a local function</b> withdraws the field
///             entirely. A delegate runs whenever somebody invokes it, holding whatever they hold,
///             so neither "guarded" nor "unguarded" is knowable for it.
///         </item>
///     </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InconsistentlySynchronizedFieldAnalyzer : DiagnosticAnalyzer {
    /// <summary>
    ///     ⚠ Matched on the written name, which is the one place in this rule where that is the right
    ///     instrument. The list can only <em>silence</em> the rule, so a name collision with somebody's
    ///     own <c>Monitor</c> costs a finding and never produces a wrong one — and resolving each name
    ///     would mean binding every identifier in the type to rule out synchronization the rule cannot
    ///     model anyway.
    /// </summary>
    static readonly HashSet<string> ForeignSynchronization = new(StringComparer.Ordinal) {
        "Interlocked",
        "Volatile",
        "Monitor",
        "SemaphoreSlim",
        "Semaphore",
        "Mutex",
        "ReaderWriterLockSlim",
        "ReaderWriterLock",
        "SpinLock",
        "Barrier",
        "CountdownEvent",
        "ManualResetEvent",
        "ManualResetEventSlim",
        "AutoResetEvent",
        "EventWaitHandle",
        "Lazy"
    };

    static readonly DiagnosticDescriptor Descriptor =
        SkalaRule.Descriptor(RuleIds.InconsistentlySynchronizedField);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    static void Analyze(SymbolAnalysisContext context) {
        if (context.Symbol is not INamedTypeSymbol owner) {
            return;
        }

        var declarations = owner.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(context.CancellationToken))
            .OfType<TypeDeclarationSyntax>()
            .ToArray();

        // ⚠ The cheap gate, and it is here for a reason that is not tidiness: everything below needs
        // a semantic model, and asking a symbol action for one binds the tree afresh. A type with no
        // `lock` in it can never produce a finding, and that is nearly every type.
        var bodies = declarations.SelectMany(Own).ToArray();
        var locks = bodies.OfType<LockStatementSyntax>().ToArray();
        if (locks.Length == 0) {
            return;
        }

        foreach (var node in bodies) {
            if (node is SimpleNameSyntax name && ForeignSynchronization.Contains(name.Identifier.ValueText)) {
                return;
            }
        }

        var model = new ModelCache(context.Compilation);
        if (Gate(locks, owner, model, context.CancellationToken) is not { } gate) {
            return;
        }

        foreach (var field in owner.GetMembers().OfType<IFieldSymbol>()) {
            if (field.IsImplicitlyDeclared
                || field.IsConst
                || field.IsReadOnly
                || field.IsVolatile
                || field.DeclaredAccessibility != Accessibility.Private
                || field.IsStatic != gate.IsStatic
                || SymbolEqualityComparer.Default.Equals(field, gate)) {
                continue;
            }

            Examine(context, field, gate, bodies, locks, model);
        }
    }

    /// <summary>
    ///     The type's single lock object, or <c>null</c> if it does not have exactly one.
    /// </summary>
    static IFieldSymbol? Gate(
        IReadOnlyList<LockStatementSyntax> locks,
        INamedTypeSymbol owner,
        ModelCache model,
        CancellationToken cancellation
    ) {
        IFieldSymbol? gate = null;
        foreach (var statement in locks) {
            var target = statement.Expression switch {
                IdentifierNameSyntax identifier => identifier,
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: { } name } => name,
                _ => null
            };

            if (target is null
                || model.Of(target).GetSymbolInfo(target, cancellation).Symbol is not IFieldSymbol field
                || !SymbolEqualityComparer.Default.Equals(field.ContainingType, owner)) {
                return null;
            }

            if (gate is null) {
                gate = field;
            } else if (!SymbolEqualityComparer.Default.Equals(gate, field)) {
                return null;
            }
        }

        return gate;
    }

    static void Examine(
        SymbolAnalysisContext context,
        IFieldSymbol field,
        IFieldSymbol gate,
        IReadOnlyList<SyntaxNode> bodies,
        IReadOnlyList<LockStatementSyntax> locks,
        ModelCache model
    ) {
        var guarded = 0;
        var unguarded = new List<(MemberDeclarationSyntax Member, Location Location)>();
        foreach (var node in bodies) {
            if (node is not SimpleNameSyntax name
                || !string.Equals(name.Identifier.ValueText, field.Name, StringComparison.Ordinal)
                || !SymbolEqualityComparer.Default.Equals(
                    model.Of(name).GetSymbolInfo(name, context.CancellationToken).Symbol,
                    field
                )) {
                continue;
            }

            var reference = name.Parent is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access
                ? access
                : (ExpressionSyntax)name;

            var (member, insideLambda, locked) = Where(reference);

            // ⚠ Withdraws the whole field, not this access. A delegate runs whenever somebody
            // invokes it, holding whatever they hold, so neither answer is knowable for it — and
            // the wrong answer here is a finding rather than a silence.
            if (insideLambda) {
                return;
            }

            if (member is null
                or ConstructorDeclarationSyntax
                or DestructorDeclarationSyntax
                or FieldDeclarationSyntax) {
                continue;
            }

            if (locked) {
                guarded++;
            } else if (IsWrite(reference)
                       && IsReachableWithoutTheLock(member, locks, model, context.CancellationToken)) {
                unguarded.Add((member, reference.GetLocation()));
            }
        }

        // ⚠ Two, not one. One guarded access and one bare one is as likely to be a lock introduced
        // in the wrong place as a lock forgotten in the other, and the rule has no way to say which.
        if (guarded < 2 || unguarded.Count == 0) {
            return;
        }

        var first = unguarded
            .OrderBy(static entry => entry.Location.SourceTree?.FilePath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Location.SourceSpan.Start)
            .First();

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                first.Location,
                "`"
                + field.Name
                + "` is written here without holding `"
                + gate.Name
                + "`, and is accessed under it "
                + guarded.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " times elsewhere in this type"
            )
        );
    }

    /// <summary>Where an access sits: its member, whether a closure is in the way, and whether a lock is.</summary>
    static (MemberDeclarationSyntax? Member, bool InsideLambda, bool Locked) Where(SyntaxNode node) {
        var lambda = false;
        var locked = false;
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case LockStatementSyntax:
                    locked = true;

                    break;
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                    lambda = true;

                    break;
                case MemberDeclarationSyntax member:
                    return (member, lambda, locked);
            }
        }

        return (null, lambda, locked);
    }

    static bool IsWrite(ExpressionSyntax reference) =>
        reference.Parent switch {
            AssignmentExpressionSyntax assignment => assignment.Left == reference,
            PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.PreIncrementExpression)
                || prefix.IsKind(SyntaxKind.PreDecrementExpression),
            PostfixUnaryExpressionSyntax postfix => postfix.IsKind(SyntaxKind.PostIncrementExpression)
                || postfix.IsKind(SyntaxKind.PostDecrementExpression),
            ArgumentSyntax argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None),
            _ => false
        };

    /// <summary>
    ///     Whether a member can be entered by somebody who is not already holding the lock.
    /// </summary>
    /// <remarks>
    ///     ⚠ The gate that keeps the "caller holds the lock" contract out, and it is the most common
    ///     shape this rule would otherwise wreck. A private helper is never reported at all. A member
    ///     the type itself invokes from inside a <c>lock</c> body is not reported either: whatever it
    ///     is documented to be, the type demonstrably calls it under the lock, and a finding would be
    ///     an argument about a convention rather than a race.
    ///     <para>
    ///         ⚠ <c>Dispose</c> is excluded by name. Disposal after the last user has let go is the
    ///         normal contract, and every disposable type would otherwise report.
    ///     </para>
    /// </remarks>
    static bool IsReachableWithoutTheLock(
        MemberDeclarationSyntax member,
        IReadOnlyList<LockStatementSyntax> locks,
        ModelCache model,
        CancellationToken cancellation
    ) {
        if (model.Of(member).GetDeclaredSymbol(member, cancellation) is not { } symbol
            || symbol.DeclaredAccessibility == Accessibility.Private
            || symbol.Name is "Dispose" or "DisposeAsync" or "Finalize") {
            return false;
        }

        foreach (var statement in locks) {
            foreach (var invocation in statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()) {
                var invoked = model.Of(invocation).GetSymbolInfo(invocation, cancellation).Symbol;
                if (invoked is not null
                    && SymbolEqualityComparer.Default.Equals(invoked.OriginalDefinition, symbol)) {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Every node of a type declaration that belongs to <em>this</em> type.</summary>
    /// <remarks>
    ///     ⚠ A nested type's declarations are descendants of the outer one's. Walking into them would
    ///     count another type's locks as this type's, and this rule's first gate is "exactly one lock
    ///     object" — a nested type with a second gate would silently withdraw the outer type.
    /// </remarks>
    static IEnumerable<SyntaxNode> Own(TypeDeclarationSyntax declaration) =>
        declaration.DescendantNodes(node => node == declaration || node is not TypeDeclarationSyntax);

    /// <summary>
    ///     One semantic model per tree, for the life of one type's analysis.
    /// </summary>
    /// <remarks>
    ///     ⚠ A symbol action has no model of its own, and <c>Compilation.GetSemanticModel</c> binds the
    ///     tree afresh every call. Asking once per identifier would re-bind the file hundreds of times
    ///     for one type. The `lock`-present gate in <see cref="Analyze" /> is what keeps this from
    ///     running at all on the overwhelming majority of types.
    /// </remarks>
    sealed class ModelCache(Compilation compilation) {
        readonly Dictionary<SyntaxTree, SemanticModel> models = [];

        public SemanticModel Of(SyntaxNode node) {
            var tree = node.SyntaxTree;
            if (!models.TryGetValue(tree, out var model)) {
                models[tree] = model = compilation.GetSemanticModel(tree);
            }

            return model;
        }
    }
}
