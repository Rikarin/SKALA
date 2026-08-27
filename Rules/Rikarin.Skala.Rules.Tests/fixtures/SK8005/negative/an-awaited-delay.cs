using System.Threading.Tasks;

public sealed class FactAttribute : System.Attribute { }

public sealed class BusTests {
    [Fact]
    public async Task Delivers() {
        await Task.Delay(200);
    }
}
