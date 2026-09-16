namespace Constructs.Breaks;

// SK-DIV-0101 (issue #369). A body that opens with a parenthesis or tuple the author broke right
// after — after `=>`, after `=`, after a lambda's `=>`, after `return` — puts the `(` at the owner's
// own indent and its contents one level in, like an opening brace. The boundary is the operand of a
// binary expression and the receiver of a call chain broken at a dot, which keep the continuation.
public class ChoppedParenthesisBody {
    object Tuple() =>
        (
            1, 2);

    object TupleOnTheArrowLine() => (
        1, 2);

    object Parenthesised() =>
        (
            a + b);

    object Receiver() =>
        (
            first, second).ToString();

    object Indexed() =>
        (
            a)[0];

    object Switched() =>
        (
            a, b) switch {
            _ => 1
        };

    object Ternary() =>
        (
            a) ? b : c;

    object Nested() =>
        (
            (
                1, 2), 3);

    object Closer() =>
        (
            a
        );

    object Property =>
        (
            first, second);

    int this[int i] =>
        (
            i, i).Item1;

    int Accessor {
        get =>
            (
                1, 2).Item1;
    }

    // Kept on the continuation: a binary operand, a cast, a unary operator, and a `((` whose outer
    // parenthesis is not broken after. (A chain the author broke at a dot keeps it too, and is not
    // here because the chain's own level after such a receiver is a separate open divergence.)
    object BinaryOperand() =>
        (
            a) + b;

    object Cast() =>
        (int)(
            x);

    object Negated() =>
        !(
            a);

    object DoubleParenthesis() =>
        ((
            1, 2), 3);

    void Statements() {
        var declared =
            (
                1, 2);
        var onTheLine = (
            1, 2);
        object assigned;
        assigned =
            (
                1, 2);
        System.Func<object> lambda = () =>
            (
                1, 2);
        System.Func<object> lambdaOnTheLine = () => (
            1, 2);
        object Local() =>
            (
                1, 2);
    }

    object Returned() {
        return
            (
                1, 2);
    }

    object ReturnedOnTheLine() {
        return (
            1, 2);
    }

    object a, b, c, x, first, second;
}
