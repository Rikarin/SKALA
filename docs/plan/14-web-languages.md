# 14 — Web Languages

HTML, the CSS family, and Vixen's `.vxml`/`.vcss` dialects. **Not in v1**, and this document exists
to make sure the core does not accidentally make them impossible.

## Why they are later, and what "later" is conditioned on

C# is 100 % of the value on day one: it is where the code is, where the AI writes, and where the
configuration is 380 options deep. HTML and CSS are worth doing when three things are true:

1. Line fidelity on C# is at the bar and stable ([12](12-conformance-and-testing.md)).
2. `ISkalaLanguage` (ADR-013) has been exercised by a second implementation cheaply — the XML/xmldoc
   sub-formatter, which already exists inside the C# path, is that exercise, and lifting it out is
   the test of whether the seam is real.
3. Vixen's `.vxml`/`.vcss` parsers are stable enough to build against, or the decision is taken to
   write tolerant parsers here.

## What the config already says

The Rider export carries 24 `resharper_html_*` keys — a complete HTML formatter configuration that
nobody wrote by hand and that is nevertheless the author's settings:

```ini
resharper_html_max_line_length = 120
resharper_html_wrap_lines = true
resharper_html_attribute_indent = align_by_first_attribute
resharper_html_linebreak_before_elements = body,div,p,form,h1,h2,h3
resharper_html_max_blank_lines_between_tags = 2
resharper_html_space_before_self_closing = false
resharper_html_insert_final_newline = false
```

⚠ Note `attribute_indent = align_by_first_attribute` — alignment, which the C# configuration switches
off everywhere. The `Align` IR node exists partly for this ([05](05-csharp-formatting-rules.md) §
"Alignment"), and the HTML front end is where it becomes hot rather than incidental.

There are **no** `resharper_css_*` keys in the export, because Rider only writes CSS settings when
they differ from default. The CSS formatter's configuration therefore has to be derived from
ReSharper's documented defaults and written into `options.json` like everything else.

## The language contract

```csharp
interface ISkalaLanguage {
    string Name { get; }
    bool CanHandle(string path, SkalaConfig config);

    ParseResult Parse(SourceText text, ParseContext ctx);      // tolerant; errors ⇒ do not format
    Doc BuildDocument(ParseResult parsed, FormattingOptions options);
    IEnumerable<Diagnostic> Analyze(ParseResult parsed, AnalysisContext ctx);
    bool AreEquivalent(SourceText before, SourceText after);   // the language's safety net
}
```

Four things worth noting about that shape:

- `BuildDocument` returns the **same `Doc` IR** the C# front end uses. The fitting algorithm, the
  width model, the `Preserve` group semantics, the minimal-edit emitter and the idempotency
  properties are all reused. That is the entire payoff of keeping
  `Rikarin.Skala.Formatting` language-agnostic ([02](02-repository-layout.md)).
- `AreEquivalent` is per-language because the invariant differs: for C# it is the token stream; for
  HTML it is the element tree plus text content modulo insignificant whitespace; for CSS it is the
  rule/declaration list. ⚠ HTML's version is the hard one, because whitespace *is* significant
  between inline elements, and a formatter that inserts a newline between `</b>` and `,` changes
  rendering. `resharper_preserve_spaces_inside_tags` and the inline-element list exist for exactly
  this and must be honoured before a single byte is written.
- `Parse` is tolerant by contract, but formatting still requires a clean parse. Same rule as C#
  (ADR-003): a file that did not parse is not formatted.
- `Analyze` is optional. The first HTML/CSS release may be formatting-only, and that is a complete
  deliverable.

## `.vxml` and `.vcss`

These are Vixen's own dialects and they are not HTML and CSS:

`.vxml` is an XML-shaped component file with a directive preamble (`@component`, `@namespace`,
`@tag`, `@using`) above the tree, and C#-ish expressions inside attribute values and interpolations.
`.vcss` is CSS with cascade layers, custom properties as design tokens, and a utility-class generator
that emits sheets of its own.

Two options for the front end, and the decision is deferred to when the work starts:

| Option | For | Against |
|---|---|---|
| **Vixen supplies a `Vixen.Skala.Languages` plugin** implementing `ISkalaLanguage` over its own parsers | The parsers already exist, already track the format, and already produce the tree the engine loads. A dialect change lands in one place. | Couples a Skala release to a Vixen release; requires Vixen's parsers to expose position-accurate trivia, which parsers written for loading rarely do. |
| **Skala writes tolerant parsers** for HTML/CSS with dialect extensions | No coupling; format-preserving trivia is designed in from the start | Two implementations of one grammar, and the second one silently drifts. This is exactly the failure ADR-001 avoids for configuration. |

⚠ The deciding question is whether Vixen's parsers preserve enough trivia to reconstruct the file.
If they do, the plugin wins. If they do not — and a loader-oriented parser usually does not — then
Skala's own parsers win, and the mitigation is a conformance test that parses every `.vxml`/`.vcss`
in Vixen with both and compares the semantic trees.

## What the core must not do before then

Constraints on v1 work that exist purely to keep this door open, and which cost nothing today:

1. `Rikarin.Skala.Formatting` may not reference Roslyn. Enforced by a reference test
   ([02](02-repository-layout.md)).
2. `Doc` may not carry `SyntaxToken`. `Anchor` carries a `TextSpan` and an opaque id; the C# front
   end keeps its own token table ([04](04-formatting-engine.md) § "The document IR").
3. The option registry's `language` field is already present in `options.json` and already filters
   the effective option set. Adding `html` and `css` must be data, not code.
4. `.editorconfig` section resolution is per-path and already language-blind.
5. The safety net is an interface, not a static call to a C# token comparer.

Each of those is a one-line decision now and a refactor later. That asymmetry is the only reason to
write this document before the work exists.

## Analysis for web languages, when it comes

The interesting rules are the ones that only make sense for Vixen's dialects, which is another
argument for the plugin model:

- A `class` in `.vxml` that no `.vcss` in the tree defines, and no utility generator emits.
- A `@using` in the preamble that nothing in the tree references.
- A custom property (`--surface`) read but never declared in any layer the document loads.
- A `@layer` statement that disagrees with the tree's canonical layer order — the exact defect the
  engine's own stylesheets carry a warning comment about, which means it is real, has happened, and
  is invisible until something renders wrong.
- Specificity conflicts between a control default and a utility class.

Those are engine semantics, not CSS semantics. Skala should provide the harness — parse, report,
fix, gate — and let the engine provide the rules, through the same `ISkalaLanguage.Analyze` seam.
