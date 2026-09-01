using System.Globalization;
using System.Text.RegularExpressions;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The instrument for a rule whose subject matter is invisible in every tool that shows it.
/// </summary>
/// <remarks>
///     ⚠ <b>An <c>SK2072</c> fixture cannot be proofread.</b> A positive fixture has to carry a real
///     zero-width byte — the rule reads the token's source spelling, so an escaped one is correctly
///     silent — and that byte is exactly as invisible in the fixture as it is in the code the rule
///     exists to find. A fixture whose byte was stripped by an editor, a merge or a formatter looks
///     identical to one that was never written, and it fails as "the rule did not fire", which reads
///     as a defect in the rule.
///     <para>
///         So every fixture <em>declares</em> what it carries, in <c>// contains: U+XXXX</c> headers,
///         and this test asserts the declaration against the file's actual bytes in both directions:
///         a byte that went missing is red, and a byte nobody declared is red. That is what turns
///         "I looked at it and it seemed right" into a measurement.
///     </para>
///     <para>
///         ⚠ The analyzer's own source is held to the opposite rule — no such byte at all, every code
///         point written as an escape. A table of literal zero-width characters is a table nobody can
///         review, and Skala's own <c>format --check</c> would carry the bytes along without comment.
///     </para>
/// </remarks>
public sealed class InvisibleCharacterFixtureTests {
    static string FixtureRoot => Path.Combine(RuleFixtures.Root, "SK2072");

    static string AnalyzerPath => Path.GetFullPath(
        Path.Combine(RuleFixtures.Root, "..", "..", "Rikarin.Skala.Rules", "Correctness", "InvisibleCharacterAnalyzer.cs")
    );

    [Fact]
    public void EveryFixture_CarriesExactlyTheCodePointsItDeclares() {
        var files = Directory.GetFiles(FixtureRoot, "*.cs", SearchOption.AllDirectories)
            .OrderBy(static f => f, StringComparer.Ordinal)
            .ToArray();

        // Anti-vacuity: every assertion below passes against an empty file list.
        Assert.True(files.Length >= 10, $"Only {files.Length} SK2072 fixture(s) were found under {FixtureRoot}.");

        var carrying = 0;
        foreach (var file in files) {
            var text = File.ReadAllText(file);

            var declared = Regex.Matches(text, @"^// contains: U\+([0-9A-F]{4})$", RegexOptions.Multiline)
                .Select(static m => (char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                .ToHashSet();

            var present = text.Where(NotOrdinarySource).ToHashSet();

            var name = Path.GetFileName(file);
            Assert.True(
                present.SetEquals(declared),
                $"{name}: declares [{Format(declared)}] and holds [{Format(present)}]. "
                + "A byte that went missing reads as 'the rule did not fire'; one nobody declared is "
                + "a byte no reviewer could have seen."
            );

            if (declared.Count > 0) {
                carrying++;
            }
        }

        Assert.True(carrying >= 8, $"Only {carrying} fixture(s) carry a byte; the rest prove nothing about bytes.");
    }

    [Fact]
    public void TheAnalyzerSource_WritesEveryCodePointAsAnEscape() {
        var text = File.ReadAllText(AnalyzerPath);

        Assert.True(
            text.Contains("InvisibleCharacterAnalyzer", StringComparison.Ordinal) && text.Length > 4000,
            $"{AnalyzerPath} was read and is not the analyzer ({text.Length} bytes); the check below proves nothing."
        );

        // The escape spelling is the point: if the table were written with literal characters this
        // count would be zero and the file would still compile and still work.
        Assert.True(
            Regex.Count(text, @"'\\u[0-9A-F]{4}'") >= 20,
            "The analyzer's character table is not written as escapes."
        );

        var offenders = text.Where(NotOrdinarySource).ToHashSet();
        Assert.True(
            offenders.Count == 0,
            $"{Path.GetFileName(AnalyzerPath)} contains [{Format(offenders)}] as literal bytes. "
            + "Every code point in this file is written as an escape, comments included."
        );
    }

    /// <summary>
    ///     A character a reviewer would not see as itself: any control other than the line endings
    ///     and the tab-free indentation this repository uses, and anything above ASCII except the
    ///     handful of punctuation marks the prose is written with.
    /// </summary>
    static bool NotOrdinarySource(char c) =>
        c is not ('\n' or '\r') && (c < ' ' || c == '\u007F' || c > '~' && !Prose.Contains(c));

    static readonly HashSet<char> Prose = ['⚠', '—', '–', '…', '“', '”', '‘', '’'];

    static string Format(IEnumerable<char> chars) =>
        string.Join(
            ", ",
            chars.OrderBy(static c => c)
                .Select(static c => "U+" + ((int)c).ToString("X4", CultureInfo.InvariantCulture))
        );
}
