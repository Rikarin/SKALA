using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Formatting;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
///     <c>int_align_*</c>: pad adjacent lines so that one token of each lands in the same column.
/// </summary>
/// <remarks>
///     ⚠ A pass over the <em>output</em>, run after the layout writer and before the edit emitter, and
///     it is the only part of the formatter that works that way. Two reasons, and the first is what
///     forces it:
///     <list type="number">
///         <item>
///             The column a token ends up in is not known until the document has been written. Alignment asks
///             "how wide is the widest of these five names once every wrapping decision is made", and the IR
///             has no node that can hold an answer that depends on its own siblings' final columns.
///         </item>
///         <item>
///             Every key here is <c>false</c> in the export, so this returns its input untouched for every file
///             the fidelity number is measured over — <see cref="PhaseOneOptions.IntAlignAnything" /> is the
///             early exit, and the second parse it guards costs nothing when nothing is aligned.
///         </item>
///     </list>
///     <para>
///         ⚠ It re-parses its input rather than carrying the tree through. The output is a different text
///         from the input — that is the point of a formatter — and every offset the alignment needs is an
///         offset into the output. Mapping them back through the anchor table would be the same parse plus
///         a lookup that can fail.
///     </para>
///     <para>
///         ⚠ Insertion only, and only of spaces between two tokens the space rules already separated. That
///         is what keeps <see cref="TokenEquivalence" /> satisfied: padding widens a gap that exists, and
///         never creates or removes one.
///     </para>
/// </remarks>
public static class IntAlign {
    /// <summary>The slots one construct contributes to its run, as offsets into the output.</summary>
    /// <remarks>
    ///     ⚠ Fixed arity per kind. A construct that cannot fill every slot of its kind — a field with no
    ///     initializer among fields that have one — <em>ends</em> the run rather than joining it with a
    ///     hole, because a hole makes the column of the slots after it depend on which members happened
    ///     to be missing, which is not a rule anybody can predict from the option name.
    /// </remarks>
    /// <param name="Signature">
    ///     What a neighbour has to match to join this row's run, or <c>null</c> when membership is
    ///     decided by adjacency alone.
    /// </param>
    /// <remarks>
    ///     ⚠ Only <see cref="Kind.Invocations" /> uses it, and it is the key's own wording — "invocations
    ///     of the same method" — rather than a refinement of it. Measured: three <c>Take(…)</c> calls
    ///     with an <c>Other2(…)</c> in the middle come back from the oracle unpadded, so the different
    ///     callee ends the run rather than being skipped over.
    /// </remarks>
    readonly record struct Row(int LineStart, ImmutableArray<int> Slots, string? Signature = null);

    public static string Apply(string text, in PhaseOneOptions options, CSharpParseOptions parseOptions) {
        if (!options.IntAlignAnything || text.Length == 0) {
            return text;
        }

        var source = SourceText.From(text);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions);
        foreach (var diagnostic in tree.GetDiagnostics()) {
            // ⚠ Alignment never runs on something it cannot parse. The caller verifies the token
            // stream afterwards and would reject the file outright; returning the input here keeps
            // the failure to "not aligned" rather than "not formatted".
            if (diagnostic.Severity == DiagnosticSeverity.Error) {
                return text;
            }
        }

        var root = tree.GetRoot();
        var lines = source.Lines;
        var runs = new List<List<Row>>();

        if (options.IntAlignFields) {
            Collect(root, lines, runs, Kind.Fields);
        }

        if (options.IntAlignVariables) {
            Collect(root, lines, runs, Kind.Variables);
        }

        if (options.IntAlignAssignments) {
            Collect(root, lines, runs, Kind.Assignments);
        }

        if (options.IntAlignProperties) {
            Collect(root, lines, runs, Kind.Properties);
        }

        if (options.IntAlignMethods) {
            Collect(root, lines, runs, Kind.Methods);
        }

