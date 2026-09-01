class Item { public int Count; } class C { Item Current => new Item(); bool M() => Current != null && Current.Count == 3; }
