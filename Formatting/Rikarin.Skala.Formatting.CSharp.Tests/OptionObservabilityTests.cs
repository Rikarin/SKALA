using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Options;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     The per-option unit docs/plan/03 § "The option registry" asks for: one generated case per
///     spacing and indentation key, formatting that key's own fixture at every value in its domain.
/// </summary>
/// <remarks>
///     ⚠ Two theories, and the second is the one that matters. Asserting that an implemented key
///     changes the output catches a key that was never wired; asserting that an <em>inert</em> key does
///     not catches the opposite mistake, which is the one this repository has actually made. M3.1 found
///     keys marked Tier A that could not be observed at all, and the fix for those was a sentence
///     saying "inert, because another rule wins" — a sentence nothing checked. A key whose reason has
///     gone stale is Tier D describing behaviour it no longer has, and it fails here.
///     <para>
///         ⚠ Scoped to <c>space_*</c>, <c>indent_*</c> and <c>outdent_*</c> on purpose. The conformance
///         suite runs the same measurement over every implemented key; this one is the fast, local copy
///         that fails in the project that owns the rules, so that a spacing change is answered by the
///         spacing tests rather than by a corpus-wide run twenty minutes later.
///     </para>
///     <para>
///         ⚠ Values are flipped from the <em>repository's</em> configuration and not from the registry's
///         bare defaults, for the reason the conformance copy records: an option is observable in the
///         configuration its fixture was generated under, and asking from ReSharper's defaults asks a
///         different question.
///     </para>
/// </remarks>
public sealed class OptionObservabilityTests {
    /// <summary>The families this milestone owns.</summary>
    static bool InFamily(string key) => key.Split('_').Any(static part => part is "space" or "indent" or "outdent");

    public static TheoryData<string> Honoured {
        get {
            var data = new TheoryData<string>();
            foreach (var id in PhaseOneOptions.Implemented) {
                var key = OptionRegistry.Get(id).Key;
                if (InFamily(key)) {
                    data.Add(key);
                }
            }

            return data;
        }
    }

