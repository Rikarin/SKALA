using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Testing;

/// <summary>
/// Random compilation units, from a grammar weighted toward what the formatter handles specially.
/// </summary>
/// <remarks>
/// ⚠ docs/plan/12 § "Fuzzing" names the weighting and it is the whole design: "generics, lambdas,
/// patterns, initializers, attributes, raw strings". A uniform grammar spends its budget on
/// <c>a + b</c> and never reaches a nested generic type argument list at column 118, which is where
/// the fitting engine makes the decisions docs/plan/16 § R2 calls the project's only genuinely novel
/// code — and where M3 found two of the fitter's four measures returning zero since the milestone
/// they were written in.
/// <para>
/// ⚠ The generator is required to produce a compilation unit with <b>no parse errors</b>, and the
/// driver checks. Semantic nonsense is fine and is not avoided — an unresolved type, an operator of
/// the wrong arity, a <c>yield return</c> outside an iterator all produce diagnostics from the
/// binder rather than from the parser, and the formatter is syntactic. A *parse* error is different:
/// ADR-003 leaves such a file byte-identical, so a generator that emits one has produced a case that
/// asserts nothing while appearing to assert everything.
/// </para>
/// <para>
/// ⚠ Nothing here randomises whitespace. doc 12 asks for the trees to be "printed with random
/// whitespace", and the driver does that by running the generated text through
/// <see cref="FuzzMutations"/> — the same parse-preserving mutations the mutation half uses, which
/// already know that a space inside a raw string is data. Two implementations of "where may
/// whitespace go" is one more than the number that can be kept correct.
/// </para>
/// </remarks>
public sealed class FuzzGenerator {
    /// <summary>The type names the grammar draws from. Unresolvable on purpose; the formatter is syntactic.</summary>
    static readonly ImmutableArray<string> SimpleTypes = [
        "int", "string", "bool", "double", "object", "byte", "long", "char", "decimal", "Guid",
        "DateTime", "TimeSpan", "CancellationToken", "StringBuilder"
    ];

    static readonly ImmutableArray<string> GenericTypes = [
        "List<{0}>", "IReadOnlyList<{0}>", "Dictionary<string, {0}>", "IEnumerable<{0}>",
        "Task<{0}>", "ValueTask<{0}>", "Func<{0}, {0}>", "Nullable<{0}>", "ImmutableArray<{0}>",
        "IReadOnlyDictionary<{0}, {0}>", "Span<{0}>", "Memory<{0}>", "Lazy<{0}>",
        "Dictionary<{0}, List<{0}>>"
    ];

    static readonly ImmutableArray<string> Attributes = [
        "Obsolete", "Pure", "DebuggerStepThrough", "EditorBrowsable(EditorBrowsableState.Never)",
        "MethodImpl(MethodImplOptions.AggressiveInlining)", "Obsolete(\"replaced\", false)",
        "SuppressMessage(\"Design\", \"CA1000:DoNotDeclareStaticMembersOnGenericTypes\")",
        "InlineArray(8)", "StringSyntax(StringSyntaxAttribute.Regex)"
    ];

    readonly FuzzRandom random;
    readonly StringBuilder builder = new();
    int counter;

    FuzzGenerator(FuzzRandom random) => this.random = random;

    /// <summary>One random compilation unit.</summary>
    public static string Compile(FuzzRandom random) {
        var generator = new FuzzGenerator(random);
        generator.Unit();
        return generator.builder.ToString();
    }

    // ── the unit ─────────────────────────────────────────────────────────────────────────────────

    void Unit() {
        foreach (var name in new[] {
                     "System", "System.Collections.Generic", "System.Linq", "System.Threading.Tasks"
                 }) {
            if (random.Chance(0.6)) {
                Line(0, "using " + name + ";");
            }
        }

        if (random.Chance(0.2)) {
            Line(0, "using Alias" + Next() + " = System.Collections.Generic.Dictionary<string, int>;");
        }

        if (random.Chance(0.15)) {
            Line(0, "[assembly: CLSCompliant(false)]");
        }

        Blank();

        var scoped = random.Chance(0.45);
        var namespaced = scoped || random.Chance(0.5);
        var indent = 0;
        if (scoped) {
            Line(0, "namespace Fuzz.N" + Next() + ";");
            Blank();
        } else if (namespaced) {
            Line(0, "namespace Fuzz.N" + Next() + " {");
            indent = 1;
        }

        var types = random.Next(1, 4);
        for (var i = 0; i < types; i++) {
            if (i > 0) {
                Blank();
            }

            TypeDeclaration(indent, 0);
        }

        if (namespaced && !scoped) {
            Line(0, "}");
        }
    }

    // ── types ────────────────────────────────────────────────────────────────────────────────────

