using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

public sealed class FuzzRegressionTests {
    [Theory]
    [InlineData("nested-collection-in-generated-switch", false)]
    [InlineData("nested-collection-in-generated-switch", true)]
    [InlineData("nested-collection-in-generated-while", false)]
    [InlineData("nested-collection-in-generated-while", true)]
    [InlineData("nested-switch-in-generated-tuple-conditional", false)]
    [InlineData("nested-switch-in-generated-tuple-conditional", true)]
    [InlineData("nested-switch-in-generated-tuple-condition", false)]
    [InlineData("nested-switch-in-generated-tuple-condition", true)]
    public void NestedMultilineItems_AreIdempotentUnderTheFuzzerConfiguration(string fixture, bool defined) {
        // Minimized nightly findings need the repository configuration, not bare CLI defaults.
        var path = Path.Combine(Corpus.Root, "pathological", $"{fixture}.cs");
        var source = SourceText.From(File.ReadAllText(path));
        var options = new PhaseOneOptions(Fuzzer.OptionsFor(path));
        IReadOnlyList<string> symbols = defined ? Corpus.PropertySymbols : [];
        var first = CSharpFormatter.Format(path, source, options, preprocessorSymbols: symbols);
        var second = CSharpFormatter.Format(
            path,
            SourceText.From(first.Formatted),
            options,
            preprocessorSymbols: symbols
        );
        Assert.True(first.Changed);
        Assert.Empty(second.Edits);
        Assert.Null(
            TokenEquivalence.Compare(source, SourceText.From(first.Formatted), CSharpFormatter.ParseOptionsFor(symbols))
        );
    }

    [Theory]
    [InlineData(5423343295399047858UL)]
    [InlineData(11149039553341969427UL)]
    [InlineData(13458345604094946523UL)]
    [InlineData(6636340479617988337UL)]

    // ⚠ The Nightly's September findings, kept here because the runs they came from are deleted
    // once fixed: this theory is the only record that replays. The first six are one class —
    // a property pattern heading a switch arm or a `case` label, chopped on pass one and joined
    // on pass two (#378) — and the last is the flat direction of a collection-valued `=` (#379).
    [InlineData(2801382500469017888UL)]
    [InlineData(13095184792041486380UL)]
    [InlineData(11255509907099259375UL)]
    [InlineData(857717698562573229UL)]
    [InlineData(14871250529025744122UL)]
    [InlineData(2742638269065363150UL)]
    [InlineData(7611825995831206751UL)]
    [InlineData(3296757264995743770UL)]

    // ⚠ Fourteen consecutive Nightly runs, 2026-09-23 to 2026-10-06, all on one commit and all one
    // defect: a switch arm whose body is a multi-line raw string. The pattern before the arrow read
    // the body's unbounded flat width as its own line and chopped; pass two re-joined it.
    [InlineData(2120897534779346985UL)]
    [InlineData(9749290611115768490UL)]
    [InlineData(15958279914763084359UL)]
    [InlineData(15444912073777749680UL)]
    [InlineData(18393674522974205944UL)]
    [InlineData(12485847646168391438UL)]
    [InlineData(7912736926820264633UL)]
    [InlineData(16468989038966499649UL)]
    [InlineData(7775043994036919290UL)]
    [InlineData(16738553386079377947UL)]
    [InlineData(5651812525606868025UL)]
    [InlineData(11606463289314822479UL)]
    [InlineData(15931495183721029956UL)]
    [InlineData(15010799596576293816UL)]

