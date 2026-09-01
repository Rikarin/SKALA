class Item { public int Count; } class C { bool M(Item? item) => item is not null && item.Count == 3; }
