// The `IOrderedEnumerable<T>` arm: an ordering operator degrades the query exactly as a filtering
// one does, and its return type is not `IEnumerable<T>`.
using System;
using System.Collections.Generic;
using System.Linq;

class Row {
    public int Rank { get; set; }
}

class C {
    IEnumerable<Row> Ranked(IQueryable<Row> rows, Func<Row, int> key) => rows.OrderBy(key);

    IEnumerable<Row> Grouped(IQueryable<Row> rows, Func<Row, int> key) => rows.OrderByDescending(key);
}
