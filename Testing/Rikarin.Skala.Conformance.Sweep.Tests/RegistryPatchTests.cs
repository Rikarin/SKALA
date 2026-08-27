namespace Rikarin.Skala.Conformance.Sweep.Tests;

/// <summary>
///     The patcher that writes verified defaults back into <c>options.json</c>.
/// </summary>
/// <remarks>
///     ⚠ It edits a tracked file that is reviewed in its diff, and it edits it as text rather than by
///     reserialising, because a round trip through a different serialiser reformats all 520 entries and
///     buries five real changes in a twelve-thousand-line diff. Text editing is only safe if it changes
///     exactly the lines it means to, which is what these pin.
/// </remarks>
public sealed class RegistryPatchTests {
    const string Registry = """
                            {
                              "options": [
                                {
                                  "key": "resharper_alpha",
                                  "type": "bool",
                                  "default": "false",
                                  "defaultSource": "template",
                                  "tier": "A"
                                },
                                {
                                  "key": "resharper_beta",
                                  "type": "bool",
                                  "default": "true",
                                  "defaultSource": "unknown",
                                  "tier": "A"
                                }
                              ]
                            }
                            """;

    [Fact]
    public void AVerifiedDefault_ChangesOnlyItsOwnEntry() {
        var path = Write(Registry);
        RegistryPatch.Apply(path, RegistryPatch.Plan(path, [Verified("resharper_beta", "false")]));

        var patched = File.ReadAllText(path);
        Assert.Contains(
            "\"key\": \"resharper_beta\",\n      \"type\": \"bool\",\n      \"default\": \"false\","
            + "\n      \"defaultSource\": \"oracle-probe\",",
            Normalise(patched),
            StringComparison.Ordinal
        );

        // ⚠ The neighbouring entry is untouched, including its `default` — the field the patcher
        // searches for by name. Anchoring on the "key" line and stopping at the next one is what
        // keeps the search from wandering into it.
        Assert.Contains(
            "\"key\": \"resharper_alpha\",\n      \"type\": \"bool\",\n      \"default\": \"false\","
            + "\n      \"defaultSource\": \"template\",",
            Normalise(patched),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void OnlyVerifiedVerdicts_AreWritten() {
        var path = Write(Registry);
        var changes = RegistryPatch.Plan(
            path,
            [
                new DerivedDefault("resharper_alpha", null, DefaultsVerdict.Insensitive, false, ""),
                new DerivedDefault("resharper_beta", "true", DefaultsVerdict.Ambiguous, false, "")
            ]
        );

        Assert.Empty(changes);
    }

    /// <summary>An entry already carrying the same verified default is not rewritten.</summary>
    /// <remarks>
    ///     ⚠ Otherwise every nightly run produces a diff that says nothing, and a file whose diff always
    ///     has content is a file whose diff nobody reads.
    /// </remarks>
    [Fact]
    public void AnEntryAlreadyVerifiedAtTheSameValue_IsNotAChange() {
        var path = Write(Registry);
        RegistryPatch.Apply(path, RegistryPatch.Plan(path, [Verified("resharper_beta", "false")]));

        Assert.Empty(RegistryPatch.Plan(path, [Verified("resharper_beta", "false")]));
    }

    [Fact]
    public void ARecordedValueTheSweepContradicts_IsReportedAsSuch() {
        var path = Write(Registry);
        var changes = RegistryPatch.Plan(
            path,
            [Verified("resharper_beta", "false"), Verified("resharper_alpha", "false")]
        );

        Assert.Equal(2, changes.Count);
        Assert.True(changes.Single(static c => c.Key == "resharper_beta").ChangesValue);

        // `resharper_alpha` already records "false"; only its source is unproven, so the sweep
        // confirms it rather than correcting it.
        Assert.False(changes.Single(static c => c.Key == "resharper_alpha").ChangesValue);
    }

    static DerivedDefault Verified(string key, string value) =>
        new(key, value, DefaultsVerdict.Verified, false, "constructs/sample.cs");

    static string Normalise(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    static string Write(string content) {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        File.WriteAllText(path, content);
        return path;
    }
}
