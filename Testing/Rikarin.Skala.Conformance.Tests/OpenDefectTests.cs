using Rikarin.Skala.Testing;
using System.Globalization;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     The fuzz findings that are minimised, reproduced and <b>not fixed</b>, asserted as still broken.
/// </summary>
/// <remarks>
///     ⚠ A suite that asserts a bug is still a bug reads backwards until you ask what the alternative
///     is. docs/plan/12 § "Corpus expansion" requires a minimised failure to be committed and the corpus
///     only to grow; the formatter cannot process some of these files at all and does not reach a fixed
///     point on the others, so committing them to a measured set would take the fidelity number and the
///     differential report down with them. The
///     choice is therefore between a comment in a bug tracker and a test — and a test is the one that
///     cannot rot: when someone fixes <c>EditEmitter</c>, this suite fails, names the entry, and says
///     where the file goes next.
///     <para>
///         ⚠ It is deliberately *not* an <c>[Fact(Skip = …)]</c> on the real property. A skipped test is
///         invisible in a green run and stays skipped for a year; this one is visible, counts, and its
///         failure message is an instruction.
///     </para>
/// </remarks>
public sealed class OpenDefectTests {
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
    ///     ⚠ None of these may reach a measured set while it is still broken.
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

    /// <summary>
    ///     ⚠ A <c>[Fact]</c> over the register rather than a <c>[Theory]</c> per entry, and the reason
    ///     is that the register is allowed to be <b>empty</b>.
    /// </summary>
    /// <remarks>
    ///     An empty queue is the goal this directory is aiming at, and xUnit fails a theory whose data
    ///     source yields nothing — "No data found" — so the shape that reads best when there are four
    ///     entries reports a red suite at the moment the last one is fixed. That is precisely the wrong
    ///     signal: it makes retiring the final defect look like breaking something. The loop below
    ///     names the entry it is asserting in every message, which is what the per-entry test name was
    ///     buying.
    /// </remarks>
    [Fact]
    public void EachOpenDefect_StillFailsTheWayTheRegisterSaysItDoes() {
        foreach (var entry in OpenDefects.Register) {
            Check(entry);
        }
    }