    void TypeDeclaration(int indent, int depth) {
        AttributeLines(indent);

        var kind = random.Pick(
            ["class", "struct", "record", "record struct", "readonly struct", "interface", "enum"],
            [30, 12, 14, 8, 5, 8, 4]
        );

        if (kind == "enum") {
            Line(indent, "public enum E" + Next() + " {");
            var values = random.Next(1, 6);
            for (var i = 0; i < values; i++) {
                Line(indent + 1, "V" + Next() + (random.Chance(0.4) ? " = " + random.Next(64) : string.Empty) + ",");
            }

            Line(indent, "}");
            return;
        }

        var name = "T" + Next();
        var parameters = TypeParameters();
        var head = new StringBuilder();

        // ⚠ The modifier sequence is built rather than concatenated, because two of its rules are
        // enforced by the *parser*: `readonly` must precede the type keyword (CS1585), and `partial`
        // must be the last modifier before it (CS1585 again, from the other direction). `public
        // partial readonly struct` breaks both and does not parse.
        head.Append(random.Pick(["public", "internal", "public sealed", "internal sealed"]));
        if (kind.StartsWith("readonly ", StringComparison.Ordinal)) {
            head.Append(" readonly");
            kind = kind["readonly ".Length..];
        }

        if (random.Chance(0.15)) {
            head.Append(" partial");
        }

        head.Append(' ').Append(kind).Append(' ').Append(name).Append(parameters.List);

        // A record's primary constructor: a delimited parameter list the formatter has its own
        // wrapping rules for, and the only place where a *type* declaration carries one.
        var primary = kind.Contains("record", StringComparison.Ordinal) && random.Chance(0.6);
        if (primary) {
            head.Append('(').Append(ParameterList(random.Next(1, 5), allowModifiers: false)).Append(')');
        }

        if (random.Chance(0.4)) {
            var bases = new List<string> { "IDisposable" };
            if (random.Chance(0.6)) {
                bases.Add("IEnumerable<" + Type(0) + ">");
            }

            if (random.Chance(0.3)) {
                bases.Add("IComparable<" + name + parameters.List + ">");
            }

            head.Append(" : ").Append(string.Join(", ", bases));
        }

        head.Append(parameters.Constraints);

        if (primary && random.Chance(0.35)) {
            Line(indent, head.ToString() + ";");
            return;
        }

        Line(indent, head.ToString() + " {");

        var members = random.Next(1, kind == "interface" ? 4 : 7);
        for (var i = 0; i < members; i++) {
            if (i > 0 && random.Chance(0.6)) {
                Blank();
            }

            if (kind == "interface") {
                InterfaceMember(indent + 1);
            } else {
                Member(indent + 1, depth);
            }
        }

        Line(indent, "}");
    }

    (string List, string Constraints) TypeParameters() {
        if (!random.Chance(0.4)) {
            return (string.Empty, string.Empty);
        }

        var count = random.Next(1, 4);
        var names = new List<string>();
        for (var i = 0; i < count; i++) {
            names.Add("T" + Next());
        }

        var constraints = new StringBuilder();
        foreach (var name in names) {
            if (!random.Chance(0.55)) {
                continue;
            }

            var parts = new List<string> { random.Pick(["class", "struct", "notnull", "IDisposable", "unmanaged"]) };
            if (random.Chance(0.4)) {
                parts.Add("IEquatable<" + name + ">");
            }

            if (parts[0] != "struct" && parts[0] != "unmanaged" && random.Chance(0.4)) {
                parts.Add("new()");
            }

            constraints.Append(" where ").Append(name).Append(" : ").Append(string.Join(", ", parts));
        }

        return ("<" + string.Join(", ", names) + ">", constraints.ToString());
    }

    void InterfaceMember(int indent) {
        if (random.Chance(0.4)) {
            Line(indent, Type(0) + " P" + Next() + " { get; }");
            return;
        }

        Line(indent, Type(0) + " M" + Next() + "(" + ParameterList(random.Next(0, 4), allowModifiers: true) + ");");
    }

    // ── members ──────────────────────────────────────────────────────────────────────────────────

    void Member(int indent, int depth) {
        var choice = random.Pick(
            [
                "field", "property", "method", "expression-method", "constructor", "event", "indexer", "operator",
                "nested"
            ],
            [18, 20, 26, 10, 6, 4, 4, 4, depth < 1 ? 4 : 0]
        );

        switch (choice) {
            case "field":
                Field(indent);
                return;
            case "property":
                Property(indent);
                return;
            case "method":
                Method(indent, block: true);
                return;
            case "expression-method":
                Method(indent, block: false);
                return;
            case "constructor":
                Constructor(indent);
                return;
            case "event":
                AttributeLines(indent);
                Line(indent, "public event Action<" + Type(0) + ">? E" + Next() + ";");
                return;
            case "indexer":
                AttributeLines(indent);
                Line(indent, "public " + Type(0) + " this[int index] => " + Expression(1, indent) + ";");
                return;
            case "operator":
                Line(
                    indent,
                    "public static bool operator "
                    + random.Pick(["==", "!=", "<", ">"])
                    + "("
                    + Type(0)
                    + " left, "
                    + Type(0)
                    + " right) => "
                    + Expression(1, indent)
                    + ";"
                );
                return;
            default:
                TypeDeclaration(indent, depth + 1);
                return;
        }
    }

