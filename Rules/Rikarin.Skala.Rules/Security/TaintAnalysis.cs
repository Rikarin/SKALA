using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Security;

/// <summary>One proven flow: a value that crossed a trust boundary, arriving where it must not.</summary>
public sealed record TaintFinding(Location Location, TaintSink Sink, string SourceDescription);

/// <summary>
///     Intra-procedural taint propagation over Roslyn's <see cref="ControlFlowGraph" />.
/// </summary>
/// <remarks>
///     docs/plan/08 § "SK5000 — Security": <c>SK5001</c>–<c>SK5004</c> are "built on Roslyn's
///     <c>ControlFlowGraph</c> + <c>DataFlowAnalysis</c> with intra-procedural propagation and a
///     declared source/sink/sanitizer table in <c>taint.json</c>".
///     <para>
///         The shape is a classic forward may-analysis. The lattice is the powerset of the method's locals,
///         parameters and flow captures; the merge is union, because a value tainted on <em>either</em> arm
///         of an <c>if</c> is tainted after it; the fixpoint iterates the blocks in ordinal order until
///         nothing moves, which handles loops without a special case.
///     </para>
///     <para>
///         ⚠ <b>Every unknown resolves to "not tainted".</b> A call the table does not name, a symbol that
///         did not bind, a body the CFG could not be built for, a value that arrived from another method —
///         all of them are silence. That is not a limitation being apologised for, it is the specification:
///         doc 08 puts inter-procedural taint out of scope for v1 because "it is where the false positives
///         live", and doc 00's false-positive bar applies hardest to a range that defaults to
///         <c>error</c> and therefore fails builds.
///     </para>
///     <para>
///         ⚠ <b>A parameter is never a source.</b> This is the single most consequential line in the file,
///         and it is a correctness decision rather than a tuning one. A parameter's incoming value is
///         whatever the callers pass, which is by definition not visible from inside the method; treating
///         "unknown" as "suspicious" would make the engine assert a vulnerability in code that has none.
///         The shape is completely ordinary — a private helper ending in
///         <c>command.CommandText = sql;</c>, whose callers all pass a constant with <c>@name</c>
///         placeholders and bind the values properly — and every report on it would be simply wrong. A
///         rule that fires a lot is work for the repository it fires on; a rule that is wrong is a rule
///         nobody keeps switched on.
///     </para>
///     <para>
///         ⚠ <b>Bodies of lambdas and local functions are not visited.</b> Roslyn puts them in a nested
///         <see cref="ControlFlowGraph" /> that is not in <see cref="ControlFlowGraph.Blocks" />, and
///         following one would need the captured state, which is an inter-procedural question wearing a
///         different hat. A sink inside a lambda is therefore a miss, and a miss is the safe direction.
///     </para>
/// </remarks>
public static class TaintAnalysis {
    /// <summary>
    ///     ⚠ The fixpoint terminates on its own — the lattice is finite and the transfer is monotone —
    ///     so this bound can only ever be reached by a bug in the transfer. It is here because an
    ///     analyzer that hangs takes the compiler with it, and "no findings from a pathological method"
    ///     is a far better failure than "the IDE stopped responding".
    /// </summary>
    const int MaximumPasses = 64;

    /// <summary>Every proven flow into one of the table's sinks, in source order.</summary>
    public static IReadOnlyList<TaintFinding> Run(
        ControlFlowGraph graph,
        TaintSymbols symbols,
        CancellationToken cancellation
    ) {
        var blocks = graph.Blocks;
        var entry = new HashSet<ISymbol>[blocks.Length];
        var exit = new HashSet<ISymbol>[blocks.Length];
        var captureEntry = new HashSet<CaptureId>[blocks.Length];
        var captureExit = new HashSet<CaptureId>[blocks.Length];

        for (var i = 0; i < blocks.Length; i++) {
            entry[i] = NewSymbolSet();
            exit[i] = NewSymbolSet();
            captureEntry[i] = new HashSet<CaptureId>();
            captureExit[i] = new HashSet<CaptureId>();
        }

        for (var pass = 0; pass < MaximumPasses; pass++) {
            cancellation.ThrowIfCancellationRequested();
            var moved = false;

            foreach (var block in blocks) {
                var symbolsIn = NewSymbolSet();
                var capturesIn = new HashSet<CaptureId>();
                foreach (var predecessor in block.Predecessors) {
                    symbolsIn.UnionWith(exit[predecessor.Source.Ordinal]);
                    capturesIn.UnionWith(captureExit[predecessor.Source.Ordinal]);
                }

                if (!symbolsIn.SetEquals(entry[block.Ordinal])
                    || !capturesIn.SetEquals(captureEntry[block.Ordinal])) {
                    moved = true;
                }

                entry[block.Ordinal] = symbolsIn;
                captureEntry[block.Ordinal] = capturesIn;

                var walker = new Walker(symbols, CopyOf(symbolsIn), new HashSet<CaptureId>(capturesIn), null);
                walker.Block(block);

                if (!walker.Tainted.SetEquals(exit[block.Ordinal])
                    || !walker.Captures.SetEquals(captureExit[block.Ordinal])) {
                    moved = true;
                }

                exit[block.Ordinal] = walker.Tainted;
                captureExit[block.Ordinal] = walker.Captures;
            }

            if (!moved) {
                break;
            }
        }

        // ⚠ Reporting is a separate final pass over the settled entry states, never a side effect of
        // the fixpoint: the fixpoint visits a block once per iteration, so reporting inside it would
        // emit a finding once per pass and the count would depend on how the loops converged.
        var findings = new List<TaintFinding>();
        foreach (var block in blocks) {
            cancellation.ThrowIfCancellationRequested();
            var walker = new Walker(
                symbols,
                CopyOf(entry[block.Ordinal]),
                new HashSet<CaptureId>(captureEntry[block.Ordinal]),
                findings
            );

            walker.Block(block);
        }

        findings.Sort(static (left, right) =>
            left.Location.SourceSpan.Start.CompareTo(right.Location.SourceSpan.Start)
        );

        return findings;
    }

