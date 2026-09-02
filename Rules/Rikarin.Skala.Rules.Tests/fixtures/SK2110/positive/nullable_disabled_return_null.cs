// The same defect in a nullable-oblivious file, where CS8603 cannot be issued. This is the case
// the rule exists for, and it is why the rule does *not* withdraw when the context is disabled.
#nullable disable
namespace Fixtures {
    sealed class Receipt {
        public override string ToString() {
            return null;
        }
    }
}