    public static TheoryData<string> Inert {
        get {
            var data = new TheoryData<string>();
            foreach (var id in Ids.ReadButInert) {
                var key = OptionRegistry.Get(id).Key;
                if (InFamily(key)) {
                    data.Add(key);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Honoured))]
    public void EveryValue_IsDistinguishableOnTheKeysOwnFixture(string key) {
        var outputs = FormatAtEveryValue(key, out var files, out var values);
        Assert.True(
            outputs.Count > 1,
            $"{key}: every value in [{string.Join(", ", values)}] formats "
            + $"[{string.Join(", ", files)}] to the same bytes. The key is claimed as implemented and is not; "
            + "a Tier A badge on it says Skala reproduces Rider's behaviour, and nothing here can tell the two "
            + "behaviours apart."
        );
    }

    public static TheoryData<string> Unoracled {
        get {
            var data = new TheoryData<string>();
            foreach (var id in Ids.ReadButUnoracled) {
                data.Add(OptionRegistry.Get(id).Key);
            }

            return data;
        }
    }

    /// <summary>
    ///     ⚠ The mirror of the inert theory, and it exists because these keys used to be inert.
    /// </summary>
    /// <remarks>
    ///     The <c>resharper_xmldoc_*</c> family was <c>OfInert</c> while the sub-formatter was behind a
    ///     flag: read, and unable to change anything, because nothing ran it. The sub-formatter is the
    ///     default now, so the honest claim inverted — every one of these must change output, or it is
    ///     an unimplemented key wearing a reason.
    ///     <para>
    ///         ⚠ This remark used to continue "Tier A is still closed to them, because no oracle fixture
    ///         can pin a documentation comment under the pinned profile". That is withdrawn:
    ///         <c>OracleProfile.DocComments</c> pins them and 13 of the 22 are Tier A. What remains here
    ///         is the nine the oracle contradicts (SK-DIV-0019 … SK-DIV-0023), and for those
    ///         <c>OfUnoracled</c> now means "asked, and answered differently" rather than "unaskable" —
    ///         still honoured, still observable, and still barred from Tier A, which is what the
    ///         assertion below checks.
    ///     </para>
    ///     <para>
    ///         ⚠ Not scoped to <see cref="InFamily" />, and not measured on <c>constructs/</c>. Nine of the
    ///         seventeen are unobservable there, and the reason is the corpus rather than the keys: the
    ///         constructs fixtures carry short, already-tidy doc comments because nothing used to read them.
    ///         Asking a key about a corpus built before the key did anything answers a question about the
    ///         corpus. So the subject is <see cref="Probe" /> — one hand-written comment carrying every shape
    ///         the family governs — which is the same kind of evidence <c>XmlDocFormatterTests</c> pins the
    ///         semantics with, and is where these keys' evidence lives anyway.
    ///     </para>
    /// </remarks>
    /// <remarks>
    ///     ⚠ A <c>[Fact]</c> over the list rather than a <c>[Theory]</c> per key, because the list is
    ///     empty and an empty <c>MemberData</c> is a red build rather than a green one. The emptiness is
    ///     the finding and is asserted below: nothing in the <c>resharper_xmldoc_*</c> family is
    ///     "asked, and answered differently" any more. Three of the four that were —
    ///     <c>max_line_length</c>, <c>wrap_text</c>, <c>linebreak_before_singleline_elements</c> —
    ///     agree at every value once the model behind them was re-probed rather than read off a
    ///     fixture, and the fourth, <c>wrap_tags_and_pi</c>, turned out to govern a construct Skala
    ///     does not produce and moved to <c>XmlDocIds.Refused</c> (SK-DIV-0079). The loop still runs,
    ///     so the moment a key is registered <c>OfUnoracled</c> again it has to be observable.
    /// </remarks>
    [Fact]
    public void AnUnoracledKey_IsObservable() {
        foreach (var key in Ids.ReadButUnoracled.Select(static id => OptionRegistry.Get(id).Key)) {
            var outputs = FormatProbeAtEveryValue(key, out var values);
            Assert.True(
                outputs.Count > 1,
                $"{key} is registered OfUnoracled — honoured, and not provable against the oracle — and every value "
                + $"in [{string.Join(", ", values)}] formats the doc-comment probe to the same bytes. "
                + "Unoracled is a statement about the evidence, never about the wiring: a key nothing can observe is "
                + "unimplemented, and calling it unoracled hides that behind a reason that sounds like one. Either wire "
                + "it up, move it to XmlDocIds.Refused with a reason, or widen the probe if the shape it governs is "
                + "genuinely missing from it."
            );

            Assert.True(OptionRegistry.TryResolve(key, out var id));
            Assert.NotEqual(OptionTier.A, OptionRegistry.Get(id).Tier);
            Assert.NotEqual(OptionTier.B, OptionRegistry.Get(id).Tier);
        }

        // ⚠ Not anti-vacuity theatre. `OfUnoracled` is a real state and this line pins how many keys
        // are in it, so that a key acquiring the mark has to change a number here and cannot slip in
        // under an assertion that was passing on an empty list.
        //
        // ⚠ It went from empty to one at SK-DIV-0033. `resharper_csharp_align_multiline_comments` is
        // the mark in its documented sense — asked, and answered differently: Skala reproduces the
        // oracle byte for byte at the export's `true`, and at `false` the oracle freezes a starred
        // comment entire, including the column its opening `/*` is written at, which Skala re-indents
        // at both values. Honoured, observable, and not conformant at one of two values, which is
        // exactly what bars Tier A. The probe above carries the block comment it is observed on.
        Assert.Equal(
            ["resharper_csharp_align_multiline_comments"],
            Ids.ReadButUnoracled.Select(static id => OptionRegistry.Get(id).Key)
        );
    }

    /// <summary>
    ///     One documentation comment carrying every shape the <c>resharper_xmldoc_*</c> family governs.
    /// </summary>
    /// <remarks>
    ///     ⚠ Deliberately ugly, and every piece of the ugliness is load-bearing: a summary past the
    ///     column limit (wrapping), two <c>&lt;param&gt;</c>s sharing a line (linebreak_before_elements),
    ///     an element with children and no text (indent_child_elements), an element with text
    ///     (indent_text), a self-closing tag (space_before_self_closing), blank <c>///</c> lines between
    ///     tags (max_blank_lines_between_tags), a single-line element after text
    ///     (linebreak_before_singleline_elements) and a multi-line one
    ///     (linebreak_before_multiline_elements). It must stay well-formed XML — a malformed comment is
    ///     left exactly as written (SK0003), which would make every key here look unobservable at once.
    ///     <para>
    ///         ⚠ Widened with a processing instruction and a multi-attribute tag header, which is what the
    ///         failure message above asks for when a key's shape is missing rather than its wiring. Four
    ///         keys had no shape here to be observed on: <c>blank_line_after_pi</c> had no
    ///         <c>&lt;?…?&gt;</c>, and <c>space_after_last_attribute</c>,
    ///         <c>spaces_around_eq_in_attribute</c> and
    ///         <c>linebreaks_inside_tags_for_elements_longer_than</c> had only single-attribute headers
    ///         and short contents to work on.
    ///     </para>
    ///     <para>
    ///         ⚠ Widened again, and this time by a probe defect the wrap-column fix exposed rather than by
    ///         a missing construct. Every "long enough that it will not fit" phrase here was written
    ///         against a budget seven columns narrower than the one SK-DIV-0019 measured, so
    ///         <c>linebreak_before_multiline_elements</c> lost its only shape: the <c>&lt;exception&gt;</c>
    ///         it governed is now moved by the width wrap at both values, and a key that is masked reads
    ///         exactly like a key that is unwired. The shape it actually governs is a <em>short</em>
    ///         element that is multi-line for a structural reason, beside prose — the line added below —
    ///         and the key moves it: at <c>false</c> the <c>&lt;list&gt;</c>'s start tag stays on the prose's
    ///         line. Asserting the answer without asking whether the probe could still see it is the
    ///         mistake this repository has made in four separate areas.
    ///     </para>
    /// </remarks>
    const string Probe = """
                         class Probe {
                             /// <?skala-probe mode="short" width="80"?>
                             /// <summary>A summary written long enough that it cannot fit inside the configured column limit and has to be broken somewhere.</summary>
                             /// <remarks>
                             /// A first line the author chose to break here,
                             /// and a second the author broke here, both short enough that they would join into one.
                             /// <list><item>One item.</item><item>Another item, written at enough length that the list cannot fit on any single line of its own.</item></list>
                             /// <para>A paragraph.</para><para>A second paragraph, itself long enough that it will not sit on one line beside the first.</para>
                             /// <list><item>A.</item></list>
                             /// Prose beside a short list. <list><item>B.</item></list> and prose after it.
                             /// <value>Some ordinary prose that runs on for a while before it finally reaches an inline element <see cref="System.String"/> right here.</value>
                             /// </remarks>
                             /// <param name="first">The first parameter, described at some length so that it too runs past the margin.</param><param name="second">The second.</param>
                             ///
                             ///
                             /// <returns>A value.<br/> Then <exception cref="System.OverflowException">Short.</exception> and then <exception cref="System.ArgumentException">a much longer description that will certainly not fit on the line it starts on.</exception></returns>
                             int Method(int first, int second) => first + second;

                             /*
                          * A starred block comment whose asterisks are out of place, written at an opener
                            * column the code does not put it at, so that `align_multiline_comments` has
                          * something to move. ⚠ Every continuation line begins with `*`, which is what
                          * makes the comment qualify — see CSharpDocumentBuilder.StarredFlag.
                              */
                             int Second() => 2;
                         }
                         """;

    static HashSet<string> FormatProbeAtEveryValue(string key, out string[] values) {
        Assert.True(OptionRegistry.TryResolve(key, out var id), $"{key} is not in the registry.");
        var info = OptionRegistry.Get(id);
        values = [.. OptionDomain.Probes(info)];
        Assert.True(values.Length >= 2, $"{key}: fewer than two values to compare.");

        // ⚠ A real path under the corpus, because the .editorconfig chain is resolved from it and
        // the question is "observable in the configuration this repository actually sets", not
        // "observable from ReSharper's bare defaults" — the same rule the corpus theories follow.
        var path = Path.Combine(Corpus.Root, Corpus.Constructs, "xmldoc-observability-probe.cs");
        var outputs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values) {
            var resolved = OptionResolver.Resolve(path, [new KeyValuePair<string, string>(key, value)]);
            Assert.True(resolved.ValueErrors.IsEmpty, $"{key} = {value}: {string.Join("; ", resolved.ValueErrors)}");
            var result = CSharpFormatter.Format(path, SourceText.From(Probe), resolved.Options);
            Assert.Equal(FormatOutcome.Formatted, result.Outcome);
            outputs.Add(result.Formatted);
        }

        return outputs;
    }

