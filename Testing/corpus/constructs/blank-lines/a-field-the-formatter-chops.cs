class C {
    // ⚠ The long one is single-line in the source and several lines in the output, so it takes
    // blank_lines_around_field rather than blank_lines_around_single_line_field — and reading that
    // off the input rather than the output makes the formatter non-idempotent.
    int Short;
    static readonly string Long = string.Join(", ", "a first piece", "a second piece", "a third piece", "a fourth piece", "a fifth");
    int AlsoShort;
}