    static HashSet<ISymbol> NewSymbolSet() => new(SymbolEqualityComparer.Default);

    static HashSet<ISymbol> CopyOf(HashSet<ISymbol> source) => new(source, SymbolEqualityComparer.Default);

    /// <summary>
    ///     The transfer function: one block's operations, in order, against a mutable taint state.
    /// </summary>
    /// <remarks>
    ///     ⚠ It is used twice with different intent. With <c>findings == null</c> it is the fixpoint's
    ///     transfer and reports nothing; with a list it is the reporting pass. The state arithmetic is
    ///     identical in both, which is what makes the reported set exactly the set the fixpoint settled
    ///     on rather than an approximation of it.
    /// </remarks>
    sealed class Walker {
        readonly TaintSymbols _symbols;
        readonly List<TaintFinding>? _findings;

        public Walker(
            TaintSymbols symbols,
            HashSet<ISymbol> tainted,
            HashSet<CaptureId> captures,
            List<TaintFinding>? findings
        ) {
            _symbols = symbols;
            _findings = findings;
            Tainted = tainted;
            Captures = captures;
        }

        public HashSet<ISymbol> Tainted { get; }

        public HashSet<CaptureId> Captures { get; }

        public void Block(BasicBlock block) {
            foreach (var operation in block.Operations) {
                Walk(operation);
            }

            if (block.BranchValue is { } branch) {
                Walk(branch);
            }
        }

        void Walk(IOperation operation) {
            switch (operation) {
                case ISimpleAssignmentOperation assignment:
                    Walk(assignment.Value);
                    ReportPropertySink(assignment);
                    Assign(assignment.Target, IsTainted(assignment.Value));
                    return;

                case ICompoundAssignmentOperation compound:
                    Walk(compound.Value);

                    // ⚠ `sql += tainted` only ever adds taint. It never removes it: the target's
                    // previous value is still in the result.
                    if (IsTainted(compound.Value)) {
                        Assign(compound.Target, true);
                    }

                    return;

                case IVariableDeclaratorOperation { Initializer: { } initializer } declarator:
                    Walk(initializer.Value);
                    Set(declarator.Symbol, IsTainted(initializer.Value));
                    return;

                case IFlowCaptureOperation capture:
                    Walk(capture.Value);
                    if (IsTainted(capture.Value)) {
                        Captures.Add(capture.Id);
                    } else {
                        Captures.Remove(capture.Id);
                    }

                    return;

                case IInvocationOperation invocation:
                    WalkChildren(invocation);
                    ReportCallSink(invocation, invocation.TargetMethod, invocation.Arguments);
                    PropagateToReceiver(invocation);
                    return;

                case IObjectCreationOperation creation:
                    WalkChildren(creation);
                    if (creation.Constructor is { } constructor) {
                        ReportCallSink(creation, constructor, creation.Arguments);
                    }

                    return;

                default:
                    WalkChildren(operation);
                    return;
            }
        }

        void WalkChildren(IOperation operation) {
            foreach (var child in operation.ChildOperations) {
                Walk(child);
            }
        }

