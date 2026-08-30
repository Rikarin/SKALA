// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
using System;

// resharper_csharp_blank_lines_around_auto_property = 1.
//
// ⚠ The accessor attributes are load-bearing and not decoration. Measured: this key governs a
// *multi-line* auto property, and `{ get; set; }` on one line is a single-line auto property that
// falls to `blank_lines_around_single_line_auto_property`. Under this export
// `keep_existing_declaration_block_arrangement = false` collapses a bare `get; set;` written over
// three lines back onto one, so an attribute on an accessor is what keeps an auto property
// multi-line for this key to reach at all.
//
// ⚠ The file used to be written as two bare auto properties laid out over four lines each. The
// oracle collapsed them and Skala did not, so the file disagreed with the oracle at the sweep's
// baseline and this key's row attributed nothing — while the key itself was never in question.
class C {
    public int X {
        [Obsolete]
        get;
        set;
    }


    public int Y {
        [Obsolete]
        get;
        set;
    }
}
