// Operators with no `Queryable` counterpart. Binding these to `Enumerable` is the intended way to
// run a query, so reporting them would report every correct query in the repository.
using System.Collections.Generic;
using System.Linq;

class Order {
    public int Status { get; set; }
}

class C {
    List<Order> All(IQueryable<Order> orders) => orders.ToList();
    Order[] Copy(IQueryable<Order> orders) => orders.ToArray();
    HashSet<Order> Unique(IQueryable<Order> orders) => orders.ToHashSet();
    Dictionary<int, Order> ByStatus(IQueryable<Order> orders) => orders.ToDictionary(o => o.Status);
    int Total(IQueryable<Order> orders) => orders.Count();
    bool Some(IQueryable<Order> orders) => orders.Any();
    Order Head(IQueryable<Order> orders) => orders.First();
}