        /// <summary>
        ///     ⚠ <c>builder.Append(tainted)</c> taints <c>builder</c>, not the call's result.
        /// </summary>
        /// <remarks>
        ///     This is the shape most SQL concatenation actually has, and without it the whole
        ///     <c>StringBuilder</c> path would be invisible: the flow is into the receiver across
        ///     several statements and out again through <c>ToString()</c>.
        /// </remarks>
        void PropagateToReceiver(IInvocationOperation invocation) {
            if (invocation.Instance is null || !_symbols.IsPropagator(invocation.TargetMethod)) {
                return;
            }

            foreach (var argument in invocation.Arguments) {
                if (IsTainted(argument.Value)) {
                    Assign(RootReceiver(invocation.Instance), true);
                    return;
                }
            }
        }

        /// <summary>
        ///     The variable a fluent chain is really about.
        /// </summary>
        /// <remarks>
        ///     ⚠ <c>builder.Append(a).Append(b)</c> — the receiver of the second <c>Append</c> is the
        ///     <em>result</em> of the first, not <c>builder</c>, so taint arriving through <c>b</c> had
        ///     nowhere to land and the whole chain came out clean. Chaining is how most
        ///     <c>StringBuilder</c> code is actually written, so this was not an edge case; it was the
        ///     common path, and only the corpus found it.
        /// </remarks>
        IOperation RootReceiver(IOperation instance) {
            while (instance is IInvocationOperation { Instance: { } inner } chained
                   && _symbols.IsPropagator(chained.TargetMethod)) {
                instance = inner;
            }

            return instance;
        }

        void Assign(IOperation target, bool tainted) {
            switch (target) {
                case ILocalReferenceOperation local:
                    Set(local.Local, tainted);
                    return;
                case IParameterReferenceOperation parameter:
                    Set(parameter.Parameter, tainted);
                    return;
                case IFlowCaptureReferenceOperation capture when tainted:
                    Captures.Add(capture.Id);
                    return;

                // ⚠ Fields, properties and array elements are deliberately not tracked. A field is
                // reachable from other methods, so what it holds is an inter-procedural question,
                // and answering it with what this method happened to store is exactly the guess
                // doc 08 puts out of scope.
                default:
                    return;
            }
        }

        void Set(ISymbol symbol, bool tainted) {
            if (tainted) {
                Tainted.Add(symbol);
            } else {
                Tainted.Remove(symbol);
            }
        }

        void ReportPropertySink(ISimpleAssignmentOperation assignment) {
            if (_findings is null
                || assignment.Target is not IPropertyReferenceOperation reference
                || _symbols.Sink(reference.Property) is not { Kind: "property" } sink
                || !IsTainted(assignment.Value)) {
                return;
            }

            _findings.Add(new TaintFinding(assignment.Value.Syntax.GetLocation(), sink, Describe(assignment.Value)));
        }

        void ReportCallSink(
            IOperation call,
            IMethodSymbol method,
            System.Collections.Immutable.ImmutableArray<IArgumentOperation> arguments
        ) {
            if (_findings is null) {
                return;
            }

            var expected = method.MethodKind == MethodKind.Constructor ? "constructor" : "method";
            if (_symbols.Sink(method) is not { } sink || sink.Kind != expected) {
                return;
            }

            foreach (var argument in arguments) {
                // ⚠ By parameter name, never by index. An extension method invoked in reduced form
                // shifts every index by one, and a name is also the thing a reader of taint.json can
                // check against the API's documentation.
                if (!NamesParameter(sink, argument.Parameter?.Name) || !IsTainted(argument.Value)) {
                    continue;
                }

                _findings.Add(new TaintFinding(argument.Value.Syntax.GetLocation(), sink, Describe(argument.Value)));
                return;
            }

            _ = call;
        }

