namespace Constructs.Breaks;

// SK-DIV-0111 (issue #370). A `for` header chops under `chop_if_long` when it is multi-line, and
// "multi-line" is decided after the constructs inside it have re-joined what they re-join: a break
// before a declarator's comma, after a binary operator or inside an invocation's parentheses is
// joined and leaves the header on one line; a break after a declarator's comma, before a binary
// operator or after the `(` is kept and chops it. Before this file Skala read the source alone and
// chopped every one of them.
public class ForHeaderSurvivingBreak {
    void Joined(int n) {
        for (int i = 0
            , j = 1; i < n; i++) { }

        for (int i = 0; i <
            n; i++) { }

        for (int i = F(
            1); i < n; i++) { }

        for (int i = 0, j = 1
            , k = 2; i < n; i++) { }

        for (int i = 0
            , j = 1
            ; i < n; i++) { }
    }

    void Kept(int n) {
        for (int i = 0,
            j = 1; i < n; i++) { }

        for (int i = 0, j = 1; i < n
            && j > 0; i++) { }

        for (
            int i = 0; i < n; i++) { }

        for (int i = 0, j = 1;
            i < n; i++) { }
    }

    int F(int a) => a;
}
