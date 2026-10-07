// ⚠ #397, and the one partial shape where "never assigned" is still true: `get => field` with nothing
// writing it is `default` forever. It is declined anyway, deliberately. The same member written as one
// declaration — `public int Count { get => field; }` — has an accessor body, which this rule has never
// read, so reporting only the partial spelling would make the finding depend on how the member is
// split rather than on what it holds.
sealed partial class Loader {
    public partial int Count { get; }

    public partial int Count {
        get => field;
    }

    public bool IsEmpty => Count == 0;
}
