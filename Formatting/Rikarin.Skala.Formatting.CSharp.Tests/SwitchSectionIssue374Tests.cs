using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A <c>switch</c> statement written on one line (issue #374, SK-DIV-0115): the oracle puts each
///     section on a line of its own and the closing brace on its own, keeps a <em>simple</em> section's
///     label and statements together, and breaks every other section one statement per line.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-09-17 with <c>Testing ask</c>
///     under the repository's configuration and with each candidate key flipped alone:
///     <c>skala_keep_existing_embedded_block_arrangement</c>,
///     <c>skala_keep_existing_declaration_block_arrangement</c>, <c>skala_keep_user_linebreaks</c>,
///     <c>csharp_preserve_single_line_blocks</c> and <c>skala_place_simple_case_statement_on_same_line</c>.
///     None moved a section, so the rule is unconditional; the one key that moved anything nearby is
///     the embedded key, which keeps a braced section's block whole, and that is
///     <see cref="ABracedSectionsBlock_IsTheEmbeddedKeys" />.
///     The committed fixture is <c>Testing/corpus/constructs/breaks/switch-sections.cs</c>.
/// </remarks>
public sealed class SwitchSectionIssue374Tests {
    /// <summary>
    ///     The repository's configuration as the corpus has it: <c>csharp_prefer_braces</c> off, because
    ///     the root's own <c>true</c> would wrap every embedded statement here in a block (SK-DIV-0100)
    ///     and the oracle was asked without it.
    /// </summary>
    static string FormatWith(string source, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [
                    new KeyValuePair<string, string>("csharp_prefer_braces", "false"),
                    .. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))
                ]
            )
                .Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    /// <summary>
    ///     The body of a method, formatted and stable on a second pass, with the method's own lines
    ///     and indentation removed.
    /// </summary>
    static string[] Body(string member) {
        var formatted = FormatWith($"class C {{\n    int x;\n    {member}\n    void M() {{ }}\n}}\n");
        Assert.Equal(formatted, FormatWith(formatted));
        return formatted.Split('\n')
            .SkipWhile(static line => !line.StartsWith("    void S(", StringComparison.Ordinal)
                && !line.StartsWith("    int S(", StringComparison.Ordinal)
                && !line.StartsWith("    async ", StringComparison.Ordinal)
                && !line.StartsWith("    System.Collections", StringComparison.Ordinal)
            )
            .Skip(1)
            .TakeWhile(static line => line != "    }")
            .Select(static line => line.Length > 8 ? line[8..] : line.TrimStart())
            .ToArray();
    }

    /// <summary>The issue's input, and the oracle's answer to it byte for byte.</summary>
    [Fact]
    public void TheIssuesInput_ComesBackAsTheOracleWritesIt() {
        const string source =
            "class C {\n"
            + "    void A(bool b) { if (b) { M(); M(); } }\n"
            + "    void B(object o) { switch (o) { case 1: break; case 2: break; } }\n"
            + "    void D(object o) { switch (o) { case 1: M(); break; } }\n"
            + "    void M() { }\n"
            + "}\n";

        const string expected =
            "class C {\n"
            + "    void A(bool b) {\n"
            + "        if (b) {\n"
            + "            M();\n"
            + "            M();\n"
            + "        }\n"
            + "    }\n"
            + "\n"
            + "    void B(object o) {\n"
            + "        switch (o) {\n"
            + "            case 1: break;\n"
            + "            case 2: break;\n"
            + "        }\n"
            + "    }\n"
            + "\n"
            + "    void D(object o) {\n"
            + "        switch (o) {\n"
            + "            case 1: M(); break;\n"
            + "        }\n"
            + "    }\n"
            + "\n"
            + "    void M() { }\n"
            + "}\n";

        var formatted = Format.Text(source);
        Assert.Equal(expected, formatted);
        Assert.Equal(expected, Format.Text(formatted));
    }

    /// <summary>
    ///     A simple section — <c>[S]</c> or <c>[S, break;]</c> with <c>S</c> an expression, empty,
    ///     <c>return</c>, <c>throw</c>, <c>break</c>, <c>continue</c>, <c>goto</c> or <c>yield</c>
    ///     statement — keeps the label's line.
    /// </summary>
    [Theory]
    [InlineData(
        "void S(object o) { switch (o) { case 1: break; case 2: break; } }",
        "case 1: break;",
        "case 2: break;"
    )]
    [InlineData("void S(object o) { switch (o) { case 1: M(); break; } }", "case 1: M(); break;")]
    [InlineData(
        "void S(object o) { switch (o) { case 1: x = 1; break; case 2: x++; break; } }",
        "case 1: x = 1; break;",
        "case 2: x++; break;"
    )]
    [InlineData(
        "int S(object o) { switch (o) { case 1: return 1; default: return 0; } }",
        "case 1: return 1;",
        "default: return 0;"
    )]
    [InlineData(
        "void S(object o) { switch (o) { case 1: break; default: throw new System.Exception(); } }",
        "case 1: break;",
        "default: throw new System.Exception();"
    )]
    [InlineData(
        "void S(object o) { switch (o) { case 1: goto case 2; case 2: break; } }",
        "case 1: goto case 2;",
        "case 2: break;"
    )]
    [InlineData(
        "void S(object o) { while (true) { switch (o) { case 1: continue; } } }",
        "while (true) {",
        "    switch (o) {",
        "        case 1: continue;",
        "    }",
        "}"
    )]
    [InlineData(
        "System.Collections.Generic.IEnumerable<int> S(object o) { switch (o) { case 1: yield return 1; break; case 2: yield break; } }",
        "case 1: yield return 1; break;",
        "case 2: yield break;"
    )]
    [InlineData(
        "void S(object o) { switch (o) { case 1: Run(() => { M(); }); break; } }",
        "case 1: Run(() => { M(); }); break;"
    )]
    [InlineData(
        "async System.Threading.Tasks.Task S(object o) { switch (o) { case 1: await System.Threading.Tasks.Task.Delay(1); break; } }",
        "case 1: await System.Threading.Tasks.Task.Delay(1); break;"
    )]
    [InlineData(
        "void S(object o) { switch (o) { case 1: M(); break; case int i when i > 2: M(); break; case string { Length: 3 }: break; } }",
        "case 1: M(); break;",
        "case int i when i > 2: M(); break;",
        "case string { Length: 3 }: break;"
    )]
    [InlineData(
        "void S(object o) { switch (o) { case 1: M(); break; } M(); }",
        "switch (o) {",
        "    case 1: M(); break;",
        "}",
        "",
        "M();"
    )]
    public void ASimpleSection_KeepsTheLabelsLine(string member, params string[] expectedSectionLines) {
        // A row that starts at a label is the switch's contents; any other row is the whole body.
        var isSections = expectedSectionLines[0].StartsWith("case", StringComparison.Ordinal)
            || expectedSectionLines[0].StartsWith("default", StringComparison.Ordinal);
        string[] expected = isSections
            ? ["switch (o) {", .. expectedSectionLines.Select(static line => "    " + line), "}"]
            : expectedSectionLines;

        Assert.Equal(expected, Body(member));
    }

    /// <summary>
    ///     Anything else is broken one statement per line, the first off the label — whatever the
    ///     source wrote and whichever statement is the second one.
    /// </summary>
    [Theory]
    [InlineData(
        "void S(object o) { switch (o) { case 1: M(); M(); break; } }",
        "case 1:",
        "    M();",
        "    M();",
        "    break;"
    )]
    [InlineData(
        "void S(object o) { switch (o) { case 1: M(); M(); M(); break; } }",
        "case 1:",
        "    M();",
        "    M();",
        "    M();",
        "    break;"
    )]
    [InlineData("void S(object o) { switch (o) { case 1: M(); return; } }", "case 1:", "    M();", "    return;")]
    [InlineData(
        "void S(object o) { switch (o) { case 1: M(); goto case 2; case 2: break; } }",
        "case 1:",
        "    M();",
        "    goto case 2;",
        "case 2: break;"
    )]
    [InlineData(
        "void S(object o) { switch (o) { case 1: M(); throw new System.Exception(); } }",
        "case 1:",
        "    M();",
        "    throw new System.Exception();"
    )]
    [InlineData(
        "void S(object o) { switch (o) { case 1: x = 1; x = 2; break; } }",
        "case 1:",
        "    x = 1;",
        "    x = 2;",
        "    break;"
    )]
    [InlineData(
        "void S(object o) { switch (o) { case 1: var y = 1; break; } }",
        "case 1:",
        "    var y = 1;",
        "    break;"
    )]
    [InlineData("void S(object o) { switch (o) { case 1: int y = 1; } }", "case 1:", "    int y = 1;")]
    [InlineData("void S(object o) { switch (o) { case 1: M(); } }", "case 1: M();")]
    [InlineData("void S(object o) { switch (o) { case 1: if (x > 0) M(); } }", "case 1:", "    if (x > 0) M();")]
    [InlineData(
        "void S(object o) { switch (o) { case 1: if (x > 0) M(); break; } }",
        "case 1:",
        "    if (x > 0) M();",
        "    break;"
    )]
    [InlineData(
        "void S(object o) { switch (o) { case 1: lock (o) M(); break; } }",
        "case 1:",
        "    lock (o) M();",
        "    break;"
    )]
    [InlineData(
        "void S(object o) { switch (o) { case 1: M(); break; default: M(); M(); break; } }",
        "case 1: M(); break;",
        "default:",
        "    M();",
        "    M();",
        "    break;"
    )]
    public void AnyOtherSection_BreaksOneStatementPerLine(string member, params string[] expectedSectionLines) =>
        Assert.Equal(["switch (o) {", .. expectedSectionLines.Select(static line => "    " + line), "}"], Body(member));

    /// <summary>
    ///     Stacked labels each take a line; the statements stay with the last one; an empty switch
    ///     stays together; a braced section opens on its label and is expanded by the block's rule.
    /// </summary>
    [Theory]
    [InlineData(
        "void S(object o) { switch (o) { case 1: case 2: break; case 3: case 4: M(); break; } }",
        "case 1:",
        "case 2: break;",
        "case 3:",
        "case 4: M(); break;"
    )]
    [InlineData(
        "void S(object o) { switch (o) { case 1: { M(); break; } } }",
        "case 1: {",
        "    M();",
        "    break;",
        "}"
    )]
    [InlineData("void S(object o) { switch (o) { case 1:\n case 2: break; } }", "case 1:", "case 2: break;")]
    public void LabelsAndBracedSections(string member, params string[] expectedSectionLines) =>
        Assert.Equal(["switch (o) {", .. expectedSectionLines.Select(static line => "    " + line), "}"], Body(member));

    [Fact]
    public void AnEmptySwitch_StaysTogether() =>
        Assert.Equal(["switch (o) { }"], Body("void S(object o) { switch (o) { } }"));

    /// <summary>
    ///     The sections are one per line wherever the source put the braces: on the first section's
    ///     line, after the last one's, or around a run of joined sections.
    /// </summary>
    [Theory]
    [InlineData("void S(object o) {\n switch (o) { case 1: break;\n case 2: break; }\n }")]
    [InlineData("void S(object o) {\n switch (o) {\n case 1: break; case 2: break;\n }\n }")]
    [InlineData("void S(object o) {\n switch (o) {\n case 1: break;\n case 2: break; }\n }")]
    public void SectionsJoinedInAnyPartOfTheStatement_AreSeparated(string member) =>
        Assert.Equal(["switch (o) {", "    case 1: break;", "    case 2: break;", "}"], Body(member));

    /// <summary>
    ///     A simple section is a fill, not an all-or-nothing group: a kept label break keeps the tail
    ///     together; a break the author wrote <em>between</em> the statements breaks the label gap too.
    /// </summary>
    [Fact]
    public void ASimpleSectionsKeptBreaks_AreTheOracles() =>
        Assert.Equal(
            [
                "switch (o) {",
                "    case 1:",
                "        M(); break;",
                "    case 2:",
                "        M();",
                "        break;",
                "    case 3:",
                "        M();",
                "        break;",
                "}"
            ],
            Body(
                "void S(object o) {\n switch (o) {\n case 1:\n M(); break;\n case 2: M();\n break;\n case 3:\n M();\n break;\n }\n }"
            )
        );

    /// <summary>
    ///     The width rule, at the margin: a 120-column section stays; at 121 the statements leave the
    ///     label together; they split only when the tail still overflows; and behind a kept label
    ///     break the tail fills the same way (120 stays, 121 splits).
    /// </summary>
    [Theory]
    [InlineData(90, false, "case 1: {0}(); break;")]
    [InlineData(91, false, "case 1:", "    {0}(); break;")]
    [InlineData(101, false, "case 1:", "    {0}();", "    break;")]
    [InlineData(94, true, "case 1:", "    {0}(); break;")]
    [InlineData(95, true, "case 1:", "    {0}();", "    break;")]
    public void ASimpleSectionAtTheMargin_FillsAsTheOracleDoes(
        int nameWidth,
        bool labelBreakKept,
        params string[] expectedSectionLines
    ) {
        var name = new string('M', nameWidth);
        var gap = labelBreakKept ? "\n" : " ";
        var body = Body($"void S(object o) {{ switch (o) {{ case 1:{gap}{name}(); break; }} }}");
        Assert.Equal(
            ["switch (o) {", .. expectedSectionLines.Select(line => "    " + string.Format(null, line, name)), "}"],
            body
        );
    }

    /// <summary>
    ///     ⚠ The one key the probes found in the area. <c>skala_keep_existing_embedded_block_arrangement = true</c>
    ///     keeps <c>if (b) { M(); }</c> and a braced section's <c>{ M(); }</c> whole — and still puts
    ///     each section on its own line — while the declaration key keeps neither. <c>Keeps</c> sent
    ///     the section's block to the declaration key because a section is not a statement.
    /// </summary>
    [Fact]
    public void ABracedSectionsBlock_IsTheEmbeddedKeys() {
        const string source =
            "class C {\n    void S(object o, bool b) { switch (o) { case 1: { M(); } case 2: if (b) { M(); } break; } }\n    void M() { }\n}\n";

        var embedded = FormatWith(source, ("skala_keep_existing_embedded_block_arrangement", "true"));
        Assert.Contains(
            "        switch (o) {\n            case 1: { M(); }\n            case 2:\n                if (b) { M(); }\n\n                break;\n        }\n",
            embedded,
            StringComparison.Ordinal
        );

        // ⚠ Only the sections are asserted here: at this key Skala also keeps the *method's* brace on
        // its line (`void S(…) { switch (o) {`), where the oracle expands a body whose contents span
        // lines. That is the key's own, older gap and not this issue's.
        var declaration = FormatWith(source, ("skala_keep_existing_declaration_block_arrangement", "true"));
        Assert.Contains(
            "            case 1: {\n                M();\n            }\n            case 2:\n                if (b) {\n                    M();\n                }\n\n                break;\n",
            declaration,
            StringComparison.Ordinal
        );
    }

    /// <summary>
    ///     ⚠ The sections' breaks answer to no key. Each of the candidates flipped alone leaves the
    ///     oracle's four lines exactly where they are.
    /// </summary>
    [Theory]
    [InlineData("skala_keep_existing_embedded_block_arrangement", "true")]
    [InlineData("skala_keep_existing_declaration_block_arrangement", "true")]
    [InlineData("skala_keep_user_linebreaks", "false")]
    [InlineData("csharp_preserve_single_line_blocks", "false")]
    [InlineData("skala_place_simple_case_statement_on_same_line", "true")]
    [InlineData("skala_place_simple_case_statement_on_same_line", "false")]
    public void NoKeyKeepsASwitchOnOneLine(string key, string value) {
        var formatted = FormatWith(
            "class C {\n    void S(object o) { switch (o) { case 1: break; case 2: break; } }\n}\n",
            (key, value)
        );

        // The method's own brace is the declaration key's business (see the test above); the
        // switch's sections and closing brace are on lines of their own at every one of these.
        Assert.Contains(
            "switch (o) {\n            case 1: break;\n            case 2: break;\n        }",
            formatted,
            StringComparison.Ordinal
        );
    }

    /// <summary>
    ///     A <c>switch</c> — even an empty one — or a <c>try</c> that is somebody's embedded statement
    ///     leaves the header's line, at the export's <c>keep_existing_embedded_arrangement = true</c>.
    /// </summary>
    [Theory]
    [InlineData(
        "void S(object o) { if (x > 0) switch (o) { case 1: break; } }",
        "if (x > 0)",
        "    switch (o) {",
        "        case 1: break;",
        "    }"
    )]
    [InlineData("void S(object o) { while (x > 0) switch (o) { } }", "while (x > 0)", "    switch (o) { }")]
    [InlineData(
        "void S(object o) { foreach (var i in new[] { 1 }) switch (i) { case 1: break; } }",
        "foreach (var i in new[] { 1 })",
        "    switch (i) {",
        "        case 1: break;",
        "    }"
    )]
    [InlineData(
        "void S(object o) { lock (o) switch (o) { case 1: break; } }",
        "lock (o)",
        "    switch (o) {",
        "        case 1: break;",
        "    }"
    )]
    [InlineData(
        "void S(object o) { if (x > 0) try { M(); } finally { M(); } }",
        "if (x > 0)",
        "    try {",
        "        M();",
        "    } finally {",
        "        M();",
        "    }"
    )]
    [InlineData(
        "void S(object o) { if (x > 0)\n switch (o) { case 1: break; } }",
        "if (x > 0)",
        "    switch (o) {",
        "        case 1: break;",
        "    }"
    )]
    public void AnEmbeddedSwitchOrTry_LeavesTheHeadersLine(string member, params string[] expected) =>
        Assert.Equal(expected, Body(member));

    /// <summary>A switch <em>expression</em> on one line is untouched by any of this (SK-DIV-0107, #370).</summary>
    [Fact]
    public void ASwitchExpression_IsNotASwitchStatement() =>
        Assert.Equal(
            ["return o switch {", "    1 => 1,", "    2 => 2,", "    _ => 0", "};"],
            Body("int S(object o) { return o switch { 1 => 1, 2 => 2, _ => 0 }; }")
        );
}