    /// <summary>
    ///     ⚠ An inert key has to stay inert, or its reason is fiction.
    /// </summary>
    /// <remarks>
    ///     The failure this catches is a good one: a later rule gives an inert key something to decide,
    ///     nobody notices, and the key keeps a Tier D badge saying it cannot be observed while it
    ///     silently governs real output. Promote it and delete the reason — that is what the failure
    ///     message asks for.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Inert))]
    public void AnInertKey_StillCannotBeObserved(string key) {
        var outputs = FormatAtEveryValue(key, out var files, out var values);
        Assert.True(
            outputs.Count <= 1,
            $"{key} is recorded as inert — read but unable to change anything — and it just changed "
            + $"[{string.Join(", ", files)}] across [{string.Join(", ", values)}]. Either a rule started "
            + "consulting it or a rule stopped masking it. Move it from OfInert to Of, promote it to Tier A, "
            + "and delete the reason at its declaration; an option that can change behaviour must not carry a "
            + "note saying it cannot."
        );
    }

    static HashSet<string> FormatAtEveryValue(string key, out string[] files, out string[] values) {
        Assert.True(OptionRegistry.TryResolve(key, out var id), $"{key} is not in the registry.");
        var info = OptionRegistry.Get(id);
        values = [.. OptionDomain.Probes(info)];
        Assert.True(values.Length >= 2, $"{key}: fewer than two values to compare.");

        // ⚠ A key with no `oracle` glob is measured on the whole constructs set rather than skipped.
        // Skipping is how a key with no fixture stays unmeasured forever, which is the state the
        // fixture requirement exists to prevent.
        var corpus = info.Oracle is null ? Corpus.Files(Corpus.Constructs).ToList() : Resolve(info.Oracle);
        Assert.True(corpus.Count > 0, $"{key}: `oracle` is '{info.Oracle}' and no corpus file matches it.");
        files = [.. corpus.Select(static file => file.RelativePath)];

        var distinct = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in corpus) {
            var text = CSharpFormatter.Read(file.Path);
            var perFile = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values) {
                var resolved = OptionResolver.Resolve(file.Path, [new KeyValuePair<string, string>(key, value)]);
                Assert.True(
                    resolved.ValueErrors.IsEmpty,
                    $"{key} = {value}: {string.Join("; ", resolved.ValueErrors)}"
                );
                perFile.Add(CSharpFormatter.Format(file.Path, text, resolved.Options).Formatted);
            }

