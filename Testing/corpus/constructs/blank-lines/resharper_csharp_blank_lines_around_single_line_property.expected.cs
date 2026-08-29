// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-29
// resharper_csharp_blank_lines_around_single_line_property = 0.
//
// ⚠ Expression-bodied properties, and that is what the key needs. Measured: `public int X => 1;` is
// the single-line property this key governs — at 2 the oracle puts two blank lines around each of
// these two — while `public int X { get => 1; }` is not one for long, because under this export
// `keep_existing_declaration_block_arrangement = false` expands an accessor-list property onto three
// lines and `blank_lines_around_property` governs what is left.
//
// ⚠ The file used to be written as the `{ get => 1; }` form. The oracle expanded it and Skala did
// not, so the file disagreed with the oracle at the sweep's baseline and this key's row attributed
// nothing — while the key itself was never in question, and was in fact being asked about a shape it
// does not govern.

class C {
    public int X => 1;
    public int Y => 2;
}