    void Field(int indent) {
        AttributeLines(indent);
        var modifiers = random.Pick(
            ["private", "private readonly", "public static readonly", "internal", "const", "public required"],
            [24, 24, 12, 10, 6, 6]
        );

        var type = modifiers == "const" ? random.Pick(["int", "string"]) : Type(0);
        var name = (modifiers.Contains("private", StringComparison.Ordinal) ? "f" : "F") + Next();
        var initialiser = modifiers switch {
            "const" => type == "int" ? random.Next(1000).ToString(CultureInfo.InvariantCulture) : "\"k\"",
            "public required" => null,
            _ => random.Chance(0.7) ? Expression(1, indent) : null
        };

        Line(
            indent,
            modifiers + " " + type + " " + name + (initialiser is null ? string.Empty : " = " + initialiser) + ";"
        );
    }

    void Property(int indent) {
        AttributeLines(indent);
        var type = Type(0);
        var name = "P" + Next();
        switch (random.Pick(["auto", "expression", "accessors", "initialised"], [30, 30, 22, 18])) {
            case "auto":
                Line(
                    indent,
                    "public " + type + " " + name + " { get; " + random.Pick(["set;", "init;", "private set;"]) + " }"
                );
                return;
            case "expression":
                Line(indent, "public " + type + " " + name + " => " + Expression(1, indent) + ";");
                return;
            case "initialised":
                Line(indent, "public " + type + " " + name + " { get; } = " + Expression(1, indent) + ";");
                return;
            default:
                Line(indent, "public " + type + " " + name + " {");
                Line(indent + 1, "get => " + Expression(1, indent + 1) + ";");
                Line(indent + 1, "set {");
                Statement(indent + 2, 1);
                Line(indent + 1, "}");
                Line(indent, "}");
                return;
        }
    }

    void Constructor(int indent) {
        // ⚠ The type's own name is not tracked, so a constructor is emitted as a static factory
        // instead. A constructor whose name does not match its type is a *parse* error, and this
        // grammar's contract is that it never emits one.
        Line(
            indent,
            "public static "
            + Type(0)
            + " Create"
            + Next()
            + "("
            + ParameterList(random.Next(0, 4), allowModifiers: true)
            + ") => "
            + Expression(1, indent)
            + ";"
        );
    }

    void Method(int indent, bool block) {
        AttributeLines(indent);
        var parameters = TypeParameters();
        var async = block && random.Chance(0.25);
        var modifiers = random.Pick(
            ["public", "private", "public static", "internal", "protected virtual", "public override"],
            [30, 20, 20, 10, 10, 10]
        );

        var returns = async ? random.Pick(["Task", "Task<" + Type(0) + ">", "ValueTask<" + Type(0) + ">"]) : Type(0);
        var head = modifiers
            + (async ? " async " : " ")
            + returns
            + " M"
            + Next()
            + parameters.List
            + "("
            + ParameterList(random.Next(0, 5), allowModifiers: true)
            + ")"
            + parameters.Constraints;

        if (!block) {
            Line(indent, head + " => " + Expression(1, indent) + ";");
            return;
        }

        Line(indent, head + " {");
        var statements = random.Next(1, 6);
        for (var i = 0; i < statements; i++) {
            Statement(indent + 1, 1, async);
        }

        Line(indent, "}");
    }

    /// <summary>
    /// A parameter list.
    /// </summary>
    /// <remarks>
    /// ⚠ Optional parameters are placed in the tail and <c>params</c> only last, because both rules
    /// are enforced by the *parser* — CS1737 and CS0231 — and a declaration that breaks either is a
    /// file the formatter is required to leave byte-identical.
    /// </remarks>
    string ParameterList(int count, bool allowModifiers) {
        if (count == 0) {
            return string.Empty;
        }

        var optionalFrom = random.Chance(0.3) ? random.Next(count) : count;
        var trailingParams = allowModifiers && optionalFrom == count && random.Chance(0.12);
        var parts = new List<string>();
        for (var i = 0; i < count; i++) {
            var part = new StringBuilder();
            if (random.Chance(0.15)) {
                part.Append('[').Append(random.Pick(Attributes)).Append("] ");
            }

            if (trailingParams && i == count - 1) {
                part.Append("params ").Append(Type(0)).Append("[] p").Append(Next());
                parts.Add(part.ToString());
                continue;
            }

            if (i >= optionalFrom) {
                part.Append(Type(0)).Append(" p").Append(Next()).Append(" = default");
                parts.Add(part.ToString());
                continue;
            }

            var modifier = allowModifiers && random.Chance(0.2)
                ? random.Pick(["ref ", "out ", "in ", "scoped ref "])
                : string.Empty;

            part.Append(modifier).Append(Type(0)).Append(" p").Append(Next());
            parts.Add(part.ToString());
        }

        return string.Join(", ", parts);
    }

