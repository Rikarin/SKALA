using System.Threading.Tasks;

namespace NUnit.Framework {
    public sealed class TestAttribute : System.Attribute;

    public sealed class SetUpAttribute : System.Attribute;
}

namespace Contoso.Design {
    // A test is named after what it asserts. A suite that renamed every asynchronous test would be
    // worse to read, not better, and the convention exists for callers — a test has none. xUnit, NUnit
    // and MSTest are all recognised, and so are their setup and teardown hooks.
    public sealed class StoreTests {
        [NUnit.Framework.SetUp]
        public Task Prepare() => Task.CompletedTask;

        [NUnit.Framework.Test]
        public Task LoadReturnsTheStoredValue() => Task.CompletedTask;
    }
}
