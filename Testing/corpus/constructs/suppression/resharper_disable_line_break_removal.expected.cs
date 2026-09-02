// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
// ⚠ The same shape as its sibling, because the pair is only separable on a file that carries both
// directions at once. `disable_line_break_removal` keeps the three-blank run, the blank above the
// closing brace and the author's break after `=>` — and still breaks `void N() {` off its statement
// and still inserts the blank between `N` and `Q`, which is what `disable_line_break_changes`
// does not do.

class C {
    int _other;


    void N() {
        var y = 2;
    }

    int Q() => 3;
}
