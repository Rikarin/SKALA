// A lambda converts to `Expression<Func<>>`, so overload resolution picks `Queryable` and the query
// is still a query. Nothing here has degraded, and an `IEnumerable<T>` receiver was never this
// rule's subject in the first place.
using System.Collections.Generic;
using System.Linq;

class Order {
    public int Status { get; set; }
}

class C {
    IQueryable<Order> Open(IQueryable<Order> orders) => orders.Where(o => o.Status == 0);
    IQueryable<int> Ids(IQueryable<Order> orders) => orders.Select(o => o.Status);
    IOrderedQueryable<Order> Ranked(IQueryable<Order> orders) => orders.OrderBy(o => o.Status);
    IEnumerable<Order> Plain(IEnumerable<Order> orders) => orders.Where(o => o.Status == 0);
}
