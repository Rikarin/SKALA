// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
// ⚠ Every break the formatter can add and every one it can remove, on one file.
//
// Removals: the three-blank run is past `keep_blank_lines_in_declarations`, the blank before `}` is
// what `remove_blank_lines_near_braces_in_code` deletes, and the break after `=>` is the one
// `keep_existing_expr_member_arrangement` re-joins. Additions: the brace rules break `void M() {`
// off its statement, and `blank_lines_around_invocable` inserts one between `M` and `P`.
class C {
    int _field;



    void M() { var x = 1;

    }
    int P() =>
        1;
}
