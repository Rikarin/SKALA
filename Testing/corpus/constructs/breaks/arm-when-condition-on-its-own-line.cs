// #601: a switch arm's `when` condition the author put on a line of its own lifts the arm's braces as a kept
// arrow break does, and sits on the `} when` line's column, whether the arrow then breaks for width or not.
class C {
    object A(object state) {
        if (a) {
            if (b) {
                if (c) {
                    var v = state switch {
                        { Value.Length: > 2, Kind
: not null } when
(from item in Source where 94339 orderby item.Length descending select @"verbatim\path") => source.Value(@"verbatim\path").Select.First.Items("s"),
                        { Value.Length: > 2, Kind
: not null } when
(from item in Source where 94339 orderby item.Length descending select @"verbatim\pathhhhhhhhhhhhhhhh") => source.Value(@"verbatim\path").Select.First.Items("s"),
                        { Value.Length: > 2, Kind
: not null } when
Compute(alpha, beta, gamma) => source.Value(@"verbatim\path").Select.First.Items("ssssssssssssssssssssssssss"),
                        { Value.Length: > 2, Kind
: not null } when
someCondition => source.Value(@"verbatim\path").Select.First.Items("ssssssssssssssssssssssssssssssssssssssssssss"),
                        { Value.Length: > 2, Kind
: not null } when
(first ?? "ssssssssssssssssssssssssssssssssssssss" + "sssssssssssssssssssssssssssssssssss" + "ssssssssssss") => source.Value(@"verbatim\path").Select.First.Items("s"),
                        _ => 0
                    };
                }
            }
        }
    }
}
