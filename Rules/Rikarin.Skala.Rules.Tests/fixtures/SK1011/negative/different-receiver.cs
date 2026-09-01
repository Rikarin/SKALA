class Item { public int Count; } class C { bool M(Item? item, Item other) => item != null && other.Count == 3; }
