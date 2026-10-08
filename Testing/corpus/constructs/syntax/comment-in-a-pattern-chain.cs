// A comment interrupting a pattern chain (#584): every `or` stays on one column, the comment's, under
// `=>`, `return`, `if`, `while` and a switch arm's pattern, a `//` or a `/* */`, before the first `or` or
// a later one, and after a trailing `//`. The author's break after a comment is no point of the chain's
// group, and the chain's frame paid a level of its own for it.
namespace P;

public class C {
    bool A(object o) =>
        o is int
            or long
            // a comment
            or string;

    bool B(object o) {
        return o is int
            or long
            /* block */
            or string;
    }

    bool D(object o) =>
        o is int
            // first
            or long
            or string;

    bool F(object o) {
        if (o is int
            // c
            or long) {
            return true;
        }

        while (o is int
            // c
            or long) { }

        var x = o switch {
            int
                // c
                or long => 1,
            _ => 0
        };
        return false;
    }

    bool G(object o) =>
        o is int
            or long // trailing
            or string;
}
