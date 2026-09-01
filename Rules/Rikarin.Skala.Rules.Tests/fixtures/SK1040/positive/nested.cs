using System;
using System.Collections.Generic;

public sealed class Series {
    public List<Nullable<int>> Points { get; } = new();

    public Nullable<int>[] Samples { get; } = new Nullable<int>[4];
}
