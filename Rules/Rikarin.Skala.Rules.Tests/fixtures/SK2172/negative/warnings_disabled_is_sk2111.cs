// ⚠ The other half of SK2111's subject: with nullable warnings off at that position there was never
// a warning for the `!` to suppress, whatever it stands on. The shape is this rule's exactly and it
// is declined, so no `!` in the catalogue can be reported twice.
#nullable disable
class C {
    void M(object value) {
        if (value! is string) {
            Handle();
        }
    }

    static void Handle() { }
}
