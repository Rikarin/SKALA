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
///     <c>SK3061</c> — the <c>lock</c> takes a monitor that is not the monitor another thread takes.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime".
///     A <c>lock</c> excludes exactly the threads that take the same monitor, and the monitor lives on
///     the object — so which object it is, is the entire contract. Two shapes break it, both of them
///     reading as correct code:
///     <list type="number">
///         <item>
///             <b>The target is created in the same invocation.</b> <c>var gate = new object();</c>
///             gives every call its own monitor, so the critical section excludes nobody and the whole
///             construct is a no-op.
///         </item>
///         <item>
///             <b>The target is a field the type reassigns.</b> Whoever entered before the assignment
///             holds a different monitor from whoever enters after, so two threads sit inside the same
///             <c>lock</c> body at once.
///         </item>
///     </list>
///     <para>
///         ⚠ <b><c>lock (this)</c>, <c>lock (typeof(T))</c> and locking a string are deliberately not
///         here.</b> <c>CA2002</c> — *do not lock on objects with weak identity* — already reports all
///         four of those, measured one shape per file on a pristine <c>net10.0</c> classlib: it is
///         silent in a default build (its shipped descriptor is
///         <c>IsEnabledByDefault=False, DefaultSeverity=Warning</c>) and fires on every one of them once
///         its severity is raised. Skala's own repository raises <c>AnalysisMode</c>, so duplicating
///         them would double-report here first. The same measurement is what says this rule has
///         something to do at all: <c>CA2002</c> is <em>silent</em> on a fresh local, on a
///         <c>readonly</c> field and on a mutable <c>object</c> field, which is the whole of both shapes
///         above.
///     </para>
///     <para>
///         ⚠ <b><c>SK3040</c>'s types are declined, in both shapes.</b> <c>var s = new SemaphoreSlim(1);
///         lock (s) { }</c> is a fresh local <em>and</em> a lock over a synchronization primitive, and
///         the second reading is the one that tells the reader what to do. The list is
///         <see cref="PrimitiveNames" />, copied from that rule rather than referenced, because a rule
///         may not depend on another rule's private judgement of its own scope.
///     </para>
///     <para>
///         ⚠ <b><c>SK1023</c> is disjoint by construction, not by filter.</b> That rule modernizes a
///         <c>lock</c> over a <b>private readonly</b> <c>object</c> field to
///         <c>System.Threading.Lock</c>; shape 2 requires a field that is <b>not</b> <c>readonly</c>, so
///         no field can carry both findings and neither declares <c>supersedes</c>. The negative
///         fixture <c>a-private-readonly-field.cs</c> is what holds it.
///     </para>
///     <para>
///         ⚠ <b>Known gap: a <c>lock</c> in a top-level program is never examined.</b> The synthesized
///         <c>Program</c> type's <c>DeclaringSyntaxReferences</c> are not
///         <c>TypeDeclarationSyntax</c>, so the symbol walk below drops it. A top-level statement file
///         is a script, not a shared-state type, and the shape is not worth a second registration that
///         could double-report; recorded rather than hidden.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IneffectiveLockTargetAnalyzer : DiagnosticAnalyzer {
    /// <summary>
    ///     <c>SK3040</c>'s list, resolved from the compilation and never matched on the written name.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>System.Threading.Lock</c> is absent for the same reason it is absent there — it is the
    ///     type a C# 13 <c>lock</c> is <em>meant</em> to be taken over. Its absence is load-bearing in
    ///     the opposite direction here: <c>var gate = new Lock(); lock (gate) { }</c> is a fresh monitor
    ///     per call and is exactly as ineffective as <c>new object()</c>, so shape 1 must reach it.
    /// </remarks>
    static readonly string[] PrimitiveNames = {
        "System.Threading.WaitHandle", "System.Threading.SemaphoreSlim", "System.Threading.ManualResetEventSlim",
        "System.Threading.ReaderWriterLockSlim", "System.Threading.ReaderWriterLock", "System.Threading.CountdownEvent",
        "System.Threading.Barrier"
    };

    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.IneffectiveLockTarget);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var primitives = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
                foreach (var name in PrimitiveNames) {
                    if (start.Compilation.GetTypeByMetadataName(name) is { } type) {
                        primitives.Add(type);
                    }
                }

                // ⚠ No early return when nothing resolved, and that is the difference from `SK3040`.
                // There the list *is* the rule and an empty one means there is nothing to find; here it
                // is only an exclusion, so returning would switch the rule off wherever
                // `System.Threading` is unavailable — a silence indistinguishable from clean code.
                var resolved = primitives.ToImmutable();
                start.RegisterSymbolAction(context => Analyze(context, resolved), SymbolKind.NamedType);
            }
        );
    }

    /// <summary>
    ///     One type, every <c>lock</c> in it. A symbol action because shape 2 needs every write to the
    ///     field, and a type is partial across files: <c>rules.json</c> declares this rule
    ///     <c>scope: Compilation</c> for that reason.
    /// </summary>
    static void Analyze(SymbolAnalysisContext context, ImmutableArray<INamedTypeSymbol> primitives) {
        if (context.Symbol is not INamedTypeSymbol owner) {
            return;
        }

        var declarations = owner.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(context.CancellationToken))
            .OfType<TypeDeclarationSyntax>()
            .ToArray();

        // ⚠ The cheap gate, and it is not tidiness: everything below needs a semantic model, and a
        // symbol action has none of its own. A type with no `lock` in it can never produce a finding,
        // and that is nearly every type.
        var bodies = declarations.SelectMany(Own).ToArray();
        var locks = bodies.OfType<LockStatementSyntax>().ToArray();
        if (locks.Length == 0) {
            return;
        }

        var model = new ModelCache(context.Compilation);
        var reassigned = new Dictionary<ISymbol, bool>(SymbolEqualityComparer.Default);
        foreach (var statement in locks) {
            Examine(context, statement, owner, bodies, model, primitives, reassigned);
        }
    }

    static void Examine(
        SymbolAnalysisContext context,
        LockStatementSyntax statement,
        INamedTypeSymbol owner,
        IReadOnlyList<SyntaxNode> bodies,
        ModelCache model,
        ImmutableArray<INamedTypeSymbol> primitives,
        Dictionary<ISymbol, bool> reassigned
    ) {
        var expression = statement.Expression;
        if (model.Of(expression).GetTypeInfo(expression, context.CancellationToken).Type is INamedTypeSymbol type
            && IsPrimitive(type, primitives)) {
            return;
        }

        // The degenerate direct form. There is no name to quote, so the message quotes the creation.
        if (expression is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax) {
            Report(context, expression, Created(expression.ToString()));

            return;
        }

        var target = expression switch {
            IdentifierNameSyntax identifier => identifier,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: { } name } => name,
            _ => null
        };

        if (target is null) {
            return;
        }

        switch (model.Of(target).GetSymbolInfo(target, context.CancellationToken).Symbol) {
            case ILocalSymbol local:
                ExamineLocal(context, statement, local, model);

                break;
            case IFieldSymbol field:
                ExamineField(context, statement, field, owner, bodies, model, reassigned);

                break;
        }
    }

    /// <summary>
    ///     Shape 1 — a local whose value is a fresh object, so every invocation locks a different one.
    /// </summary>
    /// <remarks>
    ///     ⚠ The object-creation initializer is the load-bearing gate, and dropping it is how this rule
    ///     would wreck correct code: <c>var gate = this.gate;</c> and <c>var gate = Shared.Gate;</c>
    ///     are locals too, they are locked exactly like the bad shape, and they alias one shared object
    ///     that every thread reaches. Only a creation counts.
    ///     <para>
    ///         ⚠ Capture withdraws the finding rather than refining it. A delegate that closes over the
    ///         local outlives the call, so "one monitor per invocation" is no longer the truth and the
    ///         rule has no way to say what is. The specification pairs this with "declared inside a loop
    ///         whose body it outlives", which is an empty set in C# — a loop-body local's scope <em>is</em>
    ///         the body, and the only way it outlives an iteration is by being captured, which this
    ///         already declines.
    ///     </para>
    ///     <para>
    ///         ⚠ The declaration must sit in the same function body as the <c>lock</c>. A local of an
    ///         enclosing method, locked inside a lambda, is shared by every invocation of that delegate
    ///         and is the opposite of this finding.
    ///     </para>
    /// </remarks>
    static void ExamineLocal(
        SymbolAnalysisContext context,
        LockStatementSyntax statement,
        ILocalSymbol local,
        ModelCache model
    ) {
        if (local.DeclaringSyntaxReferences.Length != 1
            || local.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken)
                is not VariableDeclaratorSyntax declarator
            || declarator.Initializer?.Value
                is not (ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax)) {
            return;
        }

        var function = EnclosingFunction(statement);
        if (function is null || function != EnclosingFunction(declarator)) {
            return;
        }

        foreach (var name in function.DescendantNodes().OfType<IdentifierNameSyntax>()) {
            if (!string.Equals(name.Identifier.ValueText, local.Name, StringComparison.Ordinal)
                || !SymbolEqualityComparer.Default.Equals(
                    model.Of(name).GetSymbolInfo(name, context.CancellationToken).Symbol,
                    local
                )) {
                continue;
            }

            if (IsWrite(name) || IsInsideClosure(name, function)) {
                return;
            }
        }

        Report(context, statement.Expression, Created(local.Name));
    }

    /// <summary>
    ///     Shape 2 — a private, non-<c>readonly</c> field the type assigns outside its constructors.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Private only.</b> The claim being made is that <em>every</em> write to the field has
    ///     been seen, and that is only true while the compiler guarantees they are all inside the
    ///     containing type. A non-private field can be assigned from anywhere in the compilation, so the
    ///     rule would be guessing in both directions at once.
    ///     <para>
    ///         ⚠ <b>A field assigned only in a constructor or in its own initializer is effectively
    ///         <c>readonly</c> and is never reported.</b> That shape is common, it is correct, and
    ///         reporting it would turn this rule into noise — it is the single most important negative
    ///         in the set and carries more than one fixture.
    ///     </para>
    /// </remarks>
    static void ExamineField(
        SymbolAnalysisContext context,
        LockStatementSyntax statement,
        IFieldSymbol field,
        INamedTypeSymbol owner,
        IReadOnlyList<SyntaxNode> bodies,
        ModelCache model,
        Dictionary<ISymbol, bool> cache
    ) {
        if (field.IsConst
            || field.IsReadOnly
            || field.IsImplicitlyDeclared
            || field.DeclaredAccessibility != Accessibility.Private
            || !SymbolEqualityComparer.Default.Equals(field.ContainingType, owner)) {
            return;
        }

        if (!cache.TryGetValue(field, out var reassigned)) {
            cache[field] = reassigned = IsReassigned(field, bodies, model, context.CancellationToken);
        }

        if (reassigned) {
            Report(
                context,
                statement.Expression,
                "`"
                + field.Name
                + "` is assigned outside a constructor, so a thread that entered before that assignment "
                + "and one that enters after hold different monitors"
            );
        }
    }

    /// <summary>Whether the type assigns the field anywhere but a constructor or its own initializer.</summary>
    static bool IsReassigned(
        IFieldSymbol field,
        IReadOnlyList<SyntaxNode> bodies,
        ModelCache model,
        CancellationToken cancellation
    ) {
        foreach (var node in bodies) {
            if (node is not SimpleNameSyntax name
                || !string.Equals(name.Identifier.ValueText, field.Name, StringComparison.Ordinal)
                || !SymbolEqualityComparer.Default.Equals(
                    model.Of(name).GetSymbolInfo(name, cancellation).Symbol,
                    field
                )) {
                continue;
            }

            // ⚠ Any receiver, not only `this.` — a private static gate is written `Meter.gate = new()`
            // as often as bare, and the symbol has already been matched, so what is to the left of the
            // dot cannot change the answer. Missing one write here is a wrong *finding*, not a missing
            // one: the rule would call a field constant that the type reassigns.
            var reference = name.Parent is MemberAccessExpressionSyntax access && access.Name == name
                ? access
                : (ExpressionSyntax)name;

            if (!IsWrite(reference)) {
                continue;
            }

            if (EnclosingMember(reference) is null or ConstructorDeclarationSyntax or FieldDeclarationSyntax) {
                continue;
            }

            return true;
        }

        return false;
    }

    static string Created(string name) =>
        "`"
        + name
        + "` is created in this method, so every call locks a different object and this `lock` excludes nothing";

    static void Report(SymbolAnalysisContext context, ExpressionSyntax expression, string message) =>
        context.ReportDiagnostic(Diagnostic.Create(Descriptor, expression.GetLocation(), message));

    static bool IsPrimitive(INamedTypeSymbol type, ImmutableArray<INamedTypeSymbol> primitives) {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType) {
            var definition = current.OriginalDefinition;
            foreach (var primitive in primitives) {
                if (SymbolEqualityComparer.Default.Equals(definition, primitive)) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The nearest body that runs per call: a lambda, a local function, an accessor, a member.</summary>
    /// <remarks>
    ///     ⚠ <c>GlobalStatementSyntax</c> is matched before <c>MemberDeclarationSyntax</c> because it
    ///     <em>is</em> one, and every top-level statement is a separate global statement — without the
    ///     earlier case each statement would be its own "function" and a local declared on one line
    ///     would never share a body with the <c>lock</c> on the next.
    /// </remarks>
    static SyntaxNode? EnclosingFunction(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                case AccessorDeclarationSyntax:
                    return current;
                case GlobalStatementSyntax global:
                    return global.Parent;
                case MemberDeclarationSyntax:
                    return current;
            }
        }

        return null;
    }

    static MemberDeclarationSyntax? EnclosingMember(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            if (current is MemberDeclarationSyntax member) {
                return member;
            }
        }

        return null;
    }

    static bool IsInsideClosure(SyntaxNode node, SyntaxNode function) {
        for (var current = node.Parent; current is not null && current != function; current = current.Parent) {
            if (current is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax) {
                return true;
            }
        }

        return false;
    }

    /// <summary>The write shape of <c>InconsistentlySynchronizedFieldAnalyzer</c>, unchanged.</summary>
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

    /// <summary>Every node of a type declaration that belongs to <em>this</em> type.</summary>
    /// <remarks>
    ///     ⚠ A nested type's declarations are descendants of the outer one's. Walking into them would
    ///     count a nested type's <c>lock</c> statements and its writes as this type's, and the two
    ///     mistakes point opposite ways — a nested lock would be examined against the outer type's
    ///     fields, and the nested type gets its own symbol action anyway.
    /// </remarks>
    static IEnumerable<SyntaxNode> Own(TypeDeclarationSyntax declaration) =>
        declaration.DescendantNodes(node => node == declaration || node is not TypeDeclarationSyntax);

    /// <summary>One semantic model per tree, for the life of one type's analysis.</summary>
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
