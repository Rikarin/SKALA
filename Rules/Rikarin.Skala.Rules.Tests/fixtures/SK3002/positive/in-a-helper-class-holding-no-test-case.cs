using System.IO;
using System.Threading.Tasks;
using Xunit;

// ⚠ [#319], as a refusal rather than a fix, and this is the boundary the issue asked to move.
// `Waits` is the shape #319 describes exactly: a helper every test funnels through, holding the
// blocking call, carrying no attribute of its own. It stays reported.
//
// #319's proposed remedy was "a non-public helper declared in a test project", and neither half
// survives: the real helper it was written for — `RuleFixtures.Analyze` — is `public static` on a
// `public static class`, so accessibility decides nothing; and "declared in a test project" is the
// compilation-references question #303 examined and refused, pinned by
// `SK2160/positive/a-helper-class-holding-no-test-case.cs`. ⚠ Refused for a measured reason: the
// fixture harness hands every fixture the test host's whole assembly closure, so "references xunit"
// is true of every fixture here, and wiring it in turned 31 positive fixtures across six rules
// silent in one run.
//
// Reaching this needs the call graph #319 rules out. The finding is correct about the mechanism and
// baselining it is the honest outcome.

public static class Settling {
    public static async Task<int> WaitsAsync(string path) {
        var text = File.ReadAllTextAsync(path).Result;
        await Task.Yield();
        return text.Length;
    }
}

public sealed class SettlingTests {
    [Fact]
    public async Task Runs() {
        Assert.True(await Settling.WaitsAsync("x") >= 0);
    }
}
