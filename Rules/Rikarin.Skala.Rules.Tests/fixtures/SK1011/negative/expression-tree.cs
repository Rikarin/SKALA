using System; using System.Linq.Expressions; class Item { public int Count; } class C { Expression<Func<Item, bool>> M() => item => item != null && item.Count == 3; }