    // #409 (found by `fuzz --seed=404002`): `nameof(value), /* f */ name176: …` past the margin. The
    // block comment hid the list's point after the comma, so pass one chopped `nameof(` instead and
    // pass two re-joined it.
    [InlineData(7005158519080377895UL)]
    // #419 (found by `fuzz --seed=4190420`): a subpattern's `{ P62` / `: null }` joined as `P62 : null`
    // indented and `P62: null` not, because the preserved gap read the next line's indent as a space.
    [InlineData(7764980540680690061UL)]
    // Nightly `fuzz --seed=37583856628`: a typed local's `= Select(` with the `(` past the margin. Pass one
    // kept the `=` by EqualsFloor's extrapolated table and chopped the arguments; pass two read the chop as
    // the author's, lost the floor and broke the `=`. The `(` past the margin now breaks the `=` on both.
    [InlineData(4304693669410283359UL)]
    // Nightly `fuzz --seed=909`: a typed local's `= context.First` whose receiver ends past the margin. The
    // `=` yielded to the dot fill (#482), which kept `= context` past the margin and broke at the dot; pass
    // two read that break as the author's and broke the `=`. GroupFacts.MemberHeadWidth.
    [InlineData(9552816164132777654UL)]
    // `fuzz --seed=31337`: an arm's `("k", var p) =>` before a body with no break point of its own. Pass one
    // took #559's type/name fill point, reading the body as part of the pattern's line, and broke the arrow
    // as well; pass two found the arrow's break kept, measured the pattern up to it and re-joined `var p`.
    // A kept break at that point is now pinned, as the oracle keeps it and as the list's commas are.
    [InlineData(9642682992700587520UL)]
    [InlineData(7862808234978504853UL)]
    [InlineData(8573762464065711162UL)]
    [InlineData(7447388608888272285UL)]
    [InlineData(3776683644240416092UL)]
    [InlineData(11325995557757886152UL)]
    // `fuzz --seed=7777`: `get => state is (` / `{ Length: > 0 }, ImmutableArray<object?> typed56);`. The
    // property pattern's `{` joined the positional `(` as an opening brace does, after pass one had already
    // broken the accessor's arrow for the body the kept break made multi-line; pass two re-joined the arrow.
    // The gap is the parenthesis's: the oracle keeps the break, the element one level in.
    [InlineData(11693758747470537505UL)]
    // `byte x when new Func<` / `(…), (…)>("s", 'c', 0xb92) => Handle(…)`: pass one filled the type arguments
    // and broke before the arrow for width; pass two read that arrow break as kept, lifted the type arguments
    // a level and chopped the call. A `when` clause holding a type argument list no longer lifts.
    [InlineData(12955079666331923518UL)]
    // `[_, .. var rest43] when $"…" => $"…{…}",`: a body with no break point is read through by the head
    // before the arrow, so pass one chopped the list pattern and broke the arrow too; pass two, finding the
    // arrow's break kept, lifted the chopped brackets. The oracle reads a body through only up to thirteen
    // columns and breaks after the arrow for a wider one.
    [InlineData(14071685607328961301UL)]
    // `object` / `C(…) =>` / an arm `… when M(…)` / `=> body` whose arrow broke for width: pass two read the
    // arrow break as kept, opened the arm's group at its pattern before the arm's frame had started, and the
    // level was asked of the member's frame, already spent by its broken header — the arrow fell to the arm's
    // column. A group opened at its node's first token now starts the node's frame first.
    [InlineData(11550439650966795547UL)]
    // `object { P102: null } when (0x116` / `?? … ) =>` / `(0x237, true),` inside a `while` header: pass one broke
    // the `??` chain and the arrow for width, pass two read the arrow break as kept and lifted the `??` line. A
    // `when` condition holding an operator chain or a conditional no longer lifts under a break after the arrow.
    [InlineData(5953779247182235391UL)]
    // `{` / `Kind` / `: not null` / `} when (from … select …)` / `=> body,`: pass one broke the arrow for width
    // with the braces unlifted, pass two read it as kept and lifted them, which chopped the query. The arrow's
    // width break now lifts the braces too — decided with the arm written ahead unlifted, and kept once the
    // lift moved what was beside it.
    [InlineData(14973596429632421881UL)]
    // `not null when ("ss"` / `?? "…" + "…" - "…")` / `=> …,` in a `using` header: pass one broke the arrow for
    // width, pass two read it as kept, opened the arm's level at the pattern and lifted and chopped the chain.
    // Every arm a kept arrow break lifts from its pattern now lifts under the width's break too.
    [InlineData(18207060042734210187UL)]
    // Nightly, 6 hits in 81k cases: `}, ["s"` / `#region fuzz` / `, false, …]` in an array initializer. The
    // draft measure read the breaks beside the directive as spaces, so pass one moved the collection down
    // whole and pass two, measuring its own broken output, kept `}, [`. A break beside a directive is no
    // kept break now (#471).
    [InlineData(12061030311543376894UL)]
    // `fuzz --seed=99991`: `(Span<…> First, Dictionary<…> Second) v204 = (x, y) => Source;` with its `=` past
    // the margin. Pass one kept the `=` and chopped the parameters past the margin; pass two read the chop as
    // the author's and filled the type. The type/name gap now breaks when the line through the `=` overflows,
    // and an `=` whose value starts past the margin breaks, as the oracle does.
    [InlineData(13830403873739157460UL)]
    public void ReportedGeneratedSeeds_HaveNoViolations(ulong seed) {
        var test = Fuzzer.Build(seed, FuzzMode.Both, Corpus.All());
        var (violations, _) = Fuzzer.Execute(
            test,
            false,
            cancellation: TestContext.Current.CancellationToken
        );
        Assert.Empty(violations);
    }

