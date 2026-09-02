using System.IO;
using System.Threading.Tasks;
using Xunit;

// ⚠ [#319]. `Dispose` carries no attribute of its own, so the enclosing-method test walked straight
// past it and reported here — inside a class xUnit will run as a test class. The question is asked of
// the type now, which is #303's rule (`TestFrameworks.HoldsATestCase`, xUnit's own discovery rule),
// so every member of a class holding a test case is test code: the constructor, the teardown, a
// field initializer and this.

public sealed class LoaderTests : System.IDisposable {
    [Fact]
    public void Loads() {
        Assert.True(true);
    }

    public void Dispose() {
        var text = File.ReadAllTextAsync("x").Result;
        System.Console.WriteLine(text.Length);
    }
}
