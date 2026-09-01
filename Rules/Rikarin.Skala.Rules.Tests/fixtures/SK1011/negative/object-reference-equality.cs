class Item { public object? Name { get; set; } } class C { bool M(Item? item) => item != null && item.Name == "abc"; }
