using System;

public sealed class Slots {
    readonly string?[] slots = new string?[4];

    public string? Name {
        get => slots[0];
        set {
            // A `set` accessor's implicit parameter is `value`, and `nameof(value)` names it.
            if (value is null) {
                throw new ArgumentNullException("valeu");
            }

            slots[0] = value;
        }
    }

    public string? this[int index] {
        get => slots[index];
        set {
            if (index < 0) {
                throw new ArgumentOutOfRangeException("idnex");
            }

            slots[index] = value;
        }
    }
}