    void AttributeLines(int indent) {
        if (!random.Chance(0.3)) {
            return;
        }

        if (random.Chance(0.4)) {
            Line(indent, "[" + random.Pick(Attributes) + ", " + random.Pick(Attributes) + "]");
            return;
        }

        var count = random.Next(1, 3);
        for (var i = 0; i < count; i++) {
            Line(indent, "[" + random.Pick(Attributes) + "]");
        }
    }

    // ── statements ───────────────────────────────────────────────────────────────────────────────

    void Statement(int indent, int depth, bool async = false) {
        var choice = depth >= 3
            ? random.Pick(["local", "expression", "return"], [40, 40, 20])
            : random.Pick(
                [
                    "local", "expression", "if", "foreach", "for", "while", "switch", "try", "using",
                    "return", "block", "local-function", "lock", "throw", "deconstruct"
                ],
                [24, 18, 14, 10, 6, 4, 8, 5, 5, 6, 3, 4, 3, 2, 6]
            );

        switch (choice) {
            case "local": {
                var type = random.Chance(0.5) ? "var" : Type(0);
                Line(indent, type + " v" + Next() + " = " + Expression(1, indent) + ";");
                return;
            }

            case "deconstruct":
                Line(indent, "var (a" + Next() + ", b" + Next() + ") = " + Expression(1, indent) + ";");
                return;

            case "expression":
                // ⚠ Not any expression. C# only allows an invocation, an assignment, an increment,
                // an `await` or an object creation to stand as a statement, and `static x => …;` as
                // a statement is CS0106 — reported before the formatter ever sees the tree.
                Line(
                    indent,
                    random.Pick(
                        [
                            random.Pick(["Compute", "Emit", "Handle"]) + "(" + Arguments(1, indent) + ")",
                            random.Pick(["value", "state", "_cache"]) + " = " + Expression(2, indent),
                            random.Pick(["value", "count"]) + "++",
                            random.Pick(["total", "state"]) + " += " + Operand(2, indent),
                            "new " + Type(1) + "(" + Arguments(2, indent) + ")"
                        ],
                        [30, 30, 8, 16, 10]
                    )
                    + ";"
                );

                return;

            case "if": {
                Line(indent, "if (" + Expression(1, indent) + ") {");
                Statement(indent + 1, depth + 1, async);
                if (random.Chance(0.45)) {
                    Line(indent, "} else if (" + Expression(1, indent) + ") {");
                    Statement(indent + 1, depth + 1, async);
                }

                if (random.Chance(0.4)) {
                    Line(indent, "} else {");
                    Statement(indent + 1, depth + 1, async);
                }

                Line(indent, "}");
                return;
            }

            case "foreach":
                Line(
                    indent,
                    (random.Chance(0.2) ? "await foreach (" : "foreach (")
                    + (random.Chance(0.25) ? "var (k" + Next() + ", w" + Next() + ")" : "var e" + Next())
                    + " in "
                    + Expression(1, indent)
                    + ") {"
                );

                Statement(indent + 1, depth + 1, async);
                Line(indent, "}");
                return;

            case "for": {
                // ⚠ The loop variable is captured before the bound is generated. Reading `counter`
                // inline would name the variable after whatever the *bound's* last `Next()` handed
                // out, and `for (var i3 = 0; i9 < …; i9++)` is a different program.
                var loop = "i" + Next();
                Line(indent, "for (var " + loop + " = 0; " + loop + " < " + Operand(2, indent) + "; " + loop + "++) {");
                Statement(indent + 1, depth + 1, async);
                Line(indent, "}");
                return;
            }

            case "while":
                Line(indent, "while (" + Expression(1, indent) + ") {");
                Statement(indent + 1, depth + 1, async);
                Line(indent, "}");
                return;

            case "switch": {
                Line(indent, "switch (" + Expression(1, indent) + ") {");
                var sections = random.Next(1, 4);
                for (var i = 0; i < sections; i++) {
                    Line(
                        indent + 1,
                        "case "
                        + Pattern(1)
                        + (random.Chance(0.3) ? " when " + Expression(2, indent) : string.Empty)
                        + ":"
                    );
                    Statement(indent + 2, depth + 1, async);
                    Line(indent + 2, "break;");
                }

                Line(indent + 1, "default:");
                Line(indent + 2, "break;");
                Line(indent, "}");
                return;
            }

            case "try":
                Line(indent, "try {");
                Statement(indent + 1, depth + 1, async);
                Line(
                    indent,
                    "} catch ("
                    + random.Pick(["InvalidOperationException", "IOException", "Exception"])
                    + " exception"
                    + Next()
                    + ")"
                    + (random.Chance(0.35) ? " when (" + Expression(2, indent) + ") {" : " {")
                );

                Statement(indent + 1, depth + 1, async);
                if (random.Chance(0.4)) {
                    Line(indent, "} finally {");
                    Statement(indent + 1, depth + 1, async);
                }

                Line(indent, "}");
                return;

            case "using":
                if (random.Chance(0.5)) {
                    Line(indent, "using var u" + Next() + " = " + Expression(1, indent) + ";");
                    return;
                }

                Line(indent, "using (var u" + Next() + " = " + Expression(1, indent) + ") {");
                Statement(indent + 1, depth + 1, async);
                Line(indent, "}");
                return;

            case "lock":
                Line(indent, "lock (" + Expression(2, indent) + ") {");
                Statement(indent + 1, depth + 1, async);
                Line(indent, "}");
                return;

            case "throw":
                Line(indent, "throw new InvalidOperationException(" + Argument(2, indent) + ");");
                return;

            case "block":
                Line(indent, "{");
                Statement(indent + 1, depth + 1, async);
                Line(indent, "}");
                return;

            case "local-function":
                Line(
                    indent,
                    (random.Chance(0.3) ? "static " : string.Empty)
                    + Type(0)
                    + " Local"
                    + Next()
                    + "("
                    + ParameterList(random.Next(0, 3), allowModifiers: false)
                    + ") => "
                    + Expression(1, indent)
                    + ";"
                );

                return;

            default:
                Line(
                    indent,
                    "return " + (async ? "await " + Operand(1, indent) : Expression(1, indent)) + ";"
                );
                return;
        }
    }

