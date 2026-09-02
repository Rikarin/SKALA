// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
using System;

class C {
    void M(bool b) {
        if (b) {
            M(b);
        }

        M(b);
    }

    // ⚠ `blank_lines_after_block_statements` is the mirror of
    // `blank_lines_before_block_statements`, and it was not asking the mirror question. It tested
    // "the previous token is a `}` and it ends a statement in a list", which is a fact about the
    // last character rather than about the statement, and it is wrong in both directions. The five
    // methods below are the five shapes that showed it; the `if` above is the one that always
    // agreed, and it is why the defect survived.
    //
    // ⚠ All five were measured with `blank_lines_after_block_statements = 1` and *both*
    // local-method keys forced to 0, so that no other key could have been the one answering. At
    // this file's own configuration `blank_lines_around_local_method = 1`, which is why
    // `MultiLineLocalFunction` still has a blank after it here and `SingleLineLocalFunction` does
    // not — that blank is the local-method key's, not this one's.

    // Over-fired: a bare block ends in `}` and is not a statement *with* a block. The oracle writes
    // no blank after it, which is the same answer `HasChildBlock` already gave the `before`
    // direction.
    void BareBlock() {
        {
            Console.WriteLine();
        }
        Console.WriteLine();
    }

    // Over-fired: a local function is not a block statement either, at either width. This is the
    // one the defect was reported as — a blank appearing after `void Inner() { }` that the oracle
    // does not write.
    void SingleLineLocalFunction() {
        void Inner() { }
        Console.WriteLine();
    }

    void MultiLineLocalFunction() {
        void Inner() {
            Console.WriteLine();
        }

        Console.WriteLine();
    }

    // Under-fired: both of these *are* block statements and both end in a `;`, so the old
    // brace-shaped test could not see either. The oracle blanks after both.
    void DoWhile(int f) {
        do {
            f--;
        } while (f > 0);

        Console.WriteLine();
    }

    void IfBracedElseBraceless(int f) {
        if (f > 0) {
            Console.WriteLine();
        } else
            Console.WriteLine();

        Console.WriteLine();
    }

    // ⚠ The negative controls, and they are the reason the rule is `HasChildBlock` rather than
    // "owns a brace anywhere". A braceless `while` has no child block; a lambda's block is two
    // levels down and belongs to the lambda, not to the declaration statement holding it.
    void BracelessWhile(int f) {
        while (f > 0)
            f--;
        Console.WriteLine();
    }

    // ⚠ Two statements in the lambda's body, not one. With one the oracle joins the whole thing
    // onto `Action a = () => { … };` — a separate divergence about joining a simple anonymous
    // method — and the method would have measured that instead of this.
    void LambdaWithABlock() {
        Action a = () => {
            Console.WriteLine();
            Console.WriteLine();
        };
        Console.WriteLine();
    }
}
