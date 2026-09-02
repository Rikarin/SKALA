// ⚠ [#321], side two of two, and this file is literally SK0240's fix output.
// SK0240/negative/default_legitimises_a_nonexhaustive_enum_switch.cs is this switch with
// `default: break;` still in it; deleting that section is exactly the edit SK0240 used to pack, and
// what it leaves behind is this — a switch over `DocKind` omitting `Fill` and `IfBroken`, which
// SK2009 reports.
//
// So the finding SK0240's fix would have created is asserted here rather than argued about. The
// two files are one test read from both ends: if SK0240's stand-down is ever removed, the negative
// half goes red; if SK2009 ever stops owning this shape, this half does.

enum DocKind {
    Text,
    Line,
    Concat,
    Fill,
    IfBroken
}

sealed class Writer {
    public int Width(DocKind kind) {
        switch (kind) {
            case DocKind.Text:
                return 1;
            case DocKind.Line:
                return 2;
            case DocKind.Concat:
                return 3;
        }

        return 0;
    }
}
