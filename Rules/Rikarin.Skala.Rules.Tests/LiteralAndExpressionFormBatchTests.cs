using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     <c>SK1060</c>–<c>SK1064</c>: the literal and expression forms, shape by shape.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         Every rule in this batch is gated on a C# version, and two of them cover shapes whose
///         floors differ.
///     </b> A single rule-level floor would either silence the older shape on an older
///     project or emit newer syntax into one, so the gate is asserted per shape wherever the shapes
///     disagree — not once per rule.
///     <para>
///         ⚠ <see cref="Analyze" /> refuses an <c>AD0001</c>. A crashed analyzer produces nothing,
///         which passes every negative fixture and fails only the positives; a batch that did not
///         check for it would read a crash as a well-behaved rule.
///     </para>
/// </remarks>
public sealed class LiteralAndExpressionFormBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new IndexFromEndAnalyzer(), new NameofExpressionAnalyzer(), new EscapeFreeStringLiteralAnalyzer(),
        new InterpolatedStringFormAnalyzer(), new UnsignedRightShiftAnalyzer()
    ];

    [Theory]
    [InlineData("using System.Collections.Generic; class C { string M(List<string> x) => x[x.Count - 1]; }", "^1")]
    [InlineData("class C { byte M(byte[] b) => b[b.Length - 1]; }", "^1")]
    [InlineData("class C { char M(string s) => s[s.Length - 1]; }", "^1")]
    [InlineData("class C { byte M(byte[] b, int n) => b[b.Length - n]; }", "^n")]
    [InlineData("using System; class C { byte M(Span<byte> b) => b[b.Length - 2]; }", "^2")]
    [InlineData(
        "using System.Collections.Generic; class C { string M(IReadOnlyList<string> x) => x[x.Count - 1]; }",
        "^1"
    )]
    public void IndexFromEnd_Fires(string source, string replacement) {
        var finding = Assert.Single(Analyze(source, LanguageVersion.CSharp12));
        Assert.Equal("SK1060", finding.Id);
        Assert.Contains(replacement, finding.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The type test, which is the rule. "Has a <c>Count</c>" is not it in either direction.
    /// </summary>
    [Theory]
    // A different collection on each side: the defect the rule exists to make unwritable.
    [InlineData(
        "using System.Collections.Generic; class C { string M(List<string> a, List<string> b) => a[b.Count - 1]; }"
    )]
    // Countable and `int`-indexed, and `^1` would read as an ordinal position it does not have.
    [InlineData("using System.Collections.Generic; class C { string M(Dictionary<int, string> d) => d[d.Count - 1]; }")]
    // The same shape on a hand-written type the language would accept and a reader would not.
    [InlineData(
        "class T { public int Count => 0; public string this[int h] => \"\"; } class C { string M(T t) => t[t.Count - 1]; }"
    )]
    // Evaluated twice today, once after the rewrite.
    [InlineData(
        "using System.Collections.Generic; class C { List<string> G() => new(); string M() => G()[G().Count - 1]; }"
    )]
    // `^0` is `Count`, which is not the last element.
    [InlineData("using System.Collections.Generic; class C { string M(List<string> x) => x[x.Count - 0]; }")]
    // `Index`'s constructor rejects a negative value; the subtraction reaches the indexer instead.
    [InlineData(
        "using System.Collections.Generic; class C { const int B = -1; string M(List<string> x) => x[x.Count - B]; }"
    )]
    // Not a name path, so the rule does not move it.
    [InlineData(
        "using System.Collections.Generic; class C { int B() => 1; string M(List<string> x) => x[x.Count - B()]; }"
    )]
    // An addition is not an index from the end.
    [InlineData("using System.Collections.Generic; class C { string M(List<string> x, int n) => x[x.Count + n]; }")]
    // A `long` offset is not an `Index` operand.
    [InlineData(
        "using System.Collections.Generic; class C { string M(List<string> x, long n) => x[(int)(x.Count - n)]; }"
    )]
    public void IndexFromEnd_DeclinesTheNearestMiss(string source) =>
        Assert.Empty(Analyze(source, LanguageVersion.CSharp12));

    [Fact]
    public void IndexFromEnd_RequiresCSharp8() {
        const string source =
            "using System.Collections.Generic; class C { string M(List<string> x) => x[x.Count - 1]; }";

        Assert.Empty(Below(source, LanguageVersion.CSharp7_3).Where(static d => d.Id == "SK1060"));
        Assert.NotEmpty(Analyze(source, LanguageVersion.CSharp8));
    }

    [Theory]
    [InlineData("class W { } class C { string M() => typeof(W).Name; }", "nameof(W)")]
    [InlineData("namespace N { class W { } } class C { string M() => typeof(N.W).Name; }", "nameof(N.W)")]
    [InlineData("enum E { Red } class C { string M() => E.Red.ToString(); }", "nameof(E.Red)")]
    [InlineData(
        "using System; class C { void M(int count) { throw new ArgumentOutOfRangeException(\"count\", count, null); } }",
        "nameof(count)"
    )]
    [InlineData(
        "using System; class C { void M(object o) { ArgumentNullException.ThrowIfNull(o, \"o\"); } }",
        "nameof(o)"
    )]
    [InlineData(
        "using System.ComponentModel; class C { public string T { get; set; } = \"\"; "
        + "PropertyChangedEventArgs M() => new PropertyChangedEventArgs(\"T\"); }",
        "nameof(T)"
    )]
    public void Nameof_Fires(string source, string replacement) {
        var finding = Assert.Single(Analyze(source, LanguageVersion.CSharp12));
        Assert.Equal("SK1061", finding.Id);
        Assert.Contains(replacement, finding.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The refusals, which are the rule. Four of these are literals that equal an identifier and
    ///     must never follow a rename; the rest are names <c>nameof</c> would answer differently.
    /// </summary>
    [Theory]
    // A bare literal that matches a member, in a position that says nothing about its meaning.
    [InlineData("class C { public int Count { get; set; } string M() => \"Count\"; }")]
    // The same literal as a dictionary key: a wire format, not a name.
    [InlineData(
        "using System.Collections.Generic; class C { public int Count { get; set; } "
        + "Dictionary<string, object> M() => new() { [\"Count\"] = Count }; }"
    )]
    // `typeof(List<int>).Name` is "List`1".
    [InlineData("using System.Collections.Generic; class C { string M() => typeof(List<int>).Name; }")]
    // `nameof(int)` does not compile.
    [InlineData("class C { string M() => typeof(int).Name; }")]
    // The run-time argument's name, not "T".
    [InlineData("class C<T> { string M() => typeof(T).Name; }")]
    // `nameof(Text)` is "Text" and `typeof(Text).Name` is "String".
    [InlineData("using Text = System.String; class C { string M() => typeof(Text).Name; }")]
    // An array's metadata name carries the brackets.
    [InlineData("class W { } class C { string M() => typeof(W[]).Name; }")]
    // ⚠ `Enum.ToString` answers with the first member declared with the value.
    [InlineData("enum E { Done = 1, Finished = 1 } class C { string M() => E.Finished.ToString(); }")]
    // A variable's `ToString`, which is a value and not a name.
    [InlineData("enum E { Red } class C { string M(E e) => e.ToString(); }")]
    // SK2017's shape: the literal names no parameter.
    [InlineData("using System; class C { void M(int count) { throw new ArgumentOutOfRangeException(\"size\"); } }")]
    // The message argument is prose, not an identifier, even when it happens to be one.
    [InlineData("using System; class C { void M(int count) { throw new ArgumentException(\"count\"); } }")]
    // No such property on this type: `nameof` written here would not bind.
    [InlineData(
        "using System.ComponentModel; class C { PropertyChangedEventArgs M() => new PropertyChangedEventArgs(\"V\"); }"
    )]
    // The empty name means "all properties changed".
    [InlineData(
        "using System.ComponentModel; class C { public string T { get; set; } = \"\"; "
        + "PropertyChangedEventArgs M() => new PropertyChangedEventArgs(\"\"); }"
    )]
    public void Nameof_DeclinesTheNearestMiss(string source) =>
        Assert.Empty(Analyze(source, LanguageVersion.CSharp12).Where(static d => d.Id == "SK1061"));

    /// <summary>
    ///     ⚠ <c>ArgumentException(string message)</c> is the counter-example that makes the
    ///     position test worth having: its single parameter is <c>message</c>, and the value people
    ///     pass it is frequently a parameter name.
    /// </summary>
    [Fact]
    public void Nameof_ReadsTheParameterAndNotTheValue() {
        const string message =
            "using System; class C { void M(int count) { throw new ArgumentException(\"count\"); } }";
        const string paramName =
            "using System; class C { void M(int count) { throw new ArgumentException(\"bad\", \"count\"); } }";

        Assert.Empty(Analyze(message, LanguageVersion.CSharp12).Where(static d => d.Id == "SK1061"));
        Assert.Single(Analyze(paramName, LanguageVersion.CSharp12).Where(static d => d.Id == "SK1061"));
    }

    [Fact]
    public void Nameof_RequiresCSharp6() {
        const string source = "class W { } class C { string M() => typeof(W).Name; }";

        Assert.Empty(Below(source, LanguageVersion.CSharp5).Where(static d => d.Id == "SK1061"));
        Assert.NotEmpty(Below(source, LanguageVersion.CSharp6).Where(static d => d.Id == "SK1061"));
    }

    /// <summary>
    ///     ⚠ Each case states the value the literal must still have after the fix, and the assertion
    ///     applies the edit and re-reads the constant rather than comparing strings. A fence one quote
    ///     short does not produce a subtly different literal — it produces a different program.
    /// </summary>
    [Theory]
    // A JSON body: every quote escaped today, none of them escaped after.
    [InlineData("class C { string M() => \"{\\\"id\\\":1}\"; }", "{\"id\":1}")]
    // A regex: the backslashes are the content.
    [InlineData("class C { string M() => \"\\\\d+\\\\.\\\\d+\"; }", "\\d+\\.\\d+")]
    // The verbatim spelling of the same JSON body.
    [InlineData("class C { string M() => @\"{\"\"id\"\":1}\"; }", "{\"id\":1}")]
    // Two quotes in a row still fit inside a three-quote fence: the run has to *reach* three.
    [InlineData("class C { string M() => \"a\\\"\\\"b\"; }", "a\"\"b")]
    // ⚠ Three quotes in a row is the case that needs a four-quote fence, and the only one in this
    // set where the delimiter arithmetic is load-bearing. A sabotage pinning the fence at three
    // turned nothing red until this case existed.
    [InlineData("class C { string M() => \"a\\\"\\\"\\\"b\"; }", "a\"\"\"b")]
    // A fence longer than the content needs.
    [InlineData("class C { string M() => \"\"\"\"abc\"\"\"\"; }", "abc")]
    // No floor at all on this one: an escape that is simply a character.
    [InlineData("class C { string M() => \"\\x41\"; }", "A")]
    public void EscapeFreeLiteral_FiresAndKeepsTheValue(string source, string value) {
        var finding = Assert.Single(Analyze(source, LanguageVersion.CSharp12));
        Assert.Equal("SK1062", finding.Id);

        var fixedSource = ApplyEdits(source, finding);
        var literal = CSharpSyntaxTree
            .ParseText(
                fixedSource,
                new CSharpParseOptions(LanguageVersion.Preview),
                cancellationToken: TestContext.Current.CancellationToken
            )
            .GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax>()
            .Single();

        Assert.Equal(value, literal.Token.ValueText);
        Assert.Empty(Analyze(fixedSource, LanguageVersion.CSharp12));
    }

    [Theory]
    // Already as plain as it gets.
    [InlineData("class C { string M() => \"hello\"; }")]
    // Verbatim, and no quote to double.
    [InlineData("class C { string M() => @\"C:\\Users\\app\"; }")]
    // ⚠ The fence is greedy, so a content ending in a quote has no single-line raw spelling.
    [InlineData("class C { string M() => \"say \\\"hi\\\"\"; }")]
    // …nor one starting with one.
    [InlineData("class C { string M() => \"\\\"hi\\\" he said\"; }")]
    // A tab cannot be written literally on one line, and `\\t` is not simplified.
    [InlineData("class C { string M() => \"a\\\\b\\tc\"; }")]
    // Nor a newline.
    [InlineData("class C { string M() => \"a\\\\b\\nc\"; }")]
    // The fence is already minimal.
    [InlineData("class C { string M() => \"\"\"a\"b\"\"\"; }")]
    // ⚠ `\\x` is greedy: this is U+041B, not `\\x41` followed by `B`.
    [InlineData("class C { string M() => \"\\x41B\"; }")]
    // The denoted character is a newline, which cannot be written literally.
    [InlineData("class C { string M() => \"\\u000a\"; }")]
    // SK1063's span, not this rule's.
    [InlineData("class C { string M() => $\"{\"a\\\\b\"}\"; }")]
    // An empty value has no raw spelling.
    [InlineData("class C { string M() => \"\"; }")]
    public void EscapeFreeLiteral_DeclinesTheNearestMiss(string source) =>
        Assert.Empty(Analyze(source, LanguageVersion.CSharp12).Where(static d => d.Id == "SK1062"));

    /// <summary>
    ///     ⚠ The gate is per shape, and this is the assertion that says so. The raw shapes are silent
    ///     on C# 10; the escape-simplification shape has no floor and fires on both.
    /// </summary>
    [Fact]
    public void EscapeFreeLiteral_GatesTheRawShapesAndNotTheEscapeShape() {
        const string rawShape = "class C { string M() => \"{\\\"id\\\":1}\"; }";
        const string escapeShape = "class C { string M() => \"\\x41\"; }";

        Assert.Empty(Analyze(rawShape, LanguageVersion.CSharp10).Where(static d => d.Id == "SK1062"));
        Assert.Single(Analyze(rawShape, LanguageVersion.CSharp11).Where(static d => d.Id == "SK1062"));

        Assert.Single(Analyze(escapeShape, LanguageVersion.CSharp10).Where(static d => d.Id == "SK1062"));
        Assert.Single(Below(escapeShape, LanguageVersion.CSharp7_3).Where(static d => d.Id == "SK1062"));
    }

    [Theory]
    [InlineData(
        "class C { string M(int d, int t) => string.Format(\"{0} of {1}\", d, t); }",
        "$\"{d} of {t}\""
    )]
    // ⚠ Four arguments binds `Format(string, params object[])`, not an explicitly typed overload.
    // The rule declined every one of those until a corpus sweep found it.
    [InlineData(
        "class C { string M(string a, string b, string c, string d) => string.Format(\"{0}{1}{2}{3}\", a, b, c, d); }",
        "$\"{a}{b}{c}{d}\""
    )]
    // Alignment and format clauses ride across unchanged.
    [InlineData("class C { string M(decimal a) => string.Format(\"{0,10:N2}\", a); }", "$\"{a,10:N2}\"")]
    // ⚠ A doubled brace means a literal brace in both grammars and must survive as one.
    [InlineData("class C { string M(int d) => string.Format(\"{{{0}}}\", d); }", "$\"{{{d}}}\"")]
    // A colon inside the format clause is not the clause separator.
    [InlineData(
        "using System; class C { string M(DateTime d) => string.Format(\"{0:HH:mm}\", d); }",
        "$\"{d:HH:mm}\""
    )]
    [InlineData("class C { string M(int n) => $\"{n.ToString()} left\"; }", "n")]
    [InlineData("class C { string M(string r, string n) => $\"{r}{\"/\"}{n}\"; }", "/")]
    public void InterpolatedForm_Fires(string source, string replacement) {
        var finding = Assert.Single(Analyze(source, LanguageVersion.CSharp12).Where(static d => d.Id == "SK1063"));
        Assert.Contains(replacement, finding.GetMessage(), StringComparison.Ordinal);

        var fixedSource = ApplyEdits(source, finding);
        var reparsed = CSharpSyntaxTree.ParseText(
            fixedSource,
            new CSharpParseOptions(LanguageVersion.Preview),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(
            reparsed.GetDiagnostics(TestContext.Current.CancellationToken),
            static d => d.Severity == DiagnosticSeverity.Error
        );
    }

    [Theory]
    // Evaluated twice after the rewrite, once before it.
    [InlineData("class C { string M(string n) => string.Format(\"{0} and {0}\", n); }")]
    // Evaluated in the order they are printed, not the order they are written.
    [InlineData("class C { string M(string a, string b) => string.Format(\"{1} {0}\", a, b); }")]
    // The provider is the point of the overload.
    [InlineData(
        "using System.Globalization; class C { string M(decimal a) => "
        + "string.Format(CultureInfo.InvariantCulture, \"{0:N2}\", a); }"
    )]
    // ⚠ One argument, not two: `{0}` means `v[0]`.
    [InlineData("class C { string M(object[] v) => string.Format(\"{0} {1}\", v); }")]
    // A colon inside a hole is grammar.
    [InlineData("class C { string M(bool f, string a, string b) => string.Format(\"{0}\", f ? a : b); }")]
    // A gap leaves an argument with nowhere to go.
    [InlineData("class C { string M(string a, string b) => string.Format(\"{0}\", a, b); }")]
    // A verbatim format string escapes its text differently.
    [InlineData("class C { string M(string n) => string.Format(@\"C:\\logs\\{0}\", n); }")]
    // The format string is not a literal at all.
    [InlineData("class C { const string F = \"{0}\"; string M(string n) => string.Format(F, n); }")]
    // ⚠ `null.ToString()` throws where `$\"{null}\"` renders empty.
    [InlineData("class C { string M(object v) => $\"{v.ToString()} left\"; }")]
    // Not covered: the instance method and `IFormattable` agree only for the BCL types.
    [InlineData("class C { string M(decimal a) => $\"{a.ToString(\"N2\")}\"; }")]
    // Nothing to remove.
    [InlineData("class C { string M(decimal a) => $\"{a:N2}\"; }")]
    public void InterpolatedForm_DeclinesTheNearestMiss(string source) =>
        Assert.Empty(Analyze(source, LanguageVersion.CSharp12).Where(static d => d.Id == "SK1063"));

    /// <summary>
    ///     ⚠ The two interactions that would make the rewrite wrong rather than merely noisy.
    /// </summary>
    /// <remarks>
    ///     A <c>FormattableString</c> overload beside the <c>string</c> one can rebind after the
    ///     rewrite, and in EF Core's <c>FromSql</c> family that is the difference between a
    ///     parameterised query and a concatenated one. The stand-in here declares both overloads
    ///     itself, so the assertion does not depend on a package being referenced.
    /// </remarks>
    [Fact]
    public void InterpolatedForm_DeclinesWhereAnInterpolationWouldRebind() {
        const string ambiguous = "using System; class Db { public static void Run(string s) { } "
            + "public static void Run(FormattableString s) { } } "
            + "class C { void M(string n) => Db.Run(string.Format(\"{0}\", n)); }";
        const string unambiguous = "using System; class Db { public static void Run(string s) { } } "
            + "class C { void M(string n) => Db.Run(string.Format(\"{0}\", n)); }";

        Assert.Empty(Analyze(ambiguous, LanguageVersion.CSharp12).Where(static d => d.Id == "SK1063"));
        Assert.Single(Analyze(unambiguous, LanguageVersion.CSharp12).Where(static d => d.Id == "SK1063"));
    }

    [Fact]
    public void InterpolatedForm_RequiresCSharp6() {
        const string source = "class C { string M(int d, int t) => string.Format(\"{0} of {1}\", d, t); }";

        Assert.Empty(Below(source, LanguageVersion.CSharp5).Where(static d => d.Id == "SK1063"));
        Assert.NotEmpty(Below(source, LanguageVersion.CSharp6).Where(static d => d.Id == "SK1063"));
    }

    [Theory]
    [InlineData("class C { int M(int h) => (int)((uint)h >> 16); }", "h >>> 16")]
    [InlineData("class C { long M(long s, int b) => (long)((ulong)s >> b); }", "s >>> b")]
    // The cast operand keeps its own parentheses, so it stays the left operand it was.
    [InlineData("class C { int M(int a, int b) => (int)((uint)(a + b) >> 4); }", "(a + b) >>> 4")]
    // ⚠ The nearest overflow context wins: an `unchecked` inside a `checked` restores the rule.
    [InlineData(
        "class C { int M(int h) { checked { return unchecked((int)((uint)h >> 16)); } } }",
        "h >>> 16"
    )]
    public void UnsignedShift_Fires(string source, string replacement) {
        var finding = Assert.Single(Analyze(source, LanguageVersion.CSharp12).Where(static d => d.Id == "SK1064"));
        Assert.Contains(replacement, finding.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The width proof, stated as tests. Everything narrower than <c>int</c> promotes before
    ///     <c>&gt;&gt;</c> runs, so the identical-looking spelling is a different program.
    /// </summary>
    [Theory]
    // For value = -1 and bits = 4 this gives 4095; `value >>> 4` gives -1.
    [InlineData("class C { short M(short v, int b) => (short)((ushort)v >> b); }")]
    [InlineData("class C { sbyte M(sbyte v, int b) => (sbyte)((byte)v >> b); }")]
    // Not the same width on both sides, so not a round trip.
    [InlineData("class C { long M(int h) => (long)((uint)h >> 16); }")]
    // The operand was unsigned already: this converts a result.
    [InlineData("class C { int M(uint h) => (int)(h >> 16); }")]
    // Both casts can throw here and `>>>` cannot.
    [InlineData("class C { int M(int h) { checked { return (int)((uint)h >> 16); } } }")]
    // ⚠ `>>>` binds below `+`, so the bare rewrite would be `h >>> (16 + 1)`.
    [InlineData("class C { int M(int h) => (int)((uint)h >> 16) + 1; }")]
    // …and below `&`.
    [InlineData("class C { int M(int h) => (int)((uint)h >> 16) & 255; }")]
    // A left shift does not sign-extend.
    [InlineData("class C { int M(int h) => (int)((uint)h << 16); }")]
    public void UnsignedShift_DeclinesTheNearestMiss(string source) =>
        Assert.Empty(Analyze(source, LanguageVersion.CSharp12).Where(static d => d.Id == "SK1064"));

    [Fact]
    public void UnsignedShift_RequiresCSharp11() {
        const string source = "class C { int M(int h) => (int)((uint)h >> 16); }";

        Assert.Empty(Analyze(source, LanguageVersion.CSharp10).Where(static d => d.Id == "SK1064"));
        Assert.Single(Analyze(source, LanguageVersion.CSharp11).Where(static d => d.Id == "SK1064"));
    }

    static string ApplyEdits(string source, Diagnostic diagnostic) {
        var count = int.Parse(diagnostic.Properties[FixEdits.CountKey]!);
        var text = source;
        for (var i = count - 1; i >= 0; i--) {
            var start = int.Parse(diagnostic.Properties[FixEdits.StartKey(i)]!);
            var length = int.Parse(diagnostic.Properties[FixEdits.LengthKey(i)]!);
            text = text[..start] + diagnostic.Properties[FixEdits.TextKey(i)] + text[(start + length)..];
        }

        return text;
    }

    static ImmutableArray<Diagnostic> Analyze(string source, LanguageVersion version) {
        var compilation = RuleFixtures.Compile(source, "test.cs", version);
        Assert.DoesNotContain(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            static d => d.Severity == DiagnosticSeverity.Error
        );

        var diagnostics = RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(diagnostics, static d => d.Id == "AD0001");
        return diagnostics;
    }

    /// <summary>
    ///     ⚠ The below-the-floor half, which cannot use <see cref="Analyze" />.
    /// </summary>
    /// <remarks>
    ///     <c>RuleFixtures.Compile</c> enables the nullable context unconditionally, and that is
    ///     <c>CS8630</c> below C# 8 — a compilation-option error, not a source one, and the source
    ///     still binds. Asserting "no error" here would make every floor below C# 8 untestable, so
    ///     this path asserts only that the analyzer did not crash and left the rule out.
    /// </remarks>
    static ImmutableArray<Diagnostic> Below(string source, LanguageVersion version) {
        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "test.cs", version),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(diagnostics, static d => d.Id == "AD0001");
        return diagnostics;
    }
}
