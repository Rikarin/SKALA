using System;
using System.Collections.Generic;
using System.IO;

public sealed class Bundle : IDisposable {
    readonly MemoryStream buffer = new();

    readonly List<IDisposable> owned = new();

    public void Register() {
        owned.Add(buffer);
    }

    public void Dispose() {
        foreach (var item in owned) {
            item.Dispose();
        }
    }
}
