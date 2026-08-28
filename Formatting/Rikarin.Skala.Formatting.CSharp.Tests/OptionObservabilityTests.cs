using System.Globalization;
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
    ///     an unimplemented key wearing a reason. Tier A is still closed to them (no oracle fixture can
    ///     pin a documentation comment under the pinned profile, SK-DIV-0006), which is exactly the
    ///     combination <c>OfUnoracled</c> marks: honoured, observable, and unprovable against the
    ///     oracle.
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
    [Theory]
    [MemberData(nameof(Unoracled))]
    public void AnUnoracledKey_IsObservable(string key) {
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
                             /// <value>Some ordinary prose that runs on for a while before it finally reaches an inline element <see cref="System.String"/> right here.</value>
                             /// </remarks>
                             /// <param name="first">The first parameter, described at some length so that it too runs past the margin.</param><param name="second">The second.</param>
                             ///
                             ///
                             /// <returns>A value.<br/> Then <exception cref="System.OverflowException">Short.</exception> and then <exception cref="System.ArgumentException">a much longer description that will certainly not fit on the line it starts on.</exception></returns>
                             int Method(int first, int second) => first + second;
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
