// resharper_csharp_blank_lines_around_single_line_property = 0.
//
// ⚠ THE CLAIM THIS FILE USED TO CARRY IS REFUTED. It said `public int X => 1;` is the single-line
// property this key governs, and that "at 2 the oracle puts two blank lines around each of these
// two". Re-measured 2026-08-30 against `jb cleanupcode` 2025.2.6, one key at a time, at 0, 1, 2 and
// 3, on input written both tight and with blank runs already in it: the oracle puts none, in every
// direction. `blank_lines_around_property` does not reach an expression-bodied property either.
//
// What the key does govern is an ACCESSOR-LIST property that is on one line —
// `public int X { get => 1; }` — where it is decisive at 2. `{ get; set; }` is the *auto*-property
// key's, confirmed on the same run. So the file kept the wrong shape twice: the accessor-list form
// first, and then this one.
//
// ⚠ And the accessor-list form cannot be the fixture either, for the reason the old note got right:
// under this export `keep_existing_declaration_block_arrangement = false` expands it onto three
// lines, and a property that is not on one line is not this key's. The key is masked at the export's
// own values and the row reads UNEXERCISED — SK-DIV-0092. The shape stays as it is so that the
// measurement above is the thing the next sweep re-asks.
class C {
    public int X => 1;
    public int Y => 2;
}
