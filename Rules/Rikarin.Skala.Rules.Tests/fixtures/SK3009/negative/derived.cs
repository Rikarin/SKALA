using System; class Mine : Lazy<int> { public Mine(bool safe) : base(() => 1, safe) { } } class C { static Mine Value = new(false); }
