class Reading {
    public int Value { get; set; }
}

class AndPattern {
    public bool Narrow(Reading r) => r is { Value: > 1 } and { Value: < 9 };
}
