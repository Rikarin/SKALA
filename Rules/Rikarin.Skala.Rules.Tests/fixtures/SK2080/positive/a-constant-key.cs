using System.Collections.Generic;

public sealed class Headers {
    const string TenantId = "tenant-id";

    public static readonly SortedDictionary<string, int> Order = new() {
        ["tenant-id"] = 0,
        ["trace-id"] = 1,
        [TenantId] = 2
    };
}
