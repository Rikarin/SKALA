class Item { public string? Name { get; set; } } class C { bool M(Item? item) => item != null && item.Name == "abc"; }
