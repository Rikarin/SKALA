using System;
using System.Linq.Expressions;

public sealed class Row {
    public string? Name { get; set; }
}

public sealed class Queries {
    public static Expression<Func<Row, bool>> Named() => row => row.Name != null;
}
