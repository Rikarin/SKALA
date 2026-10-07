using System;
using System.Threading.Tasks;

// #400: `[Fact]` is on the definition, and `async` can only be on the implementation.
public sealed class FactAttribute : Attribute { }

public sealed partial class PanelTests {
    [Fact]
    public partial void Throws();

    public async partial void Throws() {
        await Task.Yield();
        throw new InvalidOperationException("expected");
    }
}
