class Item { public int Count; } class C { bool M(Item? item) { ref Item? alias = ref item; return item != null && item.Count == 3; } }
