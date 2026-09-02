// A receiver mentioning a method type parameter has to move to `extension<T>(…)`, and the arity has
// to be reconciled across every member of the block. The class of `this IEnumerable<T>` helpers is
// the common case and it is exactly the one this rule does not attempt.
using System.Collections.Generic;

namespace Fixtures {
    static class GenericHelpers {
        public static bool HasAny<T>(this IEnumerable<T> source) {
            foreach (var unused in source) {
                return true;
            }

            return false;
        }

        public static int Count<T>(this IEnumerable<T> source) {
            var n = 0;
            foreach (var unused in source) {
                n++;
            }

            return n;
        }
    }
}
