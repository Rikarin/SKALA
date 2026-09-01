namespace Acme.Annotations {
    [System.AttributeUsage(System.AttributeTargets.Method)]
    sealed class PureAttribute : System.Attribute { }
}

namespace Acme {
    using Acme.Annotations;

    // ⚠ A third-party attribute that happens to share the simple name is under no obligation to
    // mean what either accepted one means. Matching on the short name is how an annotation rule
    // acquires false positives on code it has never seen.
    static class Validation {
        [Pure]
        public static void Check(string input) {
            _ = input.Length;
        }
    }
}