    // ── expressions ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The productions that may not stand as an operand without parentheses around them.
    /// </summary>
    /// <remarks>
    /// ⚠ This list is the difference between a grammar that emits C# and one that emits something
    /// that looks like C#. Every one of these productions is *greedy* — a lambda body, a query's
    /// <c>select</c> clause, a switch expression's arm list and a conditional's <c>:</c> all extend
    /// until the parser cannot continue — so `x => y + 1` parses as `x => (y + 1)` and
    /// `[from a in b select c, d]` parses as one query whose `select` clause swallowed the comma.
    /// Measured on 300 units before this list existed: <b>147 of them had a parse error</b>, and a
    /// file that does not parse is one ADR-003 leaves byte-identical, so every property held over it
    /// for free. Half the generative half of the fuzzer was asserting nothing.
    /// </remarks>
    static readonly ImmutableArray<string> NeedsParentheses = [
        "lambda", "block-lambda", "ternary", "binary", "is-pattern", "switch-expression", "cast",
        "with", "query"
    ];

    /// <summary>One expression, in a position where a greedy production is safe.</summary>
    string Expression(int depth, int indent) => Expression(depth, indent, out _);

    /// <summary>
    /// One expression, in a position where it is an <b>operand</b> of something else.
    /// </summary>
    /// <remarks>
    /// ⚠ Parenthesised unless the production drawn cannot run away with what follows it. Real code
    /// is full of these parentheses for exactly the same reason, so the output is not made
    /// artificial by it.
    /// </remarks>
    string Operand(int depth, int indent) {
        var text = Expression(depth, indent, out var free);
        return free ? text : "(" + text + ")";
    }

