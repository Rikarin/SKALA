// The apostrophes before the join are odd, so the join is inside a `'…'` literal. A space there
// would change the value the statement compares against rather than repair how it parses.
public sealed class Queries {
    public string Failed() =>
        "select * from logs where message = 'timed out at the end"
        + "ORDER'";
}
