namespace JetBrains.Annotations {
    [System.AttributeUsage(System.AttributeTargets.Method)]
    sealed class PureAttribute : System.Attribute { }
}

namespace Acme {
    using JetBrains.Annotations;

    // JetBrains' `[Pure]` means the return value must be used. There is no return value.
    static class Validation {
        [Pure]
        public static void Check(string input) {
            _ = input.Length;
        }
    }
}
