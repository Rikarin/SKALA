using System;

public sealed class Widget { }

public sealed class Registry {
    public bool Has(Type kind) => kind.IsDefined(typeof(Widget), false);
}
