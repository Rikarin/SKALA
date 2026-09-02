// A parameter read from a member body becomes a hidden field, and that is what primary
// constructors are for. Reporting it would be reporting C# 12.
namespace Fixtures {
    sealed class Greeter(string name) {
        public string Greet() => "Hello, " + name;

        public int Length => name.Length;
    }
}
