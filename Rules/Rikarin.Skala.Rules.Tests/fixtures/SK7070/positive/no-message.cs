using System;

public sealed class Store {
    [Obsolete]
    public void Save() { }

    [ObsoleteAttribute]
    public void Flush() { }
}
