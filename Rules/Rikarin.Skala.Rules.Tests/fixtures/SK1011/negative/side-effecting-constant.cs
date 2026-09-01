class Item { public int Count; } class C { int Next() => 3; bool M(Item? item) => item != null && item.Count == Next(); }