        if (options.IntAlignSwitchExpressions) {
            Collect(root, lines, runs, Kind.SwitchExpressions);
        }

        if (options.IntAlignSwitchSections) {
            Collect(root, lines, runs, Kind.SwitchSections);
        }

        if (options.IntAlignParameters) {
            Collect(root, lines, runs, Kind.Parameters);
        }

        if (options.IntAlignInvocations) {
            Collect(root, lines, runs, Kind.Invocations);
        }

        if (options.IntAlignPropertyPatterns) {
            Collect(root, lines, runs, Kind.PropertyPatterns);
        }

        if (options.IntAlignNestedTernary) {
            CollectConditionalChains(root, lines, runs, questions: true);
        }

        if (options.IntAlignBinaryExpressions) {
            CollectConditionalChains(root, lines, runs, questions: false);
        }

        if (options.IntAlignComments) {
            CollectComments(root, lines, runs);
        }

        return runs.Count == 0 ? text : Pad(text, runs);
    }

    enum Kind {
        Fields,
        Variables,
        Assignments,
        Properties,
        Methods,
        SwitchExpressions,
        SwitchSections,
        Parameters,
        Invocations,
        PropertyPatterns
    }

    /// <summary>
    ///     Walks every list of siblings in the tree and cuts it into runs of alignable neighbours.
    /// </summary>
    /// <remarks>
    ///     ⚠ A blank line ends a run, and that is measured rather than assumed: asked directly, the
    ///     oracle aligns three fields and three constants separated by one blank line to two different
    ///     columns, not to one.
    /// </remarks>
    static void Collect(SyntaxNode root, TextLineCollection lines, List<List<Row>> runs, Kind kind) {
        foreach (var node in root.DescendantNodesAndSelf()) {
            var children = Siblings(node, kind);
            if (children is null) {
                continue;
            }

            var run = new List<Row>();
            var previousLine = -2;
            foreach (var child in children) {
                var row = RowOf(child, lines, kind);
                var line = row is { } present ? lines.GetLineFromPosition(present.LineStart).LineNumber : -1;

                // ⚠ Three ways a run ends, and the third is the one this family's list-shaped
                // members needed: the row is not alignable at all, it is not on the next line, or
                // it does not match what the run is aligning — a call to a different method, or to
                // the same one with a different number of arguments.
                if (row is null
                    || line != previousLine + 1
                    || run.Count > 0 && !Joins(run[^1], row.Value)) {
                    Flush(runs, run);
                }

                if (row is { } value) {
                    run.Add(value);
                    previousLine = line;
                }
            }

            Flush(runs, run);
        }
    }

    /// <summary>Whether two adjacent rows are aligning the same thing.</summary>
    /// <remarks>
    ///     ⚠ The arity check is not decoration. <see cref="Pad" /> reads the slot count off the run's
    ///     first row, so a run whose rows disagree about how many slots they have is an index out of
    ///     range rather than a wrong column.
    /// </remarks>
    static bool Joins(in Row previous, in Row next) =>
        previous.Slots.Length == next.Slots.Length
        && string.Equals(previous.Signature, next.Signature, StringComparison.Ordinal);

    static void Flush(List<List<Row>> runs, List<Row> run) {
        if (run.Count > 1) {
            runs.Add([.. run]);
        }

        run.Clear();
    }

    static IEnumerable<SyntaxNode>? Siblings(SyntaxNode node, Kind kind) =>
        kind switch {
            Kind.Fields => node switch {
                TypeDeclarationSyntax type => type.Members,
                EnumDeclarationSyntax enumeration => enumeration.Members,
                _ => null
            },
            Kind.Properties => node is TypeDeclarationSyntax properties ? properties.Members : null,
            Kind.Methods => node is TypeDeclarationSyntax methods ? methods.Members : null,
            Kind.Variables or Kind.Assignments => node switch {
                BlockSyntax block => block.Statements,
                SwitchSectionSyntax section => section.Statements,
                _ => null
            },
            Kind.SwitchExpressions => node is SwitchExpressionSyntax arms ? arms.Arms : null,
            Kind.SwitchSections => node is SwitchStatementSyntax sections ? sections.Sections : null,
            Kind.Parameters => node is ParameterListSyntax parameters ? parameters.Parameters : null,
            Kind.Invocations => node switch {
                BlockSyntax block => block.Statements,
                SwitchSectionSyntax section => section.Statements,
                _ => null
            },
            Kind.PropertyPatterns => node is PropertyPatternClauseSyntax pattern ? pattern.Subpatterns : null,
            _ => null
        };

    /// <summary>The slots one construct offers, or null when it is not alignable.</summary>
    /// <remarks>
    ///     ⚠ Every kind requires its construct to occupy exactly one output line. Alignment pads a gap
    ///     on the line a token is on; a construct whose tokens are spread over three lines has no such
    ///     gap to widen, and the oracle leaves it alone.
    /// </remarks>
    static Row? RowOf(SyntaxNode node, TextLineCollection lines, Kind kind) {
        var span = node.Span;
        var line = lines.GetLineFromPosition(span.Start);
        if (span.End > line.End) {
            return null;
        }

        var slots = kind switch {
            Kind.Fields => node switch {
                FieldDeclarationSyntax { Declaration.Variables: [{ Initializer: { } initializer } declarator] } =>
                    Two(declarator.Identifier.SpanStart, initializer.EqualsToken.SpanStart),
                EnumMemberDeclarationSyntax { EqualsValue: { } value } => One(value.EqualsToken.SpanStart),
                _ => default
            },
            Kind.Variables =>
                node is LocalDeclarationStatementSyntax {
                    Declaration.Variables: [{ Initializer: { } initializer } declarator]
                }
                    ? Two(declarator.Identifier.SpanStart, initializer.EqualsToken.SpanStart)
                    : default,
            Kind.Assignments =>
                node is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
                    ? One(assignment.OperatorToken.SpanStart)
                    : default,
            Kind.Properties =>
                node is PropertyDeclarationSyntax { AccessorList: { } accessors } property
                    ? Two(property.Identifier.SpanStart, accessors.OpenBraceToken.SpanStart)
                    : default,
            Kind.Methods =>
                node is MethodDeclarationSyntax { Body: { } body }
                    ? One(body.OpenBraceToken.SpanStart)
                    : default,
            Kind.SwitchExpressions =>
                node is SwitchExpressionArmSyntax arm ? One(arm.EqualsGreaterThanToken.SpanStart) : default,
            Kind.SwitchSections =>
                node is SwitchSectionSyntax { Statements: [{ } first] } ? One(first.SpanStart) : default,

            // ⚠ The parameter's *name*, so the widest type pads out to a column and the names line
            // up. A parameter with no identifier — a function pointer's — has nothing to align and
            // ends the run.
            Kind.Parameters =>
                node is ParameterSyntax parameter && !parameter.Identifier.IsKind(SyntaxKind.None)
                    ? One(parameter.Identifier.SpanStart)
                    : default,
            Kind.Invocations =>
                node is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }
                    ? [.. invocation.ArgumentList.Arguments.Select(static argument => argument.SpanStart)]
                    : default,

            // ⚠ The `:` and not the subpattern's start. `ExpressionColon` rather than `NameColon`
            // because an extended property pattern — `{ A.B: 1 }` — has the second and not the
            // first, and its colon is the same column the oracle pads to.
            Kind.PropertyPatterns =>
                node is SubpatternSyntax { ExpressionColon: { } colon } ? One(colon.ColonToken.SpanStart) : default,
            _ => default
        };

        return slots.IsDefaultOrEmpty
            ? null
            : new Row(line.Start, slots, kind == Kind.Invocations ? Callee(node) : null);
    }

    /// <summary>The invoked method's text, as written, which is what a run of invocations shares.</summary>
    /// <remarks>
    ///     ⚠ Text and not a symbol. This pass runs over the formatter's <em>output</em> with no
    ///     compilation behind it (see the class remarks), so there is nothing to bind against; and text
    ///     is the stricter answer anyway, because two spellings of one method are two columns on the
    ///     page whatever they resolve to.
    /// </remarks>
    static string Callee(SyntaxNode node) =>
        node is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }
            ? invocation.Expression.ToString()
            : string.Empty;

    /// <summary>
    ///     <c>int_align_nested_ternary</c> and <c>int_align_binary_expressions</c>: one run per nested
    ///     conditional chain laid out with one arm per line.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two keys and one walk, because the oracle aligns two tokens of the same rows:
    ///     <code>
    /// var t = flag > 10 ? "a" :        var t = flag &gt; 10 ? "a" :
    ///     flag > 5 ? "bb" :        →       flag     &gt; 5 ? "bb" :     ← binary_expressions
    ///     flag > 1 ? "ccc" : "d";          flag     &gt; 1 ? "ccc" : "d";
    ///     </code>
    ///     and <c>nested_ternary</c> pads the same rows out to the <c>?</c> instead.
    ///     <para>
    ///         ⚠ Not every binary expression in the file, which is measured rather than inferred from the
    ///         key's name. With <c>int_align_binary_expressions = true</c> the oracle moves nothing in
    ///         adjacent assignments with binary right-hand sides, in a binary chain chopped one operand per
    ///         line, in adjacent <c>if</c> conditions, or in binary expressions used as arguments, as
    ///         initializer elements, or as switch-expression arm results.
    ///     </para>
    ///     <para>
    ///         ⚠ It is <em>wider</em> than this walk in exactly one place, and that is why
    ///         <c>int_align_binary_expressions</c> is Tier D where <c>int_align_nested_ternary</c> is Tier A.
    ///         Adjacent local variable <em>declarations</em> whose initializers are binary are also a run,
    ///         padded at every operator: <c>var first = flag &gt; 1 &amp;&amp; other &gt; 2;</c> beside
    ///         <c>var secondName = flag &gt; 100000 &amp;&amp; other &gt; 2;</c> comes back with both the
    ///         <c>&gt;</c> and the <c>&amp;&amp;</c> in a column. Pinned unimplemented in
    ///         <c>constructs/alignment/int-align-ternary.cs</c> under <c>Declarations</c>.
    ///     </para>
    ///     <para>
    ///         ⚠ A member joins only while its <c>?</c> is on its condition's own line, which is a weaker
    ///         condition than "the chain is laid out with a trailing colon": the leading-colon layout —
    ///         <c>: cond ? value</c> per line — satisfies it too, and the oracle pads that one as well at
    ///         both keys. What the test excludes is the staircase, where the <c>?</c> is on a line of its own.
    ///     </para>
    /// </remarks>
    static void CollectConditionalChains(
        SyntaxNode root,
        TextLineCollection lines,
        List<List<Row>> runs,
        bool questions
    ) {
        foreach (var node in root.DescendantNodesAndSelf()) {
            // ⚠ Started from the chain's root only. Every conditional in a chain is also a
            // descendant of the one above it, so walking from each would collect the same run once
            // per member and pad it that many times over.
            if (node is not ConditionalExpressionSyntax chain
                || node.Parent is ConditionalExpressionSyntax parent
                && parent.WhenFalse == node) {
                continue;
            }

            var run = new List<Row>();
            var previousLine = -2;
            for (ConditionalExpressionSyntax? member = chain;
                 member is not null;
                 member = member.WhenFalse as ConditionalExpressionSyntax) {
                var start = member.Condition.SpanStart;
                var line = lines.GetLineFromPosition(start);
                var slot = questions
                    ? member.QuestionToken.SpanStart
                    : member.Condition is BinaryExpressionSyntax binary
                        ? binary.OperatorToken.SpanStart
                        : -1;

                if (slot < 0 || slot > line.End || line.LineNumber != previousLine + 1 && run.Count > 0) {
                    Flush(runs, run);
                    if (slot < 0 || slot > line.End) {
                        previousLine = -2;
                        continue;
                    }
                }

                run.Add(new Row(line.Start, One(slot)));
                previousLine = line.LineNumber;
            }

            Flush(runs, run);
        }
    }

    static ImmutableArray<int> One(int a) => [a];

    static ImmutableArray<int> Two(int a, int b) => [a, b];

    /// <summary>
    ///     <c>int_align_comments</c>: the trailing <c>//</c> comments of adjacent lines.
    /// </summary>
    /// <remarks>
    ///     ⚠ Trivia rather than nodes, and it has to be: a trailing comment belongs to whatever token
    ///     precedes it, and two comments on consecutive lines can hang off tokens in different
    ///     constructs at different depths. The run is the run of <em>lines</em>.
    /// </remarks>
    static void CollectComments(SyntaxNode root, TextLineCollection lines, List<List<Row>> runs) {
        var run = new List<Row>();
        var previousLine = -2;
        foreach (var trivia in root.DescendantTrivia()) {
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)) {
                continue;
            }

            var line = lines.GetLineFromPosition(trivia.SpanStart);
            if (trivia.Span.End > line.End || line.Start == trivia.SpanStart) {
                // A comment on a line of its own has nothing to its left to align against.
                Flush(runs, run);
                previousLine = -2;
                continue;
            }

            if (line.LineNumber != previousLine + 1) {
                Flush(runs, run);
            }

            run.Add(new Row(line.Start, One(trivia.SpanStart)));
            previousLine = line.LineNumber;
        }

        Flush(runs, run);
    }

    /// <summary>
    ///     Turns the runs into insertions and applies them.
    /// </summary>
    /// <remarks>
    ///     ⚠ Slots are resolved left to right and each carries the shift the ones before it introduced,
    ///     because the column of the second slot depends on how far the first one moved. Doing it in one
    ///     pass over the original columns aligns the first slot and leaves the second ragged by exactly
    ///     the padding the first one added.
    /// </remarks>
    static string Pad(string text, List<List<Row>> runs) {
        // offset → spaces to insert before it. Two runs can want padding at the same offset — a
        // field's `=` is a slot of the fields run and the text before a trailing comment is a slot
        // of the comments run — and the wider of the two wins rather than the two summing.
        var insertions = new Dictionary<int, int>();

        foreach (var run in runs) {
            var arity = run[0].Slots.Length;
            var shift = new int[run.Count];
            for (var slot = 0; slot < arity; slot++) {
                var target = 0;
                for (var i = 0; i < run.Count; i++) {
                    target = Math.Max(target, ColumnOf(text, run[i], slot) + shift[i]);
                }

                for (var i = 0; i < run.Count; i++) {
                    var pad = target - (ColumnOf(text, run[i], slot) + shift[i]);
                    if (pad <= 0) {
                        continue;
                    }

                    var at = run[i].Slots[slot];
                    insertions[at] = Math.Max(insertions.GetValueOrDefault(at), pad);
                    shift[i] += pad;
                }
            }
        }

        if (insertions.Count == 0) {
            return text;
        }

        var builder = new System.Text.StringBuilder(text.Length + insertions.Count * 4);
        var cursor = 0;
        foreach (var offset in insertions.Keys.Order()) {
            builder.Append(text, cursor, offset - cursor);
            builder.Append(' ', insertions[offset]);
            cursor = offset;
        }

        builder.Append(text, cursor, text.Length - cursor);
        return builder.ToString();
    }

    static int ColumnOf(string text, in Row row, int slot) => TextWidth.Measure(text[row.LineStart..row.Slots[slot]]);
}
