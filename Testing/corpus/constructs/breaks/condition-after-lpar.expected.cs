// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-16
namespace Constructs.Breaks;

// SK-DIV-0102 (issue #369). A statement condition broken right after its `(` has nothing on the
// column align_multiline_statement_conditions would align to, and lands one continuation level in
// from the statement instead. `if (` is four columns wide, so the two answers coincide there; every
// other keyword tells them apart. A condition broken inside — after the first operand — still
// aligns to the parenthesis.
public class ConditionAfterLpar {
    void M(bool c, int n, int[] xs) {
        while (
            c) {
            n++;
        }

        while (
            c
            && n > 0) {
            n--;
        }

        switch (
            n) {
            case 1:
                break;
        }

        if (
            c) {
            n++;
        }

        foreach (
            var x in xs) {
            n++;
        }

        for (
            var i = 0;
            i < n;
            i++) {
            n--;
        }

        using (
            var d = default(System.IDisposable)) { }

        lock (
            xs) { }

        do {
            n--;
        } while (
            c);

        // Broken inside the condition: aligned to the parenthesis, as before.
        while (c
               && n > 0) {
            n--;
        }

        switch (n
                + 1) {
            case 1:
                break;
        }

        foreach (var x in
                 xs) {
            n++;
        }
    }
}
