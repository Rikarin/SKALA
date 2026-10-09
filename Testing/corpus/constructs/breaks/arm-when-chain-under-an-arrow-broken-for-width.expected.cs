// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-09
// Fuzz 18207060042734210187: an arm's `when` condition that broke lifts from the arm's pattern when the arrow
// breaks for width, as under a kept break: the operator chain two levels past the arm (measured 2026-10-09).

public sealed class T3 {
    public double A(out Guid? p250, Func<long, long> p251) {
        using (var u252 = value switch {
                   [_, .. var rest254] when (state is _) => this.Current,
                   not null when ("ss"
                           ?? "ssssssssssssssssssssssssssss"
                           + "sssssssssssssssssssssssssss"
                           - "ssssssssssssssssssssssss")
                       => source?.Value?.Length,
                   not null => (from item in items where false select "utf8"u8),
                   _ => (69086 ? "ss" : "ss")
               }) { }
    }

    object B(object state) {
        if (a) {
            if (b) {
                var v = state switch {
                    not null when ("ss"
                            ?? "ssssssssssssssssssssssssssss"
                            + "sssssssssssssssssssssssssss"
                            - "ssssssssssssssssssssssss")
                        => source?.Value?.Length,
                    string s when s.Length > 100000 && someVeryLongConditionName && anotherVeryLongConditionName
                        || yetAnotherOne => source.Value(@"verbatim\path").Select.First.Items("s"),
                    _ => 0
                };
            }
        }
    }
}
