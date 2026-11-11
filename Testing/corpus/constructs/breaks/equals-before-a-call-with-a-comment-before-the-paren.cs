// An `=` before a call or creation whose callee ends in a block comment before its `(` (Nightly run 42362875104,
// seed 9715006458001699096): the `(` lands where the comment pushes it, `/* f */(`, and the `=` breaks once that
// column is past the margin, exactly as it does for a callee as wide as the two together. Each comment row sits on
// the side of the margin a callee measured without its comment would get wrong; the last rows of each group are
// the same columns with no comment.
class EqualsBeforeACallWithACommentBeforeTheParen {
    void M1() {
        vvvvvv = new Fooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo /* f */ (firstArgument, secondArgument.Value);
    }

    void M2() {
        vvvvvv = new Fooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo /* f */ (firstArgument, secondArgument.Value);
    }

    void M3() {
        vvvvvv = new Foooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo /* f */ (firstArgument, secondArgument.Value);
    }

    void M4() {
        vvvvvv = new Fooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo /* f */ (firstArgument, secondArgument.Value);
    }

    void M5() {
        vvvvvv = new Fooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo /* f */ (firstArgument, secondArgument.Value);
    }

    void M6() {
        vvvvvv = new Fooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo(firstArgument, secondArgument.Value);
    }

    void M7() {
        vvvvvv = new Foooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo(firstArgument, secondArgument.Value);
    }

    void M8() {
        vvvvvv = new Fooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo /* f */ (aaaa);
    }

    void M9() {
        vvvvvv = new Fooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo /* f */ (aaaa);
    }

    void M10() {
        vvvvvv = new Foooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo /* f */ (aaaa);
    }

    void M11() {
        vvvvvv = new Fooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo /* f */ (aaaa);
    }

    void M12() {
        vvvvvv = new Fooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo /* f */ (aaaa);
    }

    void M13() {
        vvvvvv = new Fooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo(aaaa);
    }

    void M14() {
        vvvvvv = new Foooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo(aaaa);
    }

    void M15() {
        vvvvvv = Computeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee /* f */ (firstArgument, secondArgument.Value);
    }

    void M16() {
        vvvvvv = Computeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee /* f */ (firstArgument, secondArgument.Value);
    }

    void M17() {
        vvvvvv = Computeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee /* f */ (firstArgument, secondArgument.Value);
    }

    void M18() {
        vvvvvv = Computeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee /* f */ (firstArgument, secondArgument.Value);
    }

    void M19() {
        vvvvvv = Computeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee /* f */ (firstArgument, secondArgument.Value);
    }

    void M20() {
        vvvvvv = Computeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee(firstArgument, secondArgument.Value);
    }

    void M21() {
        vvvvvv = Computeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee(firstArgument, secondArgument.Value);
    }

    void M22() {
        vvvvvv = Computeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee /* f */ (aaaa);
    }

    void M23() {
        vvvvvv = Computeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee /* f */ (aaaa);
    }

    void M24() {
        vvvvvv = Computeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee /* f */ (aaaa);
    }

    void M25() {
        vvvvvv = Computeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee /* f */ (aaaa);
    }

    void M26() {
        vvvvvv = Computeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee /* f */ (aaaa);
    }

    void M27() {
        vvvvvv = Computeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee(aaaa);
    }

    void M28() {
        vvvvvv = Computeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee(aaaa);
    }
}
