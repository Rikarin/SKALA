// Six occurrences of the same header name, over the default threshold of five. Renaming the header
// means finding all six, and nothing in the file says they are the same decision.
using System.Collections.Generic;

namespace Fixtures;

class Headers {
    public static bool Has(string name) => name == "tenant-id";

    public static string Read(IDictionary<string, string> values) =>
        values.TryGetValue("tenant-id", out var value) ? value : string.Empty;

    public static void Write(IDictionary<string, string> values, string value) => values["tenant-id"] = value;

    public static void Remove(IDictionary<string, string> values) => values.Remove("tenant-id");

    public static bool Missing(IDictionary<string, string> values) => !values.ContainsKey("tenant-id");

    public static string Describe() => "tenant-id";
}
