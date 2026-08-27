using System.IO;
using System.Threading.Tasks;

public sealed class FactAttribute : System.Attribute { }

public sealed class LoaderTests {
    [Fact]
    public void Loads() {
        var text = File.ReadAllTextAsync("x").Result;
        System.Console.WriteLine(text.Length);
    }
}
