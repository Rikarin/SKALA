using System.Collections.Generic;

// ⚠ The whole reason the literal shapes are restricted to defined positions. Every one of these
// strings equals a member name and none of them may follow a rename: they are a wire format.
public sealed class Row {
    public int Count { get; set; }

    public string Title { get; set; } = string.Empty;

    public Dictionary<string, object> ToPayload() =>
        new() { ["Count"] = Count, ["Title"] = Title };

    public string Query() => "select Title from rows where Count > 0";
}
