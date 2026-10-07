// #397's exemption reads the other half; it does not excuse a member neither half documents. `Count`
// is reported — once, on its definition, which is the half a generator never writes.
/// <summary>Holds the counters.</summary>
internal sealed partial class Counters {
    internal partial int Count { get; }

    internal partial int Count => 4;
}
