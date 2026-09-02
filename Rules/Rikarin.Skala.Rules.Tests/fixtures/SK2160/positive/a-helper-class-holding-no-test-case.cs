using System;
using Xunit;

// ⚠ The boundary #303's option (1) draws, pinned from the reported side. A class is test code when
// it holds a test case — that is xUnit's own discovery rule and it is decidable from attributes
// alone. It is *not* test code merely for living beside one, referencing xUnit, or being named
// after tests: recognising that would need the compilation's references, and it would exclude a
// repository's own test-helper library too.
//
// So this stays reported, and `negative/an-xunit-fixture-helper.cs` — the identical loop, in a
// class that holds a [Fact] — does not. The two files are the whole of the decision.
public static class DeadlineHelpers {
    public static void Settle(Func<bool> until) {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < deadline && !until()) { }
    }
}

public sealed class UsesTheHelper {
    [Fact]
    public void Runs() {
        DeadlineHelpers.Settle(static () => true);
        Assert.True(true);
    }
}
