using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2200</c> — a field initializer no allocation ever keeps.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK2200 — events, delegates and effects that do not happen".
///     Two values are written down for one field and only one of them is ever true.
///     <para>
///         ⚠
///         <b>
///             The initialized value is live from the field initializer until the overwrite, and
///             anything that runs code in that window can read it (#431).
///         </b> Field initializers run
///         <em>before</em> the base constructor call, so the window spans every base constructor body
///         up to <c>object</c>'s, then the constructor's own statements up to the write, then the value
///         the write computes. #412's audit measured three readers a name scan cannot see — an override
///         reaching the field through a helper, a base constructor calling an interface on
///         <c>this</c>, a getter read before the write — and #431 found two more: the overwrite's own
///         value calling a method that reads the field, and a static getter reading an instance a base
///         constructor stored. So nothing in the window may run code at all —
///         <see cref="RewriteGuards.RunsNoOtherCode(IOperation)" />, read off the operations — and a base
///         constructor the rule cannot read, from metadata, closes it.
///     </para>
///     <para>
///         ⚠ <b>A construction that fails inside the window is a reader too.</b> The initialized value
///         outlives the exception, and a finalizer or a <c>this</c> stored during the window reads it —
///         measured <c>5</c> before the fix and <c>0</c> after, both ways. So where either is possible,
///         nothing in the window may throw.
///     </para>
///     <para>
///         ⚠ That needs every constructor body in the compilation, base types' included, and an
///         operation action can only bind its own tree (RS1030). So the rule summarizes each
///         constructor body as it is visited and decides at the end of the compilation, which is why
///         its scope is <c>Compilation</c> rather than <c>Semantic</c>: a base class edited in another
///         file changes the verdict here.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OverwrittenFieldInitializerAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.OverwrittenFieldInitializer);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var facts = new Facts();
                start.RegisterOperationAction(context => Summarize(context, facts), OperationKind.ConstructorBody);
                start.RegisterOperationAction(
                    context => Collect(context, facts),
                    OperationKind.FieldInitializer,
                    OperationKind.PropertyInitializer
                );
                start.RegisterCompilationEndAction(context => {
                        foreach (var candidate in facts.Candidates) {
                            if (IsDead(candidate.Field, facts)) {
                                context.ReportDiagnostic(
                                    Diagnostic.Create(
                                        Descriptor,
                                        candidate.Location,
                                        FixEdits.Pack((candidate.Edit, string.Empty)),
                                        "the value given to `"
                                        + candidate.Field.Name
                                        + "` here is overwritten by every constructor"
                                    )
                                );
                            }
                        }
                    }
                );
            }
        );
    }

    /// <summary>What the compilation's constructors and initializers do, gathered for the end action.</summary>
    sealed class Facts {
        public ConcurrentDictionary<IMethodSymbol, Summary> Constructors { get; } =
            new(SymbolEqualityComparer.Default);

        /// <summary>
        ///     Per type, the members whose instance initializer runs code or may throw. Every instance
        ///     initializer runs inside some candidate's window — partial declarations leave their order
        ///     unspecified — so a candidate counts all but its own.
        /// </summary>
        public ConcurrentDictionary<INamedTypeSymbol, ConcurrentBag<ISymbol>> RiskyInitializers { get; } =
            new(SymbolEqualityComparer.Default);

        public ConcurrentBag<Candidate> Candidates { get; } = new();
    }

    /// <summary>What one constructor body does, as far as the window before an overwrite is concerned.</summary>
    sealed class Summary {
        public Summary(
            bool runsNoCode,
            bool mayThrow,
            bool leaks,
            IMethodSymbol? next,
            Dictionary<IFieldSymbol, bool> overwritten
        ) {
            RunsNoCode = runsNoCode;
            MayThrow = mayThrow;
            Leaks = leaks;
            Next = next;
            Overwritten = overwritten;
        }

        /// <summary>Whether the body, apart from its constructor initializer, runs no code.</summary>
        public bool RunsNoCode { get; }

        /// <summary>Whether the body or the constructor initializer's arguments may throw.</summary>
        public bool MayThrow { get; }

        /// <summary>Whether the body hands <c>this</c> on as a value rather than using it as a receiver.</summary>
        public bool Leaks { get; }

        /// <summary>The constructor it calls first, or <c>null</c> when the rule could not tell.</summary>
        public IMethodSymbol? Next { get; }

        /// <summary>
        ///     The fields the body overwrites at its top level before anything in it runs code or touches
        ///     them, each with whether something before the write — or the written value — may throw.
        /// </summary>
        public Dictionary<IFieldSymbol, bool> Overwritten { get; }
    }

    sealed class Candidate {
        public Candidate(IFieldSymbol field, Location location, TextSpan edit) {
            Field = field;
            Location = location;
            Edit = edit;
        }

        public IFieldSymbol Field { get; }

        public Location Location { get; }

        public TextSpan Edit { get; }
    }

    static void Summarize(OperationAnalysisContext context, Facts facts) {
        if (context.ContainingSymbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor) {
            return;
        }

        var body = (IConstructorBodyOperation)context.Operation;
        var initializer = body.Initializer is IExpressionStatementOperation statement
            ? statement.Operation
            : body.Initializer;
        var mayThrow = false;
        if (initializer is IInvocationOperation invocation) {
            foreach (var argument in invocation.Arguments) {
                mayThrow |= !RewriteGuards.IsFreeToSkip(argument.Value);
            }
        }

        var runsNoCode = true;
        var leaks = false;
        foreach (var part in new IOperation?[] { body.BlockBody, body.ExpressionBody }) {
            if (part is null) {
                continue;
            }

            runsNoCode &= RewriteGuards.RunsNoOtherCode(part);
            mayThrow |= !RewriteGuards.RunsNoOtherCode(part, false);
            leaks |= Leaks(part);
        }

        facts.Constructors[constructor.OriginalDefinition] = new(
            runsNoCode,
            mayThrow,
            leaks,
            Next(constructor, initializer),
            Overwritten(body.BlockBody ?? body.ExpressionBody)
        );
    }

    /// <summary>
    ///     ⚠ Whether <c>this</c> is used as a value — stored, passed or converted — rather than as the
    ///     receiver of a member. A stored <c>this</c> outlives a constructor that throws, and whoever
    ///     holds it reads the initialized value.
    /// </summary>
    static bool Leaks(IOperation operation) {
        foreach (var descendant in operation.DescendantsAndSelf()) {
            if (descendant is IInstanceReferenceOperation
                && descendant.Parent is not (IFieldReferenceOperation
                    or IPropertyReferenceOperation
                    or IInvocationOperation
                    or IEventReferenceOperation
                    or IMethodReferenceOperation)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     The constructor this one calls before its body: the one its initializer names, or the
    ///     implicit <c>base()</c>.
    /// </summary>
    static IMethodSymbol? Next(IMethodSymbol constructor, IOperation? initializer) {
        if (initializer is IInvocationOperation { TargetMethod: { MethodKind: MethodKind.Constructor } target }) {
            return target.OriginalDefinition;
        }

        return initializer is null ? ImplicitBase(constructor.ContainingType) : null;
    }

    /// <summary>
    ///     The base constructor an implicit <c>base()</c> binds to — the one that can be called with no
    ///     arguments, when there is exactly one.
    /// </summary>
    static IMethodSymbol? ImplicitBase(INamedTypeSymbol type) {
        if (type.BaseType is not { } baseType) {
            return null;
        }

        IMethodSymbol? found = null;
        foreach (var candidate in baseType.InstanceConstructors) {
            if (candidate.Parameters.All(static parameter => parameter.IsOptional || parameter.IsParams)) {
                if (found is not null) {
                    return null;
                }

                found = candidate;
            }
        }

        return found?.OriginalDefinition;
    }

    /// <summary>
    ///     The fields the body's top-level statements overwrite while everything before the write — and
    ///     the written value — runs no code and leaves the field alone.
    /// </summary>
    static Dictionary<IFieldSymbol, bool> Overwritten(IBlockOperation? block) {
        var overwritten = new Dictionary<IFieldSymbol, bool>(SymbolEqualityComparer.Default);
        if (block is null) {
            return overwritten;
        }

        var touched = new HashSet<IFieldSymbol>(SymbolEqualityComparer.Default);
        var mayThrow = false;
        foreach (var statement in block.Operations) {
            if (statement is IExpressionStatementOperation {
                    Operation:
                    ISimpleAssignmentOperation {
                        Target:
                        IFieldReferenceOperation {
                            Instance:
                            IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance }
                        } target
                    } assignment
                }) {
                // ⚠ The written value runs inside the window too: `value = Twice()` with
                // `Twice() => value * 2` doubled the initialized value, measured `6` → `0`.
                if (!RewriteGuards.RunsNoOtherCode(assignment.Value)) {
                    return overwritten;
                }

                mayThrow |= !RewriteGuards.RunsNoOtherCode(assignment.Value, false);
                var field = target.Field.OriginalDefinition;
                Touch(assignment.Value, touched);
                if (touched.Add(field)) {
                    overwritten[field] = mayThrow;
                }

                continue;
            }

            if (!RewriteGuards.RunsNoOtherCode(statement)) {
                return overwritten;
            }

            mayThrow |= !RewriteGuards.RunsNoOtherCode(statement, false);
            Touch(statement, touched);
        }

        return overwritten;
    }

    static void Touch(IOperation operation, HashSet<IFieldSymbol> touched) {
        foreach (var descendant in operation.DescendantsAndSelf()) {
            if (descendant is IFieldReferenceOperation reference) {
                touched.Add(reference.Field.OriginalDefinition);
            }
        }
    }

    static void Collect(OperationAnalysisContext context, Facts facts) {
        var initializer = (ISymbolInitializerOperation)context.Operation;
        var initialized = initializer switch {
            IFieldInitializerOperation fields => fields.InitializedFields.CastArray<ISymbol>(),
            IPropertyInitializerOperation properties => properties.InitializedProperties.CastArray<ISymbol>(),
            _ => ImmutableArray<ISymbol>.Empty
        };

        if (initialized.IsEmpty || initialized[0].IsStatic) {
            return;
        }

        var free = RewriteGuards.IsFreeToSkip(initializer.Value);
        if (!free) {
            foreach (var symbol in initialized) {
                facts.RiskyInitializers.GetOrAdd(
                        symbol.ContainingType.OriginalDefinition,
                        static _ => new ConcurrentBag<ISymbol>()
                    )
                    .Add(symbol.OriginalDefinition);
            }
        }

        if (initialized.Length != 1
            || initialized[0] is not IFieldSymbol {
                IsConst: false, IsImplicitlyDeclared: false, DeclaredAccessibility: Accessibility.Private
            } field) {
            return;
        }

        var type = field.ContainingType;
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct) || type.IsRecord) {
            return;
        }

        // ⚠ The fix deletes the initializer, so its evaluation must be free to skip: no getter, no
        // user-defined conversion or operator, and no throw (#423). `IsSideEffectFree`, the syntactic
        // test this replaced, admitted `Counter.Next` and `(W)4` as a name path and a cast, and the
        // audit measured both running before the fix and not after.
        if (!free
            || field.DeclaringSyntaxReferences.Length != 1
            || field.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken)
            is not VariableDeclaratorSyntax { Initializer: { } value } declarator) {
            return;
        }

        var span = TextSpan.FromBounds(declarator.Identifier.Span.End, value.Span.End);
        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(declarator.SyntaxTree, span)) {
            return;
        }

        facts.Candidates.Add(new(field, value.GetLocation(), span));
    }

    /// <summary>
    ///     Whether every constructor that runs the field's initializer overwrites it before anything
    ///     could read the initialized value.
    /// </summary>
    /// <remarks>
    ///     ⚠ C# runs field initializers in every constructor that does not chain to <c>this(…)</c>, so a
    ///     chaining constructor is evidence of nothing and is skipped rather than counted as a witness
    ///     or as a counterexample. A primary constructor, a record's copy constructor and any other
    ///     implicitly declared one stop the walk for the whole type: their assignments are not
    ///     constructor statements, and guessing at them is how this rule would delete a live value.
    ///     <para>
    ///         ⚠ A construction that throws inside the window leaves the initialized value behind, and
    ///         two things read it afterwards: a finalizer, and a <c>this</c> the window stored somewhere.
    ///         So when either is possible — a destructor anywhere up the chain, a class that is not
    ///         sealed and so may gain one, or a leak — nothing in the window may throw either: no other
    ///         instance initializer, constructor-initializer argument, base body, statement before the
    ///         write, or written value.
    ///     </para>
    /// </remarks>
    static bool IsDead(IFieldSymbol field, Facts facts) {
        var type = field.ContainingType;
        var running = 0;
        var mayThrow = OtherInitializersMayThrow(type, field, facts);
        var observable = MayBeFinalized(type);
        foreach (var constructor in type.InstanceConstructors) {
            if (constructor.IsImplicitlyDeclared
                || constructor.DeclaringSyntaxReferences.Length != 1
                || constructor.DeclaringSyntaxReferences[0].GetSyntax()
                    is not ConstructorDeclarationSyntax declaration
                || !facts.Constructors.TryGetValue(constructor.OriginalDefinition, out var summary)) {
                return false;
            }

            if (declaration.Initializer.IsKind(SyntaxKind.ThisConstructorInitializer)) {
                continue;
            }

            running++;
            if (!summary.Overwritten.TryGetValue(field.OriginalDefinition, out var throwsBefore)) {
                return false;
            }

            mayThrow |= throwsBefore || summary.MayThrow;
            observable |= summary.Leaks;
            if (type.TypeKind == TypeKind.Class
                && !BaseChainRunsNoCode(summary.Next, facts, ref mayThrow, ref observable)) {
                return false;
            }
        }

        return running > 0 && !(mayThrow && observable);
    }

    static bool OtherInitializersMayThrow(INamedTypeSymbol type, IFieldSymbol self, Facts facts) {
        for (var current = type; current is not null; current = current.BaseType) {
            if (facts.RiskyInitializers.TryGetValue(current.OriginalDefinition, out var risky)
                && risky.Any(symbol => !SymbolEqualityComparer.Default.Equals(symbol, self.OriginalDefinition))) {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a finalizer could run on an instance whose construction failed, and read the field.</summary>
    static bool MayBeFinalized(INamedTypeSymbol type) {
        if (type.TypeKind == TypeKind.Struct) {
            return false;
        }

        if (!type.IsSealed) {
            return true;
        }

        for (var current = type;
             current is { SpecialType: not SpecialType.System_Object };
             current = current.BaseType) {
            if (current.GetMembers()
                    .Any(static member => member is IMethodSymbol { MethodKind: MethodKind.Destructor })) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether the constructor chain from <paramref name="constructor" /> up to <c>object</c>'s runs
    ///     no code — so nothing between the field initializer and the derived constructor body can
    ///     reach the instance — accumulating whether any of it may throw or leak <c>this</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Declines a constructor from metadata, which the rule cannot read. A framework base
    ///     constructor is free to call a virtual member, and WinForms' do.
    /// </remarks>
    static bool BaseChainRunsNoCode(
        IMethodSymbol? constructor,
        Facts facts,
        ref bool mayThrow,
        ref bool observable
    ) {
        var seen = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        while (constructor is not null) {
            if (!seen.Add(constructor)) {
                return false;
            }

            if (constructor.ContainingType.SpecialType == SpecialType.System_Object) {
                return true;
            }

            if (constructor.IsImplicitlyDeclared
                && constructor.ContainingType.DeclaringSyntaxReferences.Length > 0
                && !constructor.ContainingType.IsRecord) {
                // A default constructor the compiler wrote over a source type: it runs that type's
                // initializers, which cannot name `this`, and then the implicit `base()`.
                constructor = ImplicitBase(constructor.ContainingType);
                continue;
            }

            if (!facts.Constructors.TryGetValue(constructor, out var summary) || !summary.RunsNoCode) {
                return false;
            }

            mayThrow |= summary.MayThrow;
            observable |= summary.Leaks;
            constructor = summary.Next;
        }

        return false;
    }
}
