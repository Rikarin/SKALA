// ⚠ [#321], side one of two. `default: break;` on a switch that omits `Fill` and `IfBroken` is not
// dead control flow: it is the author's written statement that the rest of the enum is deliberately
// ignored, and it is the whole of what keeps SK2009 quiet. Deleting it clears this finding and
// immediately produces `SK2009: switch over `DocKind` omits `Fill`, `IfBroken`` at the same switch —
// a fix that hands the author a finding they did not have.
//
// Side two is SK2009/positive/sk0240_fix_output_omits_members.cs, which is this file with the
// section already deleted, and it fires. The pair is what makes the loop visible; a test that runs
// one rule at a time cannot see it.

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

            default:
                break;
        }

        return 0;
    }
}
