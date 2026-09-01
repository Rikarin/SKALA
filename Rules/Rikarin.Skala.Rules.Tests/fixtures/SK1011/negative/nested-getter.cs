class Item { public Item Next => this; public int Count; } class C { bool M(Item? item) => item != null && item.Next.Count == 3; }
