using System;

public sealed class Panel {
    public void Refresh() => throw new InvalidOperationException("nothing to refresh");
}
