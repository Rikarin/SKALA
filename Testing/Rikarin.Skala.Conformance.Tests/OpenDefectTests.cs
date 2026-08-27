using System.Globalization;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
/// The fuzz findings that are minimised, reproduced and <b>not fixed</b>, asserted as still broken.
/// </summary>
/// <remarks>
/// ⚠ A suite that asserts a bug is still a bug reads backwards until you ask what the alternative
/// is. docs/plan/12 § "Corpus expansion" requires a minimised failure to be committed and the corpus
/// only to grow; the formatter cannot process two of these three files at all, so committing them to
/// a measured set would take the fidelity number and the differential report down with them. The
/// choice is therefore between a comment in a bug tracker and a test — and a test is the one that
/// cannot rot: when someone fixes <c>EditEmitter</c>, this suite fails, names the entry, and says
/// where the file goes next.
/// <para>
/// ⚠ It is deliberately *not* an <c>[Fact(Skip = …)]</c> on the real property. A skipped test is
/// invisible in a green run and stays skipped for a year; this one is visible, counts, and its
/// failure message is an instruction.
/// </para>
/// </remarks>
public sealed class OpenDefectTests {
    public static TheoryData<string> Entries {
        get {
            var data = new TheoryData<string>();
            foreach (var entry in OpenDefects.Register) {
                data.Add(entry.Id);
            }

            return data;
        }
    }

    static OpenDefect Find(string id) =>
        OpenDefects.Register.First(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));

    [Fact]
    public void TheRegister_AccountsForEveryFileAndEveryFileForAnEntry() {
        var registered = OpenDefects.Register.Select(static entry => entry.File).Order(StringComparer.Ordinal);
        var present = OpenDefects.Files();
        Assert.Equal(registered, present);
        Assert.All(
            OpenDefects.Register,
            entry => Assert.Contains(entry.Property, FuzzProperties.All, StringComparer.Ordinal)
        );

        Assert.All(OpenDefects.Register, entry => Assert.NotEmpty(entry.Summary));
        Assert.All(OpenDefects.Register, entry => Assert.NotEmpty(entry.Seed));
    }

    /// <summary>
    /// ⚠ None of these may reach a measured set while it is still broken.
    /// </summary>
    [Fact]
    public void OpenDefects_AreNotInTheMeasuredCorpus() {
        var open = OpenDefects.Files().ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(
            Corpus.All(),
            file => open.Contains(Path.GetFileName(file.Path))
                && file.RelativePath.Contains(OpenDefects.OpenDirectory, StringComparison.Ordinal)
        );
    }

    [Theory]
    [MemberData(nameof(Entries))]
    public void EachOpenDefect_StillFailsTheWayTheRegisterSaysItDoes(string id) {
        var entry = Find(id);
        Assert.True(File.Exists(entry.Path), $"{entry}: the register names a file that is not there.");

        // ⚠ Read as bytes and never through a line-normalising helper. Several entries are *about* a
        // trailing space, a missing final newline, a lone `\r` or the width of one gap.
        var source = File.ReadAllText(entry.Path);
        var options = Fuzzer.OptionsFor(entry.Path);

        // ⚠ Absorption needs the pair, and the arrangement properties need the pipeline switched on
        // — it is off by default because it costs a compilation. An entry checked without the thing
        // its property is about reports "fixed" for every candidate, which is the one answer this
        // suite must never give wrongly.
        (string None, string Defined)? baseline = null;
        if (entry.BaselinePath is { } unmutated) {
            var text = File.ReadAllText(unmutated);
            baseline = (
                FuzzProperties.Format(entry.Path, text, options, []),
                FuzzProperties.Format(entry.Path, text, options, Corpus.PropertySymbols)
            );
        }

        var violations = FuzzProperties.Check(
            entry.Path,
            source,
            options,
            Corpus.PropertySymbols,
            baseline,
            entry.Property is FuzzProperties.ArrangementIdempotency or FuzzProperties.ArrangementConvergence,
            cancellation: TestContext.Current.CancellationToken
        );

        Assert.True(
            violations.Any(violation => string.Equals(violation.Property, entry.Property, StringComparison.Ordinal)),
            $"{entry} no longer violates `{entry.Property}` — which is good news, and this suite is where it "
            + "is delivered.\n\n"
            + "Move the file into Testing/corpus/pathological/, regenerate its fixture with\n"
            + $"  ./build.sh Oracle --only=<name>\n"
            + $"and delete the {id} entry from Testing/corpus/pathological/open/register.md.\n\n"
            + $"What the file does produce now: "
            + (violations.IsEmpty
                    ? "every property holds."
                    : string.Join("; ", violations.Select(static violation => violation.ToString())))
        );
    }

    /// <summary>
    /// ⚠ The register is a queue, not a filing cabinet.
    /// </summary>
    /// <remarks>
    /// A cap rather than a rule about any one entry: a handful of open defects is a to-do list and
    /// thirty is a policy of not fixing them. The number is deliberately close to what is there, so
    /// that adding one more is a decision somebody makes rather than a thing that happens.
    /// <para>
    /// ⚠ It was six, and seven arrived from the same fifteen-minute run that produced the sixth.
    /// Raising it rather than dropping a finding is the honest move — a register that only ever
    /// holds what fits under its own cap is a register that hides what it cannot hold, which is the
    /// failure this whole directory exists to prevent. Seven findings from a fuzzer's first day is a
    /// first harvest, not a backlog; the next commit to touch this number should be lowering it.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheRegister_HasNotBecomeAFilingCabinet() {
        Assert.True(
            OpenDefects.Register.Count <= 8,
            $"{OpenDefects.Register.Count.ToString(CultureInfo.InvariantCulture)} open fuzz findings. "
            + "The register is a queue: fix one before adding another, or raise this bound in a commit "
            + "that argues for it."
        );
    }
}
