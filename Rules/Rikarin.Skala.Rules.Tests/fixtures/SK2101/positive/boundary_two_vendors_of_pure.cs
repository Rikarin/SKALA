namespace JetBrains.Annotations {
    [System.AttributeUsage(System.AttributeTargets.Method)]
    sealed class PureAttribute : System.Attribute { }
}

namespace Acme {
    // ⚠ The boundary fixture. Two `[Pure]` attributes on one void method look like a repeated
    // attribute and are not one: they are different classes, so SK2103 declines. It is also one
    // mistake, not two, so SK2101 reports exactly once. Total across SK2100-SK2103: one finding.
    static class Validation {
        [System.Diagnostics.Contracts.Pure]
        [JetBrains.Annotations.Pure]
        public static void Check(string input) {
            _ = input.Length;
        }
    }
}
