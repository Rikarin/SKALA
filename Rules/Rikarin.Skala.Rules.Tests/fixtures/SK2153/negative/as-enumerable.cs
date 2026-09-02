// ⚠ The deliberate form, and the one this rule may never report. `AsEnumerable()` returns
// `IEnumerable<T>`, so the receiver's static type no longer implements `IQueryable` and the operator
// chained onto it cannot reach the rule at all — the exclusion is structural rather than a name the
// analyzer has to remember.
using System;
using System.Collections.Generic;
using System.Linq;

class Order {
    public int Status { get; set; }
}

class C {
    IEnumerable<Order> Open(IQueryable<Order> orders, Func<Order, bool> predicate) =>
        orders.AsEnumerable().Where(predicate);

    IEnumerable<Order> Materialised(IQueryable<Order> orders, Func<Order, bool> predicate) =>
        orders.ToList().Where(predicate);

    IEnumerable<Order> Copied(IQueryable<Order> orders, Func<Order, bool> predicate) =>
        orders.ToArray().Where(predicate);
}