    /// <summary>
    /// One expression. <paramref name="depth"/> counts down the interesting shapes.
    /// </summary>
    /// <remarks>
    /// ⚠ The weights are doc 12's list — generics, lambdas, patterns, initializers, attributes, raw
    /// strings — and they are the reason the grammar exists rather than an off-the-shelf one. The
    /// depth cut-off falls through to a literal, so a generated unit terminates.
    /// </remarks>
    /// <param name="free">
    /// Whether the result may stand as an operand of another expression without parentheses.
    /// </param>
    string Expression(int depth, int indent, out bool free) {
        free = true;
        if (depth >= 4) {
            return Literal();
        }

        var choice = random.Pick(
            [
                "literal", "identifier", "invocation", "generic-invocation", "member-chain",
                "conditional-access", "lambda", "block-lambda", "object-creation", "collection",
                "array", "ternary", "binary", "is-pattern", "switch-expression", "tuple", "cast",
                "nameof", "with", "range", "query", "interpolated", "raw-string", "checked",
                "anonymous", "default-literal"
            ],
            [
                14, 12, 16, 10, 10,
                6, 12, 5, 12, 10,
                8, 6, 12, 10, 9, 6, 4,
                4, 3, 4, 4, 8, 7, 2,
                4, 3
            ]
        );

        free = !NeedsParentheses.Contains(choice, StringComparer.Ordinal);
        switch (choice) {
            case "literal":
                return Literal();
            case "identifier":
                return random.Pick(["value", "state", "items", "this.Current", "Source", "_cache", "context"]);
            case "invocation":
                return random.Pick(["Compute", "Resolve", "Handle", "Emit", "TryGet", "Select"])
                    + "("
                    + Arguments(depth, indent)
                    + ")";
            case "generic-invocation":
                // ⚠ `M<A, B>(x)` is the shape the `>`-before-`(` defect lived in for four
                // milestones. It gets its own production for that reason.
                return random.Pick(["Convert", "Materialise", "Cast", "Bind"])
                    + "<"
                    + Type(1)
                    + (random.Chance(0.6) ? ", " + Type(1) : string.Empty)
                    + ">"
                    + "("
                    + Arguments(depth, indent)
                    + ")";
            case "member-chain": {
                var parts = new List<string> { random.Pick(["source", "builder", "context", "this"]) };
                var links = random.Next(1, 5);
                for (var i = 0; i < links; i++) {
                    parts.Add(
                        random.Pick(["Where", "Select", "OrderBy", "First", "Items", "Value", "Length"])
                        + (random.Chance(0.5) ? "(" + Argument(depth + 2, indent) + ")" : string.Empty)
                    );
                }

                return string.Join(".", parts);
            }

            case "conditional-access":
                return "source?." + random.Pick(["Value", "Items"]) + "?." + random.Pick(["Length", "Count"]);
            case "lambda":
                return random.Pick(
                    [
                        "x => " + Operand(depth + 1, indent),
                        "(x, y) => " + Operand(depth + 1, indent),
                        "static x => " + Operand(depth + 1, indent),
                        "(" + Type(1) + " x) => " + Operand(depth + 1, indent),
                        "async x => await " + Operand(depth + 1, indent),
                        "() => " + Operand(depth + 1, indent)
                    ]
                );

            case "block-lambda": {
                var inner = new StringBuilder("(x, y) => {\n");
                var pad = new string(' ', (indent + 1) * 4);
                inner.Append(pad).Append("return ").Append(Expression(depth + 2, indent + 1)).Append(";\n");
                inner.Append(new string(' ', indent * 4)).Append('}');
                return inner.ToString();
            }

            case "object-creation": {
                var type = Type(1);
                if (random.Chance(0.4)) {
                    return "new " + type + "(" + Arguments(depth, indent) + ")";
                }

                var initialisers = new List<string>();
                var count = random.Next(1, 5);
                for (var i = 0; i < count; i++) {
                    initialisers.Add("P" + Next() + " = " + Operand(depth + 2, indent));
                }

                return "new "
                    + (random.Chance(0.25) ? string.Empty : type)
                    + "("
                    + (random.Chance(0.5) ? Arguments(depth + 2, indent) : string.Empty)
                    + ") { "
                    + string.Join(", ", initialisers)
                    + " }";
            }

            case "collection": {
                var items = new List<string>();
                var count = random.Next(0, 6);
                for (var i = 0; i < count; i++) {
                    items.Add(Operand(depth + 2, indent));
                }

                if (random.Chance(0.35)) {
                    items.Add(".. " + random.Pick(["rest", "others", "Source"]));
                }

                return "[" + string.Join(", ", items) + "]";
            }

            case "array": {
                var items = new List<string>();
                var count = random.Next(1, 7);
                for (var i = 0; i < count; i++) {
                    items.Add(Operand(depth + 2, indent));
                }

                return random.Chance(0.5)
                    ? "new[] { " + string.Join(", ", items) + " }"
                    : "new " + Type(2) + "[] { " + string.Join(", ", items) + " }";
            }

            case "ternary":
                return Operand(depth + 1, indent)
                    + " ? "
                    + Operand(depth + 1, indent)
                    + " : "
                    + Operand(depth + 1, indent);
            case "binary": {
                var operands = random.Next(2, 6);
                var parts = new List<string>();
                for (var i = 0; i < operands; i++) {
                    parts.Add(Operand(depth + 1, indent));
                }

                var operators = new List<string>();
                for (var i = 1; i < operands; i++) {
                    operators.Add(random.Pick(["+", "&&", "||", "??", "*", "-", "|", "&"]));
                }

                var joined = new StringBuilder(parts[0]);
                for (var i = 1; i < operands; i++) {
                    joined.Append(' ').Append(operators[i - 1]).Append(' ').Append(parts[i]);
                }

                return joined.ToString();
            }

            case "is-pattern":
                return random.Pick(["value", "state", "source"]) + " is " + Pattern(depth);
            case "switch-expression": {
                var arms = new List<string>();
                var count = random.Next(1, 4);
                for (var i = 0; i < count; i++) {
                    arms.Add(
                        Pattern(depth + 1)
                        + (random.Chance(0.3) ? " when " + Operand(depth + 2, indent) : string.Empty)
                        + " => "
                        + Operand(depth + 2, indent)
                    );
                }

                arms.Add("_ => " + Operand(depth + 2, indent));
                return random.Pick(["value", "state"]) + " switch { " + string.Join(", ", arms) + " }";
            }

            case "tuple":
                return "("
                    + Operand(depth + 1, indent)
                    + ", "
                    + Operand(depth + 1, indent)
                    + (random.Chance(0.3) ? ", " + Operand(depth + 1, indent) : string.Empty)
                    + ")";
            case "cast":
                return "(" + Type(1) + ")" + Operand(depth + 1, indent);
            case "nameof":
                return random.Pick(
                    ["nameof(value)", "typeof(" + Type(1) + ")", "sizeof(int)", "nameof(Source.Length)"]
                );
            case "with":
                return random.Pick(["value", "state"])
                    + " with { P"
                    + Next()
                    + " = "
                    + Operand(depth + 2, indent)
                    + " }";
            case "range":
                return random.Pick(["items", "buffer"])
                    + "["
                    + random.Pick(["1..", "..^1", "1..^2", "^3..", ".."])
                    + "]";
            case "query":
                return "from item in "
                    + random.Pick(["items", "Source"])
                    + " where "
                    + Operand(depth + 2, indent)
                    + (random.Chance(0.4) ? " orderby item.Length descending" : string.Empty)
                    + " select "
                    + Operand(depth + 2, indent);
            case "interpolated":
                // ⚠ `Hole()` rather than `Expression()`. C# 11 allows a newline inside an
                // interpolation hole, so a recursive call could drop a multi-line raw string into
                // one — which parses, but is also the one construct the formatter copies verbatim
                // (NodeLayout.Verbatim), and a case whose interesting half is exempt from every
                // layout decision is a case that measures nothing.
                return "$\""
                    + random.Pick(["value ", "n=", "at "])
                    + "{"
                    + Hole()
                    + (random.Chance(0.3) ? ":F2" : string.Empty)
                    + "}"
                    + (random.Chance(0.4) ? " and {" + Hole() + "}" : string.Empty)
                    + "\"";
            case "raw-string":
                return RawString(indent);
            case "checked":
                return "checked(" + Expression(depth + 1, indent) + ")";
            case "anonymous":
                return "new { "
                    + random.Pick(["Name", "Id", "Kind"])
                    + " = "
                    + Operand(depth + 2, indent)
                    + ", Count = "
                    + Operand(depth + 2, indent)
                    + " }";
            default:
                return random.Pick(["default", "default(" + Type(1) + ")", "null!", "this"]);
        }
    }

