using Xunit;

// The shape docs/plan/08 records `SK8002` breaking on. The `0` is an implicit constant conversion to
// the enum, so `Assert.NotEqual(0, flags & Member)` cannot infer `T` — and the reason this rule never
// reaches it is that the swapped call is what would be written, not what is.
public sealed class FlagTests {
    [System.Flags]
    public enum Access {
        None = 0,
        Read = 1
    }

    [Fact]
    public void Reads() {
        var flags = Access.Read;
        Assert.NotEqual(Access.None, flags & Access.Read);
    }
}
