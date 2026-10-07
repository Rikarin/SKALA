// analyzer-option: dotnet_code_quality.SK7004.threshold = 3
// ⚠ #397: the other half of `partial-members-counted-once.cs`. A partial event's definition is spelled
// as a field-like event, and a field-like event is counted as a field — but this one is the event's
// only counted half, because the accessor half is the implementation. Counted as a field, the type
// measured three members against a threshold of three and was never reported; it has four.
using System;

public sealed partial class Counters {
    EventHandler? changed;

    public partial int Count { get; }

    public partial int Count => 4;

    public partial int this[int index] { get; }

    public partial int this[int index] => index;

    public partial event EventHandler? Changed;

    public partial event EventHandler? Changed {
        add => changed += value;
        remove => changed -= value;
    }

    public void Raise() => changed?.Invoke(this, EventArgs.Empty);
}
