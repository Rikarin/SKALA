using System;
using System.Threading.Tasks;

public sealed class FactAttribute : Attribute { }

public sealed class PanelTests {
    [Fact]
    public async void Throws() {
        await Task.Yield();
        throw new InvalidOperationException("expected");
    }
}
