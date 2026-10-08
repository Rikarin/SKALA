// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// ⚠ An enum member is a field to `blank_lines_around_field` and `blank_lines_around_single_line_field`,
// and SK-DIV-0172's glued-comment rule reaches it as it reaches a field: a member with a comment or an
// attribute line directly above it is multi-line for the gap above, and so is one with a comment
// glued under it before the `}`. A trailing comma between the member and such a comment breaks the glue.
// Issue #497.

enum Plain {
    Alpha,

    // own
    Beta,
    Gamma
}

enum Block {
    Alpha,

    /* own */
    Beta,
    Gamma
}

enum AfterATrailingComment {
    Alpha,
    BetaMemberNumberTwo, // trailing

    // own line
    Gamma
}

enum TwoLines {
    Xx,
    Alpha,

    // one
    // two
    Beta,
    Gamma
}

enum Attributed {
    Alpha,

    [System.Obsolete]
    Beta,
    Gamma
}

enum Valued {
    Alpha = 1,

    // own
    Beta = 2
}

enum GluedUnderTheLast {
    Alpha,

    Beta
    // last
}

enum CommaBeforeTheLastComment {
    Alpha,
    Beta,
    // last
}

enum FirstMember {
    // first
    Alpha,
    Beta
}

enum OnTheMembersLine {
    Alpha, // own
    Beta,
    Gamma
}