        static bool NamesParameter(TaintSink sink, string? name) {
            if (name is null) {
                return false;
            }

            // An entry with no parameter list means every argument counts.
            if (sink.Parameters.Count == 0) {
                return true;
            }

            foreach (var candidate in sink.Parameters) {
                if (string.Equals(candidate, name, StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Whether a value carries data that crossed a trust boundary, under the current state.
        /// </summary>
        /// <remarks>
        ///     ⚠ The <c>default</c> arm returns false, and every arm that cannot answer falls into it.
        /// </remarks>
        bool IsTainted(IOperation? operation) {
            if (operation is null || operation.ConstantValue.HasValue) {
                return false;
            }

            switch (operation) {
                case ILocalReferenceOperation local:
                    return Tainted.Contains(local.Local);

                case IParameterReferenceOperation parameter:
                    // ⚠ Only if something in *this* method tainted it. A parameter's incoming value
                    // is an inter-procedural question. See the type's remarks.
                    return Tainted.Contains(parameter.Parameter);

                case IFlowCaptureReferenceOperation capture:
                    return Captures.Contains(capture.Id);

                case IConversionOperation conversion:
                    return IsTainted(conversion.Operand);

                case IParenthesizedOperation parenthesized:
                    return IsTainted(parenthesized.Operand);

                case IArgumentOperation argument:
                    return IsTainted(argument.Value);

                case IPropertyReferenceOperation property:
                    return _symbols.IsSource(property.Property)
                        || CarriesText(property.Property.Type)
                        && IsTainted(property.Instance);

                case IFieldReferenceOperation field:
                    return _symbols.IsSource(field.Field)
                        || CarriesText(field.Field.Type) && IsTainted(field.Instance);

                case IArrayElementReferenceOperation element:
                    return IsTainted(element.ArrayReference);

                case IInvocationOperation invocation:
                    return IsTaintedCall(invocation);

                case IBinaryOperation { OperatorKind: BinaryOperatorKind.Add } binary:
                    return IsTainted(binary.LeftOperand) || IsTainted(binary.RightOperand);

                case IInterpolatedStringOperation interpolated:
                    foreach (var part in interpolated.Parts) {
                        if (IsTainted(part)) {
                            return true;
                        }
                    }

                    return false;

                case IInterpolationOperation interpolation:
                    return IsTainted(interpolation.Expression);

                case IConditionalOperation conditional:
                    return IsTainted(conditional.WhenTrue) || IsTainted(conditional.WhenFalse);

                case ICoalesceOperation coalesce:
                    return IsTainted(coalesce.Value) || IsTainted(coalesce.WhenNull);

                default:
                    return false;
            }
        }

        bool IsTaintedCall(IInvocationOperation invocation) {
            // ⚠ Sanitizer first. `int.Parse(request.Query["id"])` is an `int`, and an `int`
            // interpolated into SQL cannot carry a quote, however tainted its argument was.
            if (_symbols.IsSanitizer(invocation.TargetMethod)) {
                return false;
            }

            if (_symbols.IsSource(invocation.TargetMethod)) {
                return true;
            }

            if (_symbols.IsPropagator(invocation.TargetMethod)) {
                if (IsTainted(invocation.Instance)) {
                    return true;
                }

                foreach (var argument in invocation.Arguments) {
                    if (IsTainted(argument.Value)) {
                        return true;
                    }
                }

                return false;
            }

            return CarriesText(invocation.TargetMethod.ReturnType) && IsTainted(invocation.Instance);
        }

        /// <summary>
        ///     Whether a type can carry attacker-chosen text out of a value that already does.
        /// </summary>
        /// <remarks>
        ///     ⚠ This is the guard that keeps instance propagation from becoming "anything reachable
        ///     from a request is tainted". <c>request.Query.Count</c> is an <c>int</c> and stops here;
        ///     <c>request.Query["id"]</c> is a <c>StringValues</c> and does not.
        /// </remarks>
        static bool CarriesText(ITypeSymbol? type) => CarriesText(type, depth: 0);

        static bool CarriesText(ITypeSymbol? type, int depth) {
            // ⚠ Bounded. A deeply nested generic is not worth an unbounded walk inside an analyzer,
            // and stopping early is the safe direction: it under-taints.
            if (type is null || depth > 3) {
                return false;
            }

            switch (type.SpecialType) {
                case SpecialType.System_String:
                case SpecialType.System_Object:
                    return true;
            }

            if (type is IArrayTypeSymbol array) {
                return CarriesText(array.ElementType, depth + 1);
            }

            // ⚠ `IEnumerator` and `IEnumerable` are here as *names* rather than through the type
            // arguments below, because the control-flow graph's `foreach` lowering reaches for the
            // non-generic pair: block 5 of the graph for `foreach (var a in xs)` is
            // `GetEnumerator()` typed `IEnumerator`, and its `Current` is typed `object`. With the
            // generic path alone the enumerator came out clean and the loop variable with it, so a
            // request read inside any loop lost its taint at the top of the loop. The corpus found
            // that; reading the code twice did not.
            if (type.Name is "StringValues"
                or "StringBuilder"
                or "Uri"
                or "Stream"
                or "TextReader"
                or "IEnumerator"
                or "IEnumerable") {
                return true;
            }

            // ⚠ Through the type arguments, which is what makes a `foreach` propagate. Roslyn's
            // control-flow graph lowers `foreach (var s in xs)` into `e = xs.GetEnumerator()` and
            // `s = e.Current`, so without this the enumerator is an opaque type, `e` comes out
            // clean, and every loop over request data silently loses its taint at the top. That is
            // one of the two misses the vulnerable corpus caught and review did not.
            if (type is INamedTypeSymbol { IsGenericType: true } generic) {
                foreach (var argument in generic.TypeArguments) {
                    if (CarriesText(argument, depth + 1)) {
                        return true;
                    }
                }
            }

            return false;
        }

        static string Describe(IOperation value) {
            var text = value.Syntax.ToString();
            text = text.Replace('\r', ' ').Replace('\n', ' ');
            return text.Length <= 60 ? text : text.Substring(0, 57) + "…";
        }
    }
}