    string Arguments(int depth, int indent) {
        var count = random.Next(0, 5);
        var parts = new List<string>();

        // ⚠ Named arguments only in the tail. C# 7.2 does allow a non-trailing named argument, but
        // only where it names the parameter in its own position, which this grammar has no way to
        // know — and the error for getting it wrong is CS8323, which the *parser* reports.
        var namedFrom = random.Chance(0.25) ? random.Next(count + 1) : count;

        for (var i = 0; i < count; i++) {
            // ⚠ `out var o` is a whole argument, not a prefix to one. M4 found `PredefinedTypeRule`
            // rewriting `out var value` into `out string value` on 2 210 of 4 606 Vixen files, so
            // this production earns its place rather than being decoration.
            if (random.Chance(0.08)) {
                parts.Add("out var o" + Next());
                continue;
            }

            parts.Add((i >= namedFrom ? "name" + Next() + ": " : string.Empty) + Argument(depth, indent));
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// One argument.
    /// </summary>
    /// <remarks>
    /// ⚠ A bare lambda is allowed here and nowhere else that a comma can follow, and it is allowed
    /// because a lambda passed to a method is the single most common shape in modern C# and the one
    /// the formatter has the most rules about. It is safe here only because a lambda's *body* is an
    /// <see cref="Operand"/>: `M(x => (from a in b select c), d)` terminates at the comma, and
    /// `M(x => from a in b select c, d)` does not.
    /// </remarks>
    string Argument(int depth, int indent) {
        var prefix = random.Chance(0.08) ? random.Pick(["ref ", "in "]) : string.Empty;
        if (prefix.Length == 0 && random.Chance(0.22)) {
            return "x" + Next() + " => " + Operand(depth + 1, indent);
        }

        return prefix + Operand(depth + 1, indent);
    }

    /// <summary>
    /// ⚠ Every pattern form the language has, because patterns are one of doc 12's six.
    /// </summary>
    string Pattern(int depth) {
        if (depth >= 3) {
            return random.Pick(["null", "not null", "0", "\"k\"", "_", "var p"]);
        }

        // ⚠ Never a nullable type. `case string? typed:` is not a declaration pattern of a nullable
        // reference type — the parser reads the `?` as the start of a conditional expression and the
        // whole switch section stops parsing. `T?` in a pattern is legal only where the parse is
        // otherwise unambiguous, which this grammar cannot establish.
        var patternType = Type(2).TrimEnd('?');
        return random.Pick(
            [
                "null",
                "not null",
                patternType + " typed" + Next(),
                patternType,
                random.Next(100).ToString(CultureInfo.InvariantCulture),
                "> 0 and < 100",
                "not (0 or 1 or 2)",
                "{ Length: > 0 }",
                "{ Value.Length: > 2, Kind: " + Pattern(depth + 1) + " }",
                "[]",
                "[_, .. var rest" + Next() + "]",
                "[" + Pattern(depth + 1) + ", " + Pattern(depth + 1) + "]",
                "(" + Pattern(depth + 1) + ", " + Pattern(depth + 1) + ")",
                patternType + " { P" + Next() + ": " + Pattern(depth + 1) + " }",
                "var captured" + Next()
            ],
            [10, 8, 10, 6, 10, 8, 6, 10, 6, 5, 7, 6, 5, 8, 6]
        );
    }

    string Literal() =>
        random.Pick(
            [
                random.Next(100000).ToString(CultureInfo.InvariantCulture),
                "\"" + new string('s', random.Next(1, 30)) + "\"",
                random.Chance(0.5) ? "true" : "false",
                "null",
                "'c'",
                "1.5d",
                "0x" + random.Next(4096).ToString("x", CultureInfo.InvariantCulture),
                "3_000_000L",
                "@\"verbatim\\path\"",
                "\"utf8\"u8",
                "1.0m"
            ],
            [22, 22, 8, 8, 5, 5, 5, 4, 6, 4, 4]
        );

    /// <summary>
    /// A raw string literal, one of doc 12's six.
    /// </summary>
    /// <remarks>
    /// ⚠ The multi-line form is emitted with its content and its closing delimiter at exactly the
    /// same indentation. A closing delimiter indented *further* than the content is CS8999 — a
    /// **parse** error — and this grammar's contract is that it never emits one. The mutation layer
    /// cannot break the invariant afterwards: every line a multi-line token spans except its first
    /// is protected from re-indentation, and the closing delimiter's line is one of them.
    /// </remarks>
    string RawString(int indent) {
        if (random.Chance(0.5)) {
            var quotes = new string('"', random.Chance(0.3) ? 4 : 3);
            return quotes + random.Pick(["a { b } c", "no escapes \\n here", "<x a=\"1\"/>", "{}"]) + quotes;
        }

        var pad = new string(' ', (indent + 1) * 4);
        var lines = random.Next(1, 4);
        var content = new List<string>();
        for (var i = 0; i < lines; i++) {
            content.Add(random.Pick(["select 1", "{ \"json\": true }", "line of text"]));
        }

        // ⚠ The `$` prefix is taken only when no line carries a brace. In an interpolated raw string
        // a single `{` opens a hole, so `$"""{ "json": true }"""` is a *parse* error rather than a
        // string — the exact class of mistake that silently turns a fuzz case into a file the
        // formatter is required by ADR-003 to leave alone.
        var interpolated = random.Chance(0.25) && content.TrueForAll(static line => !line.Contains('{'));
        var body = new StringBuilder(interpolated ? "$\"\"\"\n" : "\"\"\"\n");
        foreach (var line in content) {
            body.Append(pad).Append(line).Append('\n');
        }

        body.Append(pad).Append("\"\"\"");
        return body.ToString();
    }

    /// <summary>A short expression with no line break in it, for an interpolation hole.</summary>
    string Hole() =>
        random.Pick(
            [
                "value",
                "state.Length",
                "items.Count",
                random.Next(1000).ToString(CultureInfo.InvariantCulture),
                "Compute(value)",
                "source?.Value",
                // ⚠ Parenthesised. In an interpolation hole the `:` of a conditional expression ends
                // the hole and starts a format specifier — CS8361, from the parser.
                "(value is null ? 0 : 1)",
                "items[0]"
            ]
        );

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────────

    string Type(int depth) {
        if (depth >= 3 || !random.Chance(0.45)) {
            return random.Pick(SimpleTypes) + (random.Chance(0.12) ? "?" : string.Empty);
        }

        if (random.Chance(0.15)) {
            return "(" + Type(depth + 1) + " First, " + Type(depth + 1) + " Second)";
        }

        return string.Format(CultureInfo.InvariantCulture, random.Pick(GenericTypes), Type(depth + 1));
    }

    int Next() => ++counter;

    void Line(int indent, string text) {
        builder.Append(new string(' ', indent * 4)).Append(text).Append('\n');
    }

    void Blank() => builder.Append('\n');
}
