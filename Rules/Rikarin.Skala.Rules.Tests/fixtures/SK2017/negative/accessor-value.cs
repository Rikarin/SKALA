using System;

public sealed class Settings {
    string? name;
    string? label;
    EventHandler? changed;

    // ⚠ `value` is a real parameter of every `set`, `init`, `add` and `remove` accessor, and it is
    // the name the BCL itself passes here. A rule that did not know that would fire on all four.
    public string? Name {
        get => name;
        set => name = value ?? throw new ArgumentNullException("value");
    }

    public string? Label {
        get => label;
        init => label = value ?? throw new ArgumentNullException("value");
    }

    public event EventHandler Changed {
        add => changed += value ?? throw new ArgumentNullException("value");
        remove => changed -= value ?? throw new ArgumentNullException("value");
    }
}