    // ⚠ A mutate finding needs its origin as well as its seed: the seed draws the file by index into
    // the corpus, so every corpus file added since re-targets it — 11718305405350914591 alone now
    // mutates a blank-lines construct and passes whatever the formatter does. The pair is the exact
    // reconstruction `fuzz --replay=<seed> --origin=<path>` prints.
    // #404 (found by `fuzz --seed=393150`): an arrow body `(`↵`a)[0] .C()` held SK-DIV-0101's level
    // on pass one, broke the chain, and gave the level up on pass two.
    // ⚠ 2026-10-09, split-line inside a lambda in a member chain: a sole lambda's property-fill body the author
    // broke before a dot kept its arrow and filled before an earlier dot, and pass two moved the kept break's
    // level (3559808079077978877); a conditional broken after `n.` read as a broken condition on pass one
    // and as a flat one on pass two, once the formatter had joined it (3423309597191150844). The two
    // whitespace-absorption seeds of the same night, fixed by measuring with BreakPlan.FormattedWidth, are
    // kept with them.
    [Theory]
    [InlineData(11718305405350914591UL, "constructs/breaks/chain-after-parenthesised-head.cs")]
    // Nightly `fuzz --seed=909`: `DeserializeObject<Review /* f */ >(x)` behind an `=`. FlatSourceWidth
    // skipped the comment, so #528's held-call table kept an `=` the comment had pushed past the margin;
    // pass two, with the arguments chopped, broke it.
    [InlineData(6285859913225113725UL, "real/newtonsoft/Newtonsoft.Json.Tests/Issues/Issue1566.cs")]
    // `fuzz --seed=3`: the same call with a `// fuzz` after its `;` — the comment, not the call, pushed the line
    // past the margin, and #528's held-call width did not count it.
    [InlineData(7754551050098241345UL, "real/newtonsoft/Newtonsoft.Json.Tests/Issues/Issue1566.cs")]
    // Group L's fuzz: `JsonConvert /** d */ .DeserializeObject<T>(x)` — a documentation-style comment, which the
    // gap width did not count, and whose `/**` lies outside its trivia's span when it does.
    [InlineData(1267273925188459665UL, "real/newtonsoft/Newtonsoft.Json.Tests/Issues/Issue1566.cs")]
    // `fuzz --seed=20261009`: `… = Call<T>(json) /* f */ ;` — a comment between the `)` and the `;`.
    [InlineData(
        10944625209729174497UL,
        "real/newtonsoft/Newtonsoft.Json.Tests/Converters/KeyValuePairConverterTests.cs"
    )]
    // A collection element broken after its `[`, measured flat by the array fill's draft.
    [InlineData(15104748770501078810UL, "pathological/nested-collection-in-generated-while.cs")]
    [InlineData(17998121662372599673UL, "real/newtonsoft/Newtonsoft.Json.Tests/Issues/Issue1566.cs")]
    [InlineData(5160029152501638677UL, "real/newtonsoft/Newtonsoft.Json.Tests/Issues/Issue1566.cs")]
    // `fuzz --seed=4242`: `a + (b * c) + (d\n== 0 ? …)` — a nested chain broken in the last operand chopped only
    // the link holding it, and pass two chopped the first link as well.
    [InlineData(5604488888367663423UL, "real/vixen/Core/Vixen.Navigation/Agents/LocalAvoidance.cs")]
    // `fuzz --seed=7777`: `bool c = o… is A // c` / `or B;` — a line comment in the pattern turned the `=` away
    // from #446's table, and the `or`s after the `is` break took a level the second pass gave back.
    [InlineData(16865623964709448456UL, "constructs/breaks/equals-before-a-binary-pattern.cs")]
    // `fuzz --seed=4242`: `T v = Callee( /** d */ a, b);` — a documentation comment after the `(` passed the
    // floor's comment test, which kept the `=`; pass two found the arguments chopped and broke it.
    [InlineData(10014018092937601535UL, "constructs/breaks/equals-before-a-call-floor.cs")]
    [InlineData(3559808079077978877UL, "constructs/wrapping/lambda-arrow-over-a-property-fill.cs")]
    // ⚠ And a kept break after a fill break in the same body (`--seed=31337`): the chain's frame paid a level
    // the from-line scope had already paid (6109074167501724172).
    [InlineData(6109074167501724172UL, "constructs/wrapping/lambda-arrow-over-a-property-fill.cs")]
    [InlineData(3423309597191150844UL, "constructs/breaks/conditional-after-eq.cs")]
    [InlineData(
        11806697963186320743UL,
        "real/newtonsoft/Newtonsoft.Json.Tests/Converters/KeyValuePairConverterTests.cs"
    )]
    [InlineData(1332581229878653148UL, "constructs/breaks/held-single-call.cs")]
    public void ReportedMutateSeeds_HaveNoViolations(ulong seed, string origin) {
        var test = Fuzzer.Build(seed, FuzzMode.Both, Corpus.All(), origin);
        var (violations, _) = Fuzzer.Execute(
            test,
            false,
            cancellation: TestContext.Current.CancellationToken
        );
        Assert.Empty(violations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbsorbedMutations_ProtectUnterminatedInterpolatedStringThroughEof(bool finalNewline) {
        var path = Path.Combine(Corpus.Root, "pathological", "interpolated-raw-string-with-nested-braces.cs");
        var source = File.ReadAllText(path).TrimEnd('\r', '\n') + (finalNewline ? "\n" : string.Empty);
        var applied = 0;
        foreach (var name in FuzzMutations.AbsorbedNames) {
            for (ulong seed = 0; seed < 100; seed++) {
                var mutated = FuzzMutations.Apply(name, source, new(seed), Corpus.PropertySymbols);
                if (mutated is null) {
                    continue;
                }

                applied++;
                foreach (var symbols in (IReadOnlyList<string>[])[[], Corpus.PropertySymbols]) {
                    Assert.Null(
                        TokenEquivalence.Compare(
                            SourceText.From(source),
                            SourceText.From(mutated),
                            CSharpFormatter.ParseOptionsFor(symbols)
                        )
                    );
                }
            }
        }

        // Safe code before the string must still be exercised; skipping the entire file is not a fix.
        Assert.True(applied > 0);
        Assert.NotEqual(
            source,
            FuzzMutations.Apply(FuzzMutations.Indent, source, new(0), Corpus.PropertySymbols)
        );
    }
}