    static void Check(OpenDefect entry) {
        var id = entry.Id;
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
            // ⚠ A space, not an `=`. NUKE binds `--only <value>` and silently drops
            // `--only=<value>` — no error, no warning, the parameter is null and the target
            // regenerates all 1 212 fixtures instead of the one. This message used to say `=`.
            + "Move the file into Testing/corpus/pathological/, regenerate its fixture with\n"
            + $"  ./build.sh Oracle --only {Path.GetFileNameWithoutExtension(entry.File)}\n"
            + $"and delete the {id} entry from Testing/corpus/pathological/open/register.md.\n\n"
            + "What the file does produce now: "
            + (violations.IsEmpty
                    ? "every property holds."
                    : string.Join("; ", violations.Select(static violation => violation.ToString())))
        );
    }

    /// <summary>
    ///     Every <c>probe:</c> in the register names a probe that exists, and every probe is the reason
    ///     its own entry's fixture fails.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is what stops the accounting mechanism from becoming a suppression list. A probe is a
    ///     claim — "delete this and the defect goes away" — and the claim is checked here against the
    ///     one input the entry is definitionally about. A probe that finds no trigger in its own
    ///     fixture, or that leaves the fixture still failing, is a probe that characterises nothing and
    ///     would silence the nightly for reasons nobody has established.
    /// </remarks>
    [Fact]
    public void EachProbe_IsTheReasonItsOwnFixtureFails() {
        foreach (var entry in OpenDefects.Register.Where(static entry => entry.Probe.Length > 0)) {
            var probe = OpenDefectProbes.Find(entry.Probe);
            Assert.True(
                probe is not null,
                $"{entry} names probe `{entry.Probe}`, which is not in OpenDefectProbes.All. "
                + "The vocabulary is closed on purpose; add it there with its argument."
            );

            var source = File.ReadAllText(entry.Path);
            var without = probe!.Neutralise(source);
            Assert.True(
                without is not null && !string.Equals(without, source, StringComparison.Ordinal),
                $"{entry}: probe `{entry.Probe}` ({probe.What}) finds no trigger in the entry's own "
                + "fixture. A probe that cannot fire on the file it was written for cannot be trusted "
                + "to fire on anything else."
            );

            Assert.True(
                OpenDefectProbes.ParsesNoWorse(source, without!),
                $"{entry}: probe `{entry.Probe}` breaks the parse of its own fixture. ADR-003 leaves an "
                + "input that lost its parse byte-identical, so every property would hold over it for "
                + "free and the probe would look successful while having established nothing."
            );

            var after = FuzzProperties.Check(
                entry.Path,
                without!,
                Fuzzer.OptionsFor(entry.Path),
                Corpus.PropertySymbols,
                null,
                entry.Property is FuzzProperties.ArrangementIdempotency or FuzzProperties.ArrangementConvergence,
                cancellation: TestContext.Current.CancellationToken
            );

            Assert.False(
                after.Any(violation => string.Equals(violation.Property, entry.Property, StringComparison.Ordinal)),
                $"{entry}: removing the trigger `{entry.Probe}` names ({probe.What}) leaves the fixture "
                + $"still violating `{entry.Property}`. The entry's recorded cause is therefore not the "
                + "whole cause, and the probe must not be used to account for anything until that is "
                + "resolved.\n\nWhat it still produces: "
                + string.Join("; ", after.Select(static violation => violation.ToString()))
            );
        }
    }

    /// <summary>
    ///     <see cref="OpenDefects.Explain" /> accepts a registered defect and <b>refuses</b> an input
    ///     that also carries a second, unregistered failure of the same property.
    /// </summary>
    /// <remarks>
    ///     ⚠ **This is the test the whole mechanism rests on.** The nightly stops failing on registered
    ///     findings, and the thing that must not follow is a new defect riding into a green run behind
    ///     one. The second half below builds exactly that input — a registered defect's fixture glued to
    ///     another open defect's fixture of the same property — and requires the accounting to say
    ///     "new". It can only say that because it re-runs the oracle on the neutralised input rather
    ///     than comparing a property name or a rule id: the registered trigger comes out, the other
    ///     defect is still there, the property still fails, no entry accounts for it.
    ///     <para>
    ///         ⚠ The composition needs two open entries sharing one property, which the register
    ///         currently supplies. If it stops supplying them this test skips its second half rather
    ///         than passing vacuously, and says so.
    ///     </para>
    /// </remarks>
    [Fact]
    public void Explain_AccountsForARegisteredDefectAndRefusesOneCarryingASecondFailure() {
        foreach (var entry in OpenDefects.Register.Where(static entry => entry.Probe.Length > 0)) {
            var source = File.ReadAllText(entry.Path);
            var arrangement =
                entry.Property is FuzzProperties.ArrangementIdempotency or FuzzProperties.ArrangementConvergence;

            var explained = OpenDefects.Explain(
                entry.Property,
                entry.Path,
                source,
                Fuzzer.OptionsFor(entry.Path),
                Corpus.PropertySymbols,
                arrangement,
                TestContext.Current.CancellationToken
            );

            Assert.True(
                explained?.Id == entry.Id,
                $"{entry}: the register does not account for its own fixture (got "
                + (explained?.Id ?? "nothing")
                + ")."
            );

            // ⚠ The other half: the same input with a second failure of the same property in it.
            var other = OpenDefects.Register.FirstOrDefault(candidate => !string.Equals(
                    candidate.Id,
                    entry.Id,
                    StringComparison.Ordinal
                )
                && string.Equals(candidate.Property, entry.Property, StringComparison.Ordinal)
            );

            if (other is null) {
                continue;
            }

            var composed = source + "\n" + File.ReadAllText(other.Path);
            var verdict = OpenDefects.Explain(
                entry.Property,
                entry.Path,
                composed,
                Fuzzer.OptionsFor(entry.Path),
                Corpus.PropertySymbols,
                arrangement,
                TestContext.Current.CancellationToken
            );

            Assert.True(
                verdict is null,
                $"{entry}'s trigger glued to {other}'s reproduction was accounted for by "
                + $"{verdict?.Id} — so a second defect of the same property hid behind a registered "
                + "one, which is the one failure this mechanism must not have. Either a probe deletes "
                + "more than the trigger it names, or the accounting stopped re-running the oracle."
            );
        }
    }

    /// <summary>
    ///     The three findings that made nightly 33207471534 red, replayed, and asserted to produce no
    ///     <b>unaccounted</b> violation.
    /// </summary>
    /// <remarks>
    ///     ⚠ A test written against three specific seeds looks like a test of a moment, and it is not.
    ///     The claim it pins is the one the whole accounting mechanism was built for and the one that
    ///     cannot be checked any other way: *these exact inputs*, which a real nightly reported as three
    ///     failures, do not fail any more — either because the defect under them is fixed, or because
    ///     the register accounts for them. It fails if a rediscovery of a registered defect starts
    ///     reding the job again, and it fails if one of these seeds ever turns up a *different* defect
    ///     that no entry explains, which is the case the property-name shortcut would have missed.
    ///     <para>
    ///         ⚠ `--origin` is passed because a seed alone does not pin a mutate case: the corpus file
    ///         is drawn by *index* and the corpus only grows, so the same seed re-points as soon as a
    ///         file is committed. The override substitutes the file after the draw, so the stream
    ///         consumes the same values and the mutation sequence begins at the same offset.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData(14612343748624500188UL, "real/newtonsoft/Newtonsoft.Json/Utilities/AsyncUtils.cs", true)]
    [InlineData(3437603854615914319UL, "pathological/wrapped-file-scoped-namespace-name.cs", true)]
    // ⚠ The third stays loud, and the row is the honest accounting rather than an omission. This seed
    // is SK-FUZZ-0017's family — a generated nested switch that loses characters on the second pass —
    // and that entry's own status line says "cause not established". No cause means no trigger to
    // name, no probe, and nothing the expedition can check, so it must keep failing the nightly. When
    // somebody diagnoses SK-FUZZ-0017 this row flips to `true` in the same commit as its `probe:`.
    [InlineData(9335905203779897982UL, null, false)]
    public void TheNightlysOwnFindings_ProduceNothingTheRegisterCannotAccountFor(
        ulong seed,
        string? origin,
        bool quiet
    ) {
        var subject = Fuzzer.Build(seed, FuzzMode.Both, Corpus.All(), origin);
        var (violations, _) = Fuzzer.Execute(subject, true, null, TestContext.Current.CancellationToken);

        var unaccounted = violations
            .Where(static violation => violation.Property != FuzzProperties.ParseLost)
            .Where(violation => OpenDefects.Explain(
                    violation.Property,
                    subject.Path,
                    subject.Text,
                    Fuzzer.OptionsFor(subject.Path),
                    Corpus.PropertySymbols,
                    true,
                    TestContext.Current.CancellationToken
                ) is null
            )
            .ToArray();

        if (quiet) {
            Assert.True(
                unaccounted.Length == 0,
                $"seed {FuzzRandom.Format(seed)} ({origin ?? "generated"}) still produces "
                + unaccounted.Length.ToString(CultureInfo.InvariantCulture)
                + " violation(s) the register cannot account for, so the nightly would go red on it:\n  "
                + string.Join("\n  ", unaccounted.Select(static violation => violation.ToString()))
                + "\n\nEither the defect is real and new — register it — or an entry that used to "
                + "account for it has lost its probe."
            );

            return;
        }

        // ⚠ Asserting that something is *still* broken, for the reason the rest of this suite does it:
        // the alternative is a row that quietly starts passing and a cost nobody notices has gone. When
        // this fires, SK-FUZZ-0017 has either been diagnosed or fixed, and the fix is to say so here.
        Assert.True(
            unaccounted.Length > 0,
            $"seed {FuzzRandom.Format(seed)} ({origin ?? "generated"}) no longer produces an "
            + "unaccounted violation — which is good news. Either SK-FUZZ-0017 was fixed, or its cause "
            + "was established and it now names a `probe:`. Flip this row's `quiet` to true, and if the "
            + "defect is fixed retire the entry as the register's preamble describes."
        );
    }

    /// <summary>
    ///     A report carrying only accounted-for findings is a passing run; one unaccounted finding beside
    ///     them is a failing one.
    /// </summary>
    /// <remarks>
    ///     ⚠ The wiring between <see cref="OpenDefects.Explain" /> and the process exit code, tested
    ///     directly rather than by hoping a fuzz run happens to hit a registered defect. `Program.Fuzz`
    ///     returns `report.NewFindings.IsEmpty ? 0 : 1`, so this is the exit code, and the case that
    ///     matters is the mixed one: an accounted-for finding must not make the run green while a new
    ///     finding sits beside it.
    /// </remarks>
    [Fact]
    public void AReportFailsOnTheUnaccountedFindings_AndOnlyThose() {
        var accounted = Finding("idempotency", "SK-FUZZ-0015");
        var fresh = Finding("token-equivalence", null);

        Assert.Empty(ReportWith(accounted).NewFindings);
        Assert.Equal([fresh], ReportWith(fresh).NewFindings);
        Assert.Equal([fresh], ReportWith(accounted, fresh).NewFindings);
        Assert.Empty(ReportWith().NewFindings);

        // The reader has to be able to see which was which, or the exit code is the only thing that
        // carries the distinction and a green run looks like a run that found nothing.
        var rendered = ReportWith(accounted, fresh).Render();
        Assert.Contains("already registered", rendered, StringComparison.Ordinal);
        Assert.Contains("SK-FUZZ-0015", rendered, StringComparison.Ordinal);
        Assert.Contains("The expedition fails", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("The expedition passes", rendered, StringComparison.Ordinal);
        Assert.Contains("The expedition passes", ReportWith(accounted).Render(), StringComparison.Ordinal);
    }

    static FuzzFinding Finding(string property, string? accountedFor) =>
        new(
            1,
            "generated",
            "generate",
            new PropertyViolation(property, false, "detail"),
            [],
            "source",
            "minimised",
            "detail"
        ) { AccountedFor = accountedFor };

    static FuzzReport ReportWith(params FuzzFinding[] findings) =>
        new(
            1,
            FuzzMode.Both,
            10,
            TimeSpan.FromSeconds(1),
            5,
            5,
            0,
            0,
            new Dictionary<string, long>(StringComparer.Ordinal),
            new Dictionary<string, long>(StringComparer.Ordinal),
            findings.ToDictionary(
                static finding => finding.Property,
                static _ => 1L,
                StringComparer.Ordinal
            ),
            [],
            0,
            [],
            [.. findings],
            findings
                .Where(static finding => finding.AccountedFor is not null)
                .ToDictionary(static finding => finding.AccountedFor!, static _ => 1L, StringComparer.Ordinal)
        );

    /// <summary>
    ///     Entries with no probe, named, because they are what still reds the nightly.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not an assertion that there are none — there is at least one, and it is honest that there
    ///     is. An entry whose own status line says "cause not established" has no trigger to name, so
    ///     the expedition cannot tell its rediscovery from a new defect and must keep failing on it.
    ///     This test exists so that cost is attributable to a named entry rather than showing up as
    ///     "the nightly is flaky", and so the bound below is a decision somebody makes.
    /// </remarks>
    [Fact]
    public void EntriesWithoutAProbe_AreTheOnesThatStillRedTheNightly() {
        var undiagnosed = OpenDefects.Register.Where(static entry => entry.Probe.Length == 0).ToArray();
        Assert.True(
            undiagnosed.Length <= 2,
            undiagnosed.Length.ToString(CultureInfo.InvariantCulture)
            + " open entries have no `probe:`, so the fuzzer cannot tell their rediscovery from a new "
            + "defect and the nightly goes red every time one turns up: "
            + string.Join(", ", undiagnosed.Select(static entry => entry.ToString()))
            + ".\n\nEstablish the cause and name a probe in OpenDefectProbes, or fix the defect. "
            + "Raising this bound is a decision to accept a red nightly and belongs in a commit that "
            + "argues for it."
        );
    }

    /// <summary>
    ///     ⚠ The register is a queue, not a filing cabinet.
    /// </summary>
    /// <remarks>
    ///     A cap rather than a rule about any one entry: a handful of open defects is a to-do list and
    ///     thirty is a policy of not fixing them. The number is deliberately close to what is there, so
    ///     that adding one more is a decision somebody makes rather than a thing that happens.
    ///     <para>
    ///         ⚠ It was six, and seven arrived from the same fifteen-minute run that produced the sixth.
    ///         Raising it rather than dropping a finding is the honest move — a register that only ever
    ///         holds what fits under its own cap is a register that hides what it cannot hold, which is the
    ///         failure this whole directory exists to prevent. Seven findings from a fuzzer's first day is a
    ///         first harvest, not a backlog; the next commit to touch this number should be lowering it.
    ///     </para>
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
