// ⚠ Not a constant, so not this rule. Whether `name` can be null here is a flow question, and
// in the contexts this rule covers there is no flow state to read.
namespace Fixtures {
    sealed class Person {
        readonly string? name;

        public Person(string? name) => this.name = name;

        public override string? ToString() => name;
    }
}
