// #576: an arm's `when` condition with no break point of its own moves below the `when` when it fits there.
class C576b {
    object A(object owner) =>
        owner switch {
            DateTime { P25: not null } when Materialise<List<bool>, IReadOnlyDictionary<(int? First, TimeSpan Second), int>>() => 1,
            _ => 0
        };

    object B(object owner) =>
        owner switch {
            DateTime { P25: not null } when MaterialiseSomethingLonger(owner, owner, owner, owner, owner) => (from item in items where "s" select item),
            _ => 0
        };

    object C(object owner) =>
        owner switch {
            DateTime { P25: not null } when MaterialiseSomethingLongerStill(owner, owner, owner, owner, owner, owner) => (from item in items where "s" select item),
            _ => 0
        };

    object D(object owner) =>
        owner switch {
            DateTime { P25: not null } when SomeVeryLongIdentifierForTheConditionThatGoesOnAndOnAndOnAndOnAndOnAndOnAndOn => 1,
            _ => 0
        };

    object E(object owner) =>
        owner switch {
            DateTime { P25: not null } when Materialise<List<bool>, IReadOnlyDictionary<(int? First, TimeSpan Second), (int? First, TimeSpan Second)>>() => 1,
            _ => 0
        };
}
