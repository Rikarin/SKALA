class Record {
    public object Payload { get; set; } = new();
}

// ⚠ A discard is the one designation that is legal under `or`, and it merges: both
// `{ Payload: string _ } or { Payload: 2 }` and `{ Payload: string _ or 2 }` compile.
class DiscardDesignation {
    public bool Known(Record r) => r is { Payload: string _ } or { Payload: 2 };
}
