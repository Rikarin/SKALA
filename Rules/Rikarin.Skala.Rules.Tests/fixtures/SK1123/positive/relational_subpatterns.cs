class Reading {
    public int Value { get; set; }
}

// `or` is the loosest combinator, so the merged form still groups the `and` first.
class RelationalSubpatterns {
    public bool Interesting(Reading r) => r is { Value: > 1 and < 5 } or { Value: 9 };
}
