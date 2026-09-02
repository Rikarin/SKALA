using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3062</c> — a constructor publishes <c>this</c> before the object is finished.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime".
///     A constructor body runs before the derived constructors, before the readonly fields are
///     guaranteed visible to another thread under a weak memory model, and before the caller has done
///     anything with the finished object. Handing <c>this</c> out from inside it gives somebody a
///     reference to an object that is not there yet, and on ARM64 the second thread may legally
///     observe a field before its initializer — the failure that never reproduces on x64.
///     <para>
///         ⚠ <b>The design constraint that decides this rule is what it refuses to report.</b>
///         <c>button.Click += OnClick;</c> in a constructor is the overwhelmingly common
///         <em>legitimate</em> shape — nearly every UI type ever written contains it — and a rule that
///         reports it is worse than no rule at all, because it would be dismissed wholesale and take
///         the four real shapes with it. So the rule reports only publication whose
///         <em>
///             second
///             reader is outside the constructor's control
///         </em>: process-wide static state, or a thread
///         the constructor itself starts. Everything else is declined, and the negative fixture set,
///         the four real shapes with it. So the rule reports only publication whose second reader is
///         <em>outside the constructor's control</em>: process-wide static state, or a thread the
///         constructor itself starts. Everything else is declined, and the negative fixture set,
///         which is more than twice the size of the positive one, is where that promise is kept.
///     </para>
///     <list type="number">
///         <item>
///             <b>A</b> — <c>Other.Current = this;</c>, where the left side binds to a
///             <c>static</c> field or property. ⚠ The static member must belong to some
///             <em>other</em> type; see <see cref="StoredInStaticState" /> for why.
///         </item>
///         <item>
///             <b>B</b> — <c>Registry.Instances.Add(this);</c>: the bare <c>this</c> handed as an
///             argument to a call whose <em>receiver</em> is a static field or property. A static
///             <em>method</em> that merely reads its argument is not publication and is not reported.
///         </item>
///         <item>
///             <b>C</b> — a <c>+=</c> onto an event that outlives the object: a static event, or an
///             instance event reached through a static field or property
///             (<c>AppDomain.CurrentDomain.ProcessExit</c>).
///         </item>
///         <item>
///             <b>D</b> — a delegate that reaches <c>this</c> handed to <c>Task.Run</c>,
///             <c>Task.Factory.StartNew</c>, <c>ThreadPool.QueueUserWorkItem</c>, or a
///             <c>Thread</c> created <em>and</em> started inside the same constructor.
///         </item>
///     </list>
///     <para>
///         ⚠ <b>Shape A stops at the constructor's own type, and the boundary is <c>SK2134</c>.</b>
///         <c>current = this;</c> in a constructor, writing this type's own static field, is the
///         canonical shape of <c>instance-write-to-static</c> and is already reported there. Two rules
///         on one line is a double-report, and the reader who has to decide which of two findings to
///         act on acts on neither. <c>SK3062/negative/the-own-types-static-field.cs</c> is what holds
///         the exclusion.
///     </para>
///     <para>
///         Report-only. Every repair — a factory method that constructs and then publishes, a separate
///         <c>Start</c>, a deferred subscription — moves code out of the constructor into a place the
///         rule cannot choose, because which caller becomes responsible for the second step is a design
///         decision the constructor does not contain.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstructorPublishesThisAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ConstructorPublishesThis);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // ⚠ The four framework types are resolved once per compilation, never matched on the written
        // name: `Thread` and `Task` are both plausible names for somebody's own type, and a finding
        // on one of those sends a reader to threading code that does not exist.
        context.RegisterCompilationStartAction(static start => {
                var starters = new Starters(start.Compilation);
                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, starters),
                    SyntaxKind.ConstructorDeclaration
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, Starters starters) {
        var declaration = (ConstructorDeclarationSyntax)context.Node;

        // A static constructor has no `this` to publish, and the initializer of a primary constructor
        // is not a `ConstructorDeclarationSyntax` at all, so neither reaches here.
        if (declaration.Modifiers.Any(SyntaxKind.StaticKeyword)) {
            return;
        }

        // ⚠ The cheap gate, and it is load-bearing rather than tidy: everything below binds symbols,
        // and a constructor that assigns its parameters to its fields — which is most constructors —
        // can never produce a finding. None of the four shapes exists without a `this` expression, a
        // `+=`, or an invocation somewhere in the declaration.
        var interesting = false;
        foreach (var node in declaration.DescendantNodes()) {
            if (node is ThisExpressionSyntax or InvocationExpressionSyntax
                || node.IsKind(SyntaxKind.AddAssignmentExpression)) {
                interesting = true;

                break;
            }
        }

        if (!interesting) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
            is not { ContainingType: { } owner }) {
            return;
        }

        foreach (var node in declaration.DescendantNodes()) {
            switch (node) {
                case AssignmentExpressionSyntax assignment
                    when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression):
                    StoredInStaticState(context, assignment, owner);

                    break;
                case AssignmentExpressionSyntax assignment when assignment.IsKind(SyntaxKind.AddAssignmentExpression):
                    SubscribedToALongLivedEvent(context, assignment);

                    break;
                case InvocationExpressionSyntax invocation:
                    HandedToStaticState(context, invocation);
                    StartedOnAnotherThread(context, invocation, declaration, owner, starters);

                    break;
            }
        }
    }

    /// <summary>
    ///     Shape A — <c>Other.Current = this;</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The containing-type exclusion is the whole subtlety.</b> <c>SK2134</c>
    ///     (<c>instance-write-to-static</c>) reports exactly <c>current = this;</c> in a constructor and
    ///     calls it the canonical shape of its own concept, so admitting the own type here would put two
    ///     findings on one line. The exclusion costs nothing in coverage: a type storing itself in its
    ///     own static slot is already told about it, in the vocabulary of the rule that owns that shape.
    /// </remarks>
    static void StoredInStaticState(
        SyntaxNodeAnalysisContext context,
        AssignmentExpressionSyntax assignment,
        INamedTypeSymbol owner
    ) {
        if (assignment.Right is not ThisExpressionSyntax self) {
            return;
        }

        var target = context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol;
        if (target is not (IFieldSymbol { IsStatic: true } or IPropertySymbol { IsStatic: true })
            || target.ContainingType is not { } holder
            || SymbolEqualityComparer.Default.Equals(holder.OriginalDefinition, owner.OriginalDefinition)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                self.GetLocation(),
                "the constructor stores `this` in `"
                + holder.Name
                + "."
                + target.Name
                + "`, static state that outlives every instance, so a half-built object is readable from "
                + "any thread before this constructor returns"
            )
        );
    }

    /// <summary>
    ///     Shape B — <c>Registry.Instances.Add(this);</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The receiver, not the method.</b> <c>Validate(this)</c>,
    ///     <c>Console.WriteLine(this)</c> and <c>ArgumentNullException.ThrowIfNull(this)</c> all hand
    ///     <c>this</c> to a static method, and none of them publishes anything: the argument is read and
    ///     dropped. Reporting them would bury the shape that matters under the shape that does not, so
    ///     the gate is that the call's receiver expression is itself static state — an object which,
    ///     because it outlives every instance, keeps whatever it is given.
    ///     <para>
    ///         The same gate declines <c>list.Add(this)</c> on a local, a parameter or an instance
    ///         field: that object's lifetime is not longer than this one's.
    ///     </para>
    /// </remarks>
    static void HandedToStaticState(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation) {
        if (invocation.Expression is not MemberAccessExpressionSyntax access) {
            return;
        }

        // Syntax before symbols: most calls in a constructor do not pass a bare `this` at all.
        ThisExpressionSyntax? self = null;
        foreach (var argument in invocation.ArgumentList.Arguments) {
            if (argument.Expression is ThisExpressionSyntax candidate) {
                self = candidate;

                break;
            }
        }

        if (self is null || StaticRoot(context, access.Expression) is not { } root) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                self.GetLocation(),
                "the constructor hands `this` to `"
                + (root.ContainingType?.Name ?? "?")
                + "."
                + root.Name
                + "`, which is static and therefore keeps the reference after this constructor has "
                + "returned — and while it is still running"
            )
        );
    }

    /// <summary>
    ///     Shape C — a handler subscribed to an event that outlives the object.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>This is the method that decides whether the rule is usable.</b> The single most common
    ///     line in any constructor that touches an event is <c>button.Click += OnClick;</c>, and it is
    ///     correct: the event's owner is a field of this object, or a parameter, or a local, and it does
    ///     not outlive what subscribed to it. Only two receivers qualify — a <c>static</c> event, and an
    ///     instance event reached through static state — because only those two are still raising after
    ///     everybody who could unsubscribe has forgotten about the object.
    ///     <para>
    ///         ⚠ <c>this.Something += Handler;</c> is declined by the same gate rather than by a
    ///         special case: <c>this</c> is not static state, and an object subscribing to its own event
    ///         has published nothing.
    ///     </para>
    /// </remarks>
    static void SubscribedToALongLivedEvent(SyntaxNodeAnalysisContext context, AssignmentExpressionSyntax assignment) {
        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol
            is not IEventSymbol published) {
            return;
        }

        string where;
        if (published.IsStatic) {
            where = "the static event `" + (published.ContainingType?.Name ?? "?") + "." + published.Name + "`";
        } else if (assignment.Left is MemberAccessExpressionSyntax access
                   && StaticRoot(context, access.Expression) is { } root) {
            where = "`"
                + published.Name
                + "` on `"
                + (root.ContainingType?.Name ?? "?")
                + "."
                + root.Name
                + "`, which is static";
        } else {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                assignment.GetLocation(),
                // ⚠ Not "before this constructor returns". The subscription is often the last
                // statement of a sealed type's constructor, where that claim is not true and a reader
                // who checks will stop believing the rest of the message. What is true everywhere is
                // the escape itself: the object is reachable from state that outlives it before the
                // caller has been given the reference, so nobody can undo the subscription if a later
                // initializer throws, and any thread can raise the event from then on.
                "the constructor subscribes to "
                + where
                + ", so this object is reachable from an event that outlives it before the caller "
                + "has the reference to unsubscribe it"
            )
        );
    }

    /// <summary>
    ///     Shape D — a delegate that reaches <c>this</c>, started on another thread.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b><c>new Thread(Work)</c> alone is not a finding.</b> A thread constructed in one place
    ///     and started in another is the ordinary shape of a type that owns a worker, and nothing races
    ///     until somebody calls <c>Start</c>. The rule requires the <c>Start()</c> to be in the same
    ///     constructor and then looks back for where that thread was made, so the field-and-start-later
    ///     shape is declined by construction rather than by a filter.
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             And starting a thread as the last act of a <c>sealed</c> type's constructor is not
    ///             a finding either, because there is nothing left to race.
    ///         </b> This gate exists because the
    ///         first draft reported <c>VideoPlayer</c> on the reference tree and that finding was
    ///         <em>wrong</em>: <c>Thread.Start</c>, <c>Task.Run</c> and <c>QueueUserWorkItem</c> all
    ///         publish a memory barrier, so everything the constructor wrote before them is visible to
    ///         the new thread. The hazard needs something that still changes afterwards — a statement
    ///         following the start, or a derived constructor, which a sealed type cannot have. ⚠ This
    ///         is the one gate in this rule that shapes A, B and C deliberately do <em>not</em> share:
    ///         storing a reference in static state is not a barrier and buys no ordering, and the defect
    ///         there is that the object is reachable at all rather than that it is unfinished.
    ///     </para>
    /// </remarks>
    static void StartedOnAnotherThread(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ConstructorDeclarationSyntax declaration,
        INamedTypeSymbol owner,
        Starters starters
    ) {
        if (!StillUnderConstruction(invocation, declaration, owner)) {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol { ContainingType: { } container } method) {
            return;
        }

        var definition = container.OriginalDefinition;
        if (Is(definition, starters.Thread) && method.Name is "Start") {
            if (CreationOf(context, invocation, declaration) is { ArgumentList: { } made }) {
                Escapes(context, made.Arguments, invocation, owner, "the `Thread` it starts here");
            }

            return;
        }

        var scheduler = Is(definition, starters.Task) && method.Name is "Run" ? "`Task.Run`"
            : Is(definition, starters.TaskFactory) && method.Name is "StartNew" ? "`Task.Factory.StartNew`"
                : Is(definition, starters.ThreadPool) && method.Name is "QueueUserWorkItem"
                    ? "`ThreadPool.QueueUserWorkItem`"
                    : null;

        if (scheduler is not null) {
            Escapes(context, invocation.ArgumentList.Arguments, invocation, owner, scheduler);
        }
    }

    /// <summary>
    ///     Whether anything can still change this object after the thread start at <paramref name="start" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two ways, and both have to be absent before the rule stays quiet. A type that is not
    ///     <c>sealed</c> has a derived constructor that runs after this one, so the object keeps being
    ///     written wherever in the body the start sits. And a statement that follows the start — at any
    ///     enclosing level up to the constructor body, so a start at the end of an <c>if</c> block that
    ///     is itself the last statement counts as following nothing — is the constructor still writing
    ///     after it has published.
    /// </remarks>
    static bool StillUnderConstruction(
        ExpressionSyntax start,
        ConstructorDeclarationSyntax declaration,
        INamedTypeSymbol owner
    ) {
        if (!owner.IsSealed) {
            return true;
        }

        for (SyntaxNode? current = start; current is not null && current != declaration; current = current.Parent) {
            if (current is not StatementSyntax statement || statement.Parent is not BlockSyntax block) {
                continue;
            }

            var index = block.Statements.IndexOf(statement);
            if (index >= 0 && index < block.Statements.Count - 1) {
                return true;
            }
        }

        return false;
    }

    static void Escapes(
        SyntaxNodeAnalysisContext context,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol owner,
        string what
    ) {
        foreach (var argument in arguments) {
            if (!Reaches(context, argument.Expression, owner)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    invocation.GetLocation(),
                    "the constructor gives "
                    + what
                    + " a delegate that reaches `this`, so a second thread reads this object while the "
                    + "rest of the constructor is still writing it"
                )
            );

            return;
        }
    }

    /// <summary>
    ///     The <c>new Thread(…)</c> that produced the receiver of a <c>Start()</c>, if it is in this
    ///     same constructor.
    /// </summary>
    /// <remarks>
    ///     Three spellings reach the same place: <c>new Thread(Work).Start()</c>, a local declared and
    ///     started, and a field assigned and started. Anything else — a thread handed in as a parameter,
    ///     or one made in another member — returns <c>null</c> and the rule stays silent, because the
    ///     delegate it was given is not visible from here.
    /// </remarks>
    static ObjectCreationExpressionSyntax? CreationOf(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ConstructorDeclarationSyntax declaration
    ) {
        if (invocation.Expression is not MemberAccessExpressionSyntax { Expression: { } receiver }) {
            return null;
        }

        if (receiver is ObjectCreationExpressionSyntax direct) {
            return direct;
        }

        if (context.SemanticModel.GetSymbolInfo(receiver, context.CancellationToken).Symbol is not { } thread) {
            return null;
        }

        foreach (var node in declaration.DescendantNodes()) {
            switch (node) {
                case VariableDeclaratorSyntax { Initializer.Value: ObjectCreationExpressionSyntax declared }
                    when SymbolEqualityComparer.Default.Equals(
                        context.SemanticModel.GetDeclaredSymbol(node, context.CancellationToken),
                        thread
                    ):
                    return declared;
                case AssignmentExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                    Left: { } target,
                    Right: ObjectCreationExpressionSyntax assigned
                } when SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetSymbolInfo(target, context.CancellationToken).Symbol,
                    thread
                ):
                    return assigned;
            }
        }

        return null;
    }

    /// <summary>
    ///     Whether a delegate argument reaches <c>this</c>: a closure that mentions <c>this</c> or any
    ///     instance member of the containing type, or a method group naming an instance method of it.
    /// </summary>
    /// <remarks>
    ///     ⚠ A lambda over locals, parameters and statics captures nothing and is the shape this test
    ///     exists to let through — <c>Task.Run(() =&gt; Compute(size))</c> starts a thread and publishes
    ///     no object, and reporting it would make the rule a complaint about starting work.
    ///     <para>
    ///         ⚠ A local function is deliberately not treated as an instance member. Its
    ///         <c>ContainingType</c> is this type and its <c>IsStatic</c> is false whether or not it
    ///         touches anything, so admitting it would report every <c>Task.Run(() =&gt; Local())</c>
    ///         including the ones that capture only locals. The cost is a missed finding where the local
    ///         function does read a field; the alternative is a false positive on a shape that is common
    ///         and correct, and docs/plan/16 § R3 prices those very differently.
    ///     </para>
    /// </remarks>
    static bool Reaches(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, INamedTypeSymbol owner) {
        if (expression is AnonymousFunctionExpressionSyntax closure) {
            foreach (var node in closure.DescendantNodes()) {
                if (node is ThisExpressionSyntax) {
                    return true;
                }

                if (node is SimpleNameSyntax name && IsOwnInstanceMember(context, name, owner)) {
                    return true;
                }
            }

            return false;
        }

        var group = expression switch {
            IdentifierNameSyntax identifier => identifier,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: { } name } => name,
            _ => null
        };

        return group is not null && IsOwnInstanceMember(context, group, owner);
    }

    static bool IsOwnInstanceMember(
        SyntaxNodeAnalysisContext context,
        SimpleNameSyntax name,
        INamedTypeSymbol owner
    ) {
        // ⚠ `CandidateSymbols` too, and not for tidiness: a method group in an argument position that
        // Roslyn could not narrow to one overload has a null `Symbol`, and reading only `Symbol` would
        // silently let every overloaded instance handler through.
        var info = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken);
        var symbol = info.Symbol ?? (info.CandidateSymbols.Length > 0 ? info.CandidateSymbols[0] : null);

        if (symbol is null
            || symbol.IsStatic
            || symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction }
            || symbol is not (IFieldSymbol or IPropertySymbol or IMethodSymbol or IEventSymbol)) {
            return false;
        }

        for (INamedTypeSymbol? type = owner; type is not null; type = type.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(
                    symbol.ContainingType?.OriginalDefinition,
                    type.OriginalDefinition
                )) {
                return true;
            }
        }

        return false;
    }

    /// <summary>The symbol an expression names, when that is a static field or a static property.</summary>
    static ISymbol? StaticRoot(SyntaxNodeAnalysisContext context, ExpressionSyntax expression) =>
        context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol switch {
            IFieldSymbol { IsStatic: true } field => field,
            IPropertySymbol { IsStatic: true } property => property,
            _ => null
        };

    static bool Is(INamedTypeSymbol type, INamedTypeSymbol? resolved) =>
        resolved is not null && SymbolEqualityComparer.Default.Equals(type, resolved);

    /// <summary>
    ///     The four ways a constructor hands work to another thread, resolved once per compilation.
    /// </summary>
    /// <remarks>
    ///     ⚠ Every one of them may be <c>null</c> and the rule must survive that. A compilation without
    ///     a reference to <c>System.Threading.Thread</c> is not hypothetical — a source generator's or a
    ///     script's compilation is routinely that thin — and shapes A, B and C still work there, so a
    ///     bail-out when the set is empty would silently withdraw three quarters of the rule.
    /// </remarks>
    sealed class Starters {
        public Starters(Compilation compilation) {
            Task = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            TaskFactory = compilation.GetTypeByMetadataName("System.Threading.Tasks.TaskFactory");
            ThreadPool = compilation.GetTypeByMetadataName("System.Threading.ThreadPool");
            Thread = compilation.GetTypeByMetadataName("System.Threading.Thread");
        }

        public INamedTypeSymbol? Task { get; }

        public INamedTypeSymbol? TaskFactory { get; }

        public INamedTypeSymbol? ThreadPool { get; }

        public INamedTypeSymbol? Thread { get; }
    }
}
