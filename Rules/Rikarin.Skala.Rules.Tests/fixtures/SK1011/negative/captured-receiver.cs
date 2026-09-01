using System; class Item { public int Count; } class C { bool M(Item? item) { Action reset = () => item = null; return item != null && item.Count == 3; } }
