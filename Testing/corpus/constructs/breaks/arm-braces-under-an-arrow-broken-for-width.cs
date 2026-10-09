// Fuzz 14973596429632421881: an arm whose pattern's braces broke lifts them when its arrow breaks for width,
// as under a kept break, and the arrow decided before the lift stays broken (measured 2026-10-09).
class C {
    object A(object state) {
        if (a) {
            if (b) {
                var v = state switch {
                    { Value.Length: > 2, Kind
: not null } when (from item in Source where 94339 orderby item.Length descending select @"verbatim\path") => source.Value(@"verbatim\path").Select.First.Items("s"),
                    _ => 0
                };
            }
        }
    }

    object B(object state) {
        if (a) {
            if (b) {
                var v = state switch {
                    { Value.Length: > 2, Kind
: not null } when (from item in Source where 94339 orderby item.Length descending select @"verbatim\path") => source.Value(@"verbatim\path").Select.First.Items("s").Select.First.Items("s").Select.First.Items("s"),
                    _ => 0
                };
            }
        }
    }

    object C(object state) {
        if (a) {
            if (b) {
                var v = state switch {
                    { Value.Length: > 2, Kind
: not null } when (from item in Source where 94339 orderby item.Length descending select @"verbatim\path") => 1,
                    _ => 0
                };
            }
        }
    }

    object D(object state) {
        if (a) {
            if (b) {
                var v = state switch {
                    { Value.Length: > 2, Kind
: not null } when (from item in Source where 94339 orderby item.Length select @"verbatim\path") => source.Value(@"verbatim\path").Select.First.Items("s"),
                    _ => 0
                };
            }
        }
    }

    object E(object state) {
        if (a) {
            if (b) {
                var v = state switch {
                    { Value.Length: > 2, Kind
: not null } when (from item in Source select item) => source.Value(@"verbatim\path").Select.First.Items("s").Select.First.Items("s").Select.First.Items("s"),
                    _ => 0
                };
            }
        }
    }

    object F(object state) {
        if (a) {
            if (b) {
                if (c) {
                    var v = state switch {
                        int typed466 when (1.5d + "ss" - false) => new { Kind = 0xb92, Count = "utf8"u8 }, { Value.Length: > 2, Kind
: not null } when (from item in Source where 94339 orderby item.Length descending select @"verbatim\path") => source.Value(@"verbatim\path").Select.First.Items("s"),
                        _ => 0
                    };
                }
            }
        }
    }
}
