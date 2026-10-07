// analyzer-option: dotnet_code_quality.SK7004.threshold = 4
// ⚠ #397: four members against a threshold of four, written as seven declarations. A partial member
// is one member whose two halves are both in this type, so counting declarations put the type over
// the threshold with nothing added to it. A partial event's definition is spelled as a field-like
// event and is still a member here, not a field.
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
