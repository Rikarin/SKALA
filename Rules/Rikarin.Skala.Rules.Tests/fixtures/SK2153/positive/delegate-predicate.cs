// The way this defect is actually introduced: the predicate is a `Func<>` rather than an
// `Expression<Func<>>`, so `Where` binds to `Enumerable` and the provider is asked for the table.
using System;
using System.Collections.Generic;
using System.Linq;

class Order {
    public int Status { get; set; }
}

class C {
    IEnumerable<Order> Open(IQueryable<Order> orders, Func<Order, bool> predicate) => orders.Where(predicate);

    IEnumerable<int> Ids(IQueryable<Order> orders, Func<Order, int> selector) => orders.Select(selector);
}
