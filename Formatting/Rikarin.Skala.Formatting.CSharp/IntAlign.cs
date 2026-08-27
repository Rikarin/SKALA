using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Formatting;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
/// <c>int_align_*</c>: pad adjacent lines so that one token of each lands in the same column.
/// </summary>
/// <remarks>
/// ⚠ A pass over the <em>output</em>, run after the layout writer and before the edit emitter, and
/// it is the only part of the formatter that works that way. Two reasons, and the first is what
/// forces it:
/// <list type="number">
/// <item>
/// The column a token ends up in is not known until the document has been written. Alignment asks
/// "how wide is the widest of these five names once every wrapping decision is made", and the IR
/// has no node that can hold an answer that depends on its own siblings' final columns.
/// </item>
/// <item>
/// Every key here is <c>false</c> in the export, so this returns its input untouched for every file
/// the fidelity number is measured over — <see cref="PhaseOneOptions.IntAlignAnything"/> is the
/// early exit, and the second parse it guards costs nothing when nothing is aligned.
/// </item>
/// </list>
/// <para>
/// ⚠ It re-parses its input rather than carrying the tree through. The output is a different text
/// from the input — that is the point of a formatter — and every offset the alignment needs is an
/// offset into the output. Mapping them back through the anchor table would be the same parse plus
/// a lookup that can fail.
/// </para>
/// <para>
/// ⚠ Insertion only, and only of spaces between two tokens the space rules already separated. That
/// is what keeps <see cref="TokenEquivalence"/> satisfied: padding widens a gap that exists, and
/// never creates or removes one.
/// </para>
/// </remarks>
public static class IntAlign {
    /// <summary>The slots one construct contributes to its run, as offsets into the output.</summary>
    /// <remarks>
    /// ⚠ Fixed arity per kind. A construct that cannot fill every slot of its kind — a field with no
    /// initializer among fields that have one — <em>ends</em> the run rather than joining it with a
    /// hole, because a hole makes the column of the slots after it depend on which members happened
    /// to be missing, which is not a rule anybody can predict from the option name.
    /// </remarks>
    readonly record struct Row(int LineStart, ImmutableArray<int> Slots);

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
        SwitchSections
    }

    /// <summary>
    /// Walks every list of siblings in the tree and cuts it into runs of alignable neighbours.
    /// </summary>
    /// <remarks>
    /// ⚠ A blank line ends a run, and that is measured rather than assumed: asked directly, the
    /// oracle aligns three fields and three constants separated by one blank line to two different
    /// columns, not to one.
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
                if (row is null || line != previousLine + 1) {
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
            _ => null
        };

    /// <summary>The slots one construct offers, or null when it is not alignable.</summary>
    /// <remarks>
    /// ⚠ Every kind requires its construct to occupy exactly one output line. Alignment pads a gap
    /// on the line a token is on; a construct whose tokens are spread over three lines has no such
    /// gap to widen, and the oracle leaves it alone.
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
            _ => default
        };

        return slots.IsDefaultOrEmpty ? null : new Row(line.Start, slots);
    }

    static ImmutableArray<int> One(int a) => [a];

    static ImmutableArray<int> Two(int a, int b) => [a, b];

    /// <summary>
    /// <c>int_align_comments</c>: the trailing <c>//</c> comments of adjacent lines.
    /// </summary>
    /// <remarks>
    /// ⚠ Trivia rather than nodes, and it has to be: a trailing comment belongs to whatever token
    /// precedes it, and two comments on consecutive lines can hang off tokens in different
    /// constructs at different depths. The run is the run of <em>lines</em>.
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
    /// Turns the runs into insertions and applies them.
    /// </summary>
    /// <remarks>
    /// ⚠ Slots are resolved left to right and each carries the shift the ones before it introduced,
    /// because the column of the second slot depends on how far the first one moved. Doing it in one
    /// pass over the original columns aligns the first slot and leaves the second ragged by exactly
    /// the padding the first one added.
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
