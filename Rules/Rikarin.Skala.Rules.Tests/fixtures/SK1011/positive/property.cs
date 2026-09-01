class Item { public bool Ready { get; set; } } class C { bool M(Item? item) => item != null && item.Ready == true; }
