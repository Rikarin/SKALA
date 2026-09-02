using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2290</c> — a <c>private</c> method's <c>out</c> parameter that every call site discards.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK2290". <c>SK6040</c> is the same word — "discarded" — at the
///     other end of the call: it reports one call site whose <em>declared variable</em> is never read.
///     This reports the declaration, and only when <em>no</em> call site anywhere reads the value, which
///     is the fact that says the body's computation is dead rather than that one call is untidy.
///     <para>
///         ⚠
///         <b>
///             <see cref="RegisterCompilationStartAction" />, not
///             <see cref="AnalysisContext.RegisterSymbolStartAction(Action{SymbolStartAnalysisContext}, SymbolKind)" />.
///         </b> A <c>private</c> member is callable from nested types, and a nested type is a different
///         <see cref="INamedTypeSymbol" /> — so a per-type symbol start sees the declaration and not
///         every call site, and the missing call sites are exactly the ones that make the claim false.
///         The price is <see cref="RuleInfo.IsCacheable" />: <c>scope: "Compilation"</c> means the rule
///         does not run at all without a project, and re-runs whole.
///     </para>
///     <para>
///         ⚠ <b><see cref="GeneratedCodeAnalysisFlags.Analyze" />, not <c>None</c></b>, for the reason
///         <c>SK2133</c> records: a call from a generated <c>partial</c> part <em>is</em> a call. At
///         <c>None</c> neither the syntax nor the operation actions run on a generated tree, so those
///         call sites are invisible and the rule reports a method whose value somebody does read.
///         Roslyn still drops a diagnostic whose own location is generated, because
///         <c>ReportDiagnostics</c> is not set.
///     </para>
///     <para>
///         ⚠ <b>Zero call sites is a decline.</b> "Every caller discards it" is vacuously true of a
///         method nobody calls, and that vacuous truth is how this rule would otherwise fire on every
///         uncalled <c>private</c> helper in a tree. An uncalled private member is a different finding
///         with a different repair.
///     </para>
///     <para>
///         Report-only. Removing an <c>out</c> parameter edits the declaration, every argument list and
///         the body at once, and the last of those ends in a judgement about which statements only
///         existed to feed the parameter. That is a refactoring, not a text-edit list.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiscardedOutParameterAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.DiscardedOutParameter);

    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.DiscardedOutParameter);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                // `out _` is C# 7.0, so below it the shape cannot be written at all.
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                var candidates = new ConcurrentDictionary<ISymbol, Candidate>(SymbolEqualityComparer.Default);
                var calls = new ConcurrentBag<CallSite>();

                // ⚠ Guard 1. Every spelling of the name that is *not* the callee of an invocation:
                // `Func<…> f = TryGet;`, `nameof(TryGet)`, `new Del(TryGet)`. Through a delegate the
                // arguments are invisible, so the call-site set stops being complete — and
                // completeness is this rule's only claim. Deliberately over-broad on the identifier
                // text, which costs findings and never costs correctness.
                var referenced = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

                start.RegisterSyntaxNodeAction(
                    context => RecordReference(context, referenced),
                    SyntaxKind.IdentifierName,
                    SyntaxKind.GenericName
                );

                // ⚠ A dynamically dispatched call is a call site the operation tree does not model as
                // an `IInvocationOperation`, and `d.TryGet(out x)` on a `dynamic` receiver compiles —
                // verified by probe, not assumed. Its name goes into the same withdrawal set.
                start.RegisterOperationAction(
                    context => {
                        if (((IDynamicInvocationOperation)context.Operation).Operation
                            is IDynamicMemberReferenceOperation member) {
                            referenced.TryAdd(member.MemberName, 0);
                        }
                    },
                    OperationKind.DynamicInvocation
                );

                // ⚠ Guard 5, and the corpus sweep is what found it. Text a `#if` switched off is
                // trivia: it carries no nodes, no operations and no symbols, so a call site inside it
                // is invisible to everything above — and under another `DefineConstants` it is a call
                // site that reads the value. Every word in every disabled region joins the same
                // withdrawal set, which is coarse on purpose: it costs findings, never correctness.
                start.RegisterSyntaxTreeAction(context => RecordDisabledText(context, referenced));

                start.RegisterSyntaxNodeAction(
                    context => Collect(context, candidates),
                    SyntaxKind.MethodDeclaration
                );

                start.RegisterOperationAction(context => Record(context, calls), OperationKind.Invocation);

                start.RegisterCompilationEndAction(context => Report(context, candidates, calls, referenced));
            }
        );
    }

    /// <summary>A <c>private</c> method with <c>out</c> parameters, pending the call-site count.</summary>
    sealed class Candidate {
        public Candidate(string name, ImmutableArray<OutParameter> parameters) {
            Name = name;
            Parameters = parameters;
        }

        public string Name { get; }

        public ImmutableArray<OutParameter> Parameters { get; }
    }

    /// <summary>One <c>out</c> parameter, by ordinal, with the span the finding is reported on.</summary>
    public readonly struct OutParameter {
        public OutParameter(int ordinal, string name, Location location) {
            Ordinal = ordinal;
            Name = name;
            Location = location;
        }

        public int Ordinal { get; }

        public string Name { get; }

        public Location Location { get; }
    }

    /// <summary>One argument passed to one <c>out</c> parameter at one call site.</summary>
    readonly struct CallSite {
        public CallSite(ISymbol method, int ordinal, bool discarded) {
            Method = method;
            Ordinal = ordinal;
            Discarded = discarded;
        }

        public ISymbol Method { get; }

        public int Ordinal { get; }

        public bool Discarded { get; }
    }

    /// <summary>
    ///     Records an identifier that names something as a value rather than calling it.
    /// </summary>
    /// <remarks>
    ///     ⚠ The same two exclusions, and the same reasoning, as <c>AsyncSignature.RecordReference</c> —
    ///     with one addition it does not need and this rule does. <c>GenericName</c> is watched as well
    ///     as <c>IdentifierName</c>, because a generic method's method-group conversion spells
    ///     <c>TryGet&lt;int&gt;</c>, which is a <see cref="GenericNameSyntax" /> and invisible to an
    ///     identifier-only sweep. Losing that would leave the one hole the guard exists to close.
    /// </remarks>
    static void RecordReference(SyntaxNodeAnalysisContext context, ConcurrentDictionary<string, byte> referenced) {
        var name = (SimpleNameSyntax)context.Node;

        // `Foo()` — a direct call is not a method group and says nothing about a delegate.
        if (name.Parent is InvocationExpressionSyntax invocation && ReferenceEquals(invocation.Expression, name)) {
            return;
        }

        // `x.Foo()` — the same, one level in.
        if (name.Parent is MemberAccessExpressionSyntax access
            && ReferenceEquals(access.Name, name)
            && access.Parent is InvocationExpressionSyntax outer
            && ReferenceEquals(outer.Expression, access)) {
            return;
        }

        referenced.TryAdd(name.Identifier.ValueText, 0);
    }

    /// <summary>
    ///     Every identifier-shaped word inside text a <c>#if</c> switched off, as a reference.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>This guard is the corpus sweep's finding, not a precaution.</b> The rule's one
    ///     false positive over the three reference trees was <c>Newtonsoft.Json</c>'s
    ///     <c>TryParseMicrosoftDate</c>: two call sites, one discarding <c>offset</c> and one reading
    ///     it, and the reading one sits inside <c>#if HAVE_DATE_TIME_OFFSET</c>. The finding was true
    ///     of the configuration compiled and false of the library's own build, and no amount of care
    ///     in the operation tree could have seen it — disabled text is trivia, with no nodes, no
    ///     operations and no symbols in it at all.
    ///     <para>
    ///         Deliberately word-level rather than parsed. Disabled text need not be valid C#, so
    ///         there is nothing to bind and nothing to walk; splitting on what cannot appear in an
    ///         identifier is the whole analysis. It over-withdraws — a comment inside a disabled
    ///         region silences the rule for a method of that name — and that direction costs findings
    ///         rather than correctness. Trees with no directives at all are skipped, which is nearly
    ///         all of them.
    ///     </para>
    /// </remarks>
    static void RecordDisabledText(
        SyntaxTreeAnalysisContext context,
        ConcurrentDictionary<string, byte> referenced
    ) {
        var root = context.Tree.GetRoot(context.CancellationToken);
        if (!root.ContainsDirectives) {
            return;
        }

        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true)) {
            if (!trivia.IsKind(SyntaxKind.DisabledTextTrivia)) {
                continue;
            }

            var text = trivia.ToString();
            var start = -1;
            for (var i = 0; i <= text.Length; i++) {
                var identifier = i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_');
                if (identifier && start < 0) {
                    start = i;
                } else if (!identifier && start >= 0) {
                    referenced.TryAdd(text.Substring(start, i - start), 0);
                    start = -1;
                }
            }
        }
    }

    /// <summary>
    ///     A <c>private</c> ordinary method declaring at least one <c>out</c> parameter.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         There is no <c>virtual</c>/<c>abstract</c>/<c>override</c> guard, and its absence is a
    ///         measurement rather than an oversight.
    ///     </b> At <c>private</c> accessibility all three are
    ///     compile errors — <c>private virtual</c> and <c>private abstract</c> are <b>CS0621</b>, and
    ///     <c>private override</c> draws <b>CS0507</b> on top of it, because nothing a base type can
    ///     declare is both private and virtual. A guard against them could never be reached by
    ///     compiling code, and doc 08's bar asks for guards that a sabotage can turn red.
    ///     <para>
    ///         ⚠ An <b>explicit interface implementation</b> is the one shape Roslyn reports as
    ///         <see cref="Accessibility.Private" /> that is not private in the language, and it is
    ///         declined here as a <em>stated gate</em>, not as a load-bearing guard: its callers reach
    ///         it through the interface, so it has zero visible call sites and
    ///         <see cref="Report" />'s zero-call-site guard already refuses it. Sabotaging this line
    ///         alone turns nothing red, which is why it says so instead of claiming otherwise.
    ///     </para>
    /// </remarks>
    static void Collect(SyntaxNodeAnalysisContext context, ConcurrentDictionary<ISymbol, Candidate> candidates) {
        var declaration = (MethodDeclarationSyntax)context.Node;

        // ⚠ Guard 2. `partial` is two symbols for one method and either part may be a generator's, so
        // a report lands on a declaration the author does not edit; `extern` has no body to hold the
        // dead work.
        foreach (var modifier in declaration.Modifiers) {
            if (modifier.IsKind(SyntaxKind.PartialKeyword) || modifier.IsKind(SyntaxKind.ExternKeyword)) {
                return;
            }
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
            is not { } method
            || method.DeclaredAccessibility != Accessibility.Private
            || method.MethodKind != MethodKind.Ordinary
            || method.IsPartialDefinition
            || method.PartialDefinitionPart is not null
            || method.PartialImplementationPart is not null) {
            return;
        }

        // ⚠ `Deconstruct` is the one out-parameter protocol the language reaches by name rather than
        // by an invocation: `var (a, b) = this;`, `foreach` deconstruction and a positional pattern
        // all call it, and none of them is an `IInvocationOperation`. A private `Deconstruct` used
        // that way *and* called explicitly once with `out _` would look unanimously discarded.
        if (string.Equals(method.Name, "Deconstruct", StringComparison.Ordinal)) {
            return;
        }

        var parameters = ImmutableArray.CreateBuilder<OutParameter>();
        foreach (var parameter in method.Parameters) {
            if (parameter.RefKind == RefKind.Out
                && parameter.Ordinal < declaration.ParameterList.Parameters.Count) {
                parameters.Add(
                    new OutParameter(
                        parameter.Ordinal,
                        parameter.Name,
                        declaration.ParameterList.Parameters[parameter.Ordinal].GetLocation()
                    )
                );
            }
        }

        if (parameters.Count > 0) {
            candidates.TryAdd(
                method.OriginalDefinition,
                new Candidate(method.Name, parameters.ToImmutable())
            );
        }
    }

    /// <summary>
    ///     Every <c>out</c> argument at one call site, and whether it is a discard.
    /// </summary>
    /// <remarks>
    ///     ⚠ Asked of the operation tree rather than of the syntax, because syntax answers it wrongly:
    ///     <c>M(out _)</c> parses its <c>_</c> as an ordinary <see cref="IdentifierNameSyntax" /> and
    ///     only binding says it is a discard, while <c>out var _</c> and <c>out int _</c> are
    ///     <see cref="DeclarationExpressionSyntax" /> nodes with a discard designation. See
    ///     <see cref="IsDiscard" /> for what the operation tree does with the three of them.
    /// </remarks>
    static void Record(OperationAnalysisContext context, ConcurrentBag<CallSite> calls) {
        var invocation = (IInvocationOperation)context.Operation;
        var target = invocation.TargetMethod.OriginalDefinition;
        foreach (var argument in invocation.Arguments) {
            if (argument.Parameter is { RefKind: RefKind.Out } parameter) {
                calls.Add(new CallSite(target, parameter.Ordinal, IsDiscard(argument.Value)));
            }
        }
    }

    /// <summary>Whether the argument passed to an <c>out</c> parameter is a discard.</summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         One test, and the second one that was written here was dead code — measured, not
    ///         assumed.
    ///     </b> The draft also unwrapped an <see cref="IDeclarationExpressionOperation" />
    ///     looking for a discard inside it, because <c>out var _</c> and <c>out int _</c> are
    ///     <see cref="DeclarationExpressionSyntax" /> nodes and <c>out _</c> is not. In the operation
    ///     tree they are not distinguishable: all three arrive as a bare
    ///     <see cref="IDiscardOperation" />, and sabotaging the unwrap left every fixture green —
    ///     including <c>positive/an-explicitly-typed-discard</c>, written specifically to reach it.
    ///     The syntactic difference is real and the semantic one does not exist, which is the reason
    ///     the question is asked of the operation at all.
    /// </remarks>
    static bool IsDiscard(IOperation value) => value is IDiscardOperation;

    /// <summary>The empty count table a method nobody calls is measured against.</summary>
    static readonly Dictionary<int, (int Total, int Discarded)> NoCalls = new();

    static void Report(
        CompilationAnalysisContext context,
        ConcurrentDictionary<ISymbol, Candidate> candidates,
        ConcurrentBag<CallSite> calls,
        ConcurrentDictionary<string, byte> referenced
    ) {
        // One pass over the bag, so a compilation with many call sites does not become quadratic.
        var seen = new Dictionary<ISymbol, Dictionary<int, (int Total, int Discarded)>>(SymbolEqualityComparer.Default);

        foreach (var call in calls) {
            if (!candidates.ContainsKey(call.Method)) {
                continue;
            }

            if (!seen.TryGetValue(call.Method, out var byOrdinal)) {
                byOrdinal = new Dictionary<int, (int, int)>();
                seen[call.Method] = byOrdinal;
            }

            byOrdinal.TryGetValue(call.Ordinal, out var counts);
            byOrdinal[call.Ordinal] = (counts.Total + 1, counts.Discarded + (call.Discarded ? 1 : 0));
        }

        foreach (var pair in candidates) {
            if (referenced.ContainsKey(pair.Value.Name)) {
                continue;
            }

            // ⚠ A method with no call sites at all must reach the count below rather than be skipped
            // here, or the guard that refuses it is never the line that refuses it — and a guard no
            // sabotage can reach is not a guard. This spelling was found by sabotaging the wrong line
            // and getting no fixture red.
            if (!seen.TryGetValue(pair.Key, out var byOrdinal)) {
                byOrdinal = NoCalls;
            }

            foreach (var parameter in pair.Value.Parameters) {
                byOrdinal.TryGetValue(parameter.Ordinal, out var counts);

                // ⚠ Guard 3, and it is `Total == 0` that carries it. Zero call sites is an absence of
                // evidence, not unanimity — and `0 discarded of 0` passes the unanimity test on its
                // own, which is exactly how this rule would fire on every uncalled `private` helper in
                // every tree.
                if (counts.Total == 0 || counts.Discarded != counts.Total) {
                    continue;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        parameter.Location,
                        "`"
                        + pair.Value.Name
                        + "` assigns `"
                        + parameter.Name
                        + "`, and "
                        + (counts.Total == 1
                                ? "its one call site discards it"
                                : "all "
                                + counts.Total.ToString(CultureInfo.InvariantCulture)
                                + " of its call sites discard it")
                    )
                );
            }
        }
    }
}
