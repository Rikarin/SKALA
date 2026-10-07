using System.IO;
using System.Threading.Tasks;

// #400: `[Fact]` is written on the definition and the blocking call is in the implementation's body.
// The attributes of a partial method belong to both halves, so this is a test method.
public sealed class FactAttribute : System.Attribute { }

public sealed partial class LoaderTests {
    [Fact]
    public partial void Loads();

    public partial void Loads() {
        var text = File.ReadAllTextAsync("x").Result;
        System.Console.WriteLine(text.Length);
    }
}
