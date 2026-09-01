// The condition is an invocation; the assignment is inside a lambda the invocation happens to take.
using System.Linq;

class C {
    void M(int[] items) {
        var last = 0;
        if (items.Any(i => (last = i) > 0)) {
            Use(last);
        }
    }

    static void Use(int value) { }
}
