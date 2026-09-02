// ⚠ The deliberately cut half. The flow state here is NotNull and the `!` really is redundant,
// and the rule still declines: removing one suppression can make another necessary, and a `!` can
// be suppressing a nested nullability warning the operand's own flow state says nothing about.
// See the rule's remarks; this fixture is the record of the cut, not an oversight.
namespace Fixtures {
    sealed class Padder {
        public int Measure(string text) => text!.Length;
    }
}
