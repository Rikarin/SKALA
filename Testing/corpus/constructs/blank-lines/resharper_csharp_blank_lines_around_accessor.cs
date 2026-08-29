using System;

// resharper_csharp_blank_lines_around_accessor = 0.
//
// ⚠ The accessors are two statements each, and that is what the key needs rather than decoration.
// Measured: this key governs a *multi-line* accessor only — a `get { return 1; }` is a single-line
// accessor and falls to `blank_lines_around_single_line_accessor`, and under this export
// `keep_existing_declaration_block_arrangement = false` collapses a one-statement accessor onto one
// line, so a one-statement body cannot stay multi-line for this key to reach.
//
// ⚠ The file used to be written with one-statement accessors laid out over three lines each. The
// oracle collapsed them and Skala did not, so the file disagreed with the oracle at the sweep's
// baseline and this key's row attributed nothing — while the key itself was never in question.
class C {
    int _a;

    public int X {
        get {
            Console.WriteLine("a");
            return 1;
        }
        set {
            Console.WriteLine("b");
            _a = value;
        }
    }
}