            if (perFile.Count > distinct.Count) {
                distinct = perFile;
            }

            if (distinct.Count > 1) {
                break;
            }
        }

        return distinct;
    }

    static List<CorpusFile> Resolve(string glob) {
        var files = new List<CorpusFile>();
        foreach (var set in new[] { Corpus.Constructs, Corpus.Real, Corpus.Pathological }) {
            var prefix = set + "/";
            if (glob.StartsWith(prefix, StringComparison.Ordinal)) {
                var pattern = glob[prefix.Length..];
                files.AddRange(Corpus.Files(set).Where(file => Matches(file.RelativePath, pattern)));
            }
        }

        return files;
    }

    static bool Matches(string path, string pattern) {
        if (!pattern.Contains('*', StringComparison.Ordinal)) {
            return string.Equals(path, pattern, StringComparison.Ordinal);
        }

        var parts = pattern.Split('*');
        var cursor = 0;
        for (var i = 0; i < parts.Length; i++) {
            if (parts[i].Length == 0) {
                continue;
            }

            var index = path.IndexOf(parts[i], cursor, StringComparison.Ordinal);
            if (index < 0 || i == 0 && index != 0) {
                return false;
            }

            cursor = index + parts[i].Length;
        }

        return parts[^1].Length == 0 || path.EndsWith(parts[^1], StringComparison.Ordinal);
    }
}
