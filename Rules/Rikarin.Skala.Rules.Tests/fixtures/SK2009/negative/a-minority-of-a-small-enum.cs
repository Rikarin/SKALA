// ⚠ The filter shape is not a large-enum problem, which is why #280's member-count threshold was
// rejected. JsonValueKind has eight values; this walker handles two of them and ignores six on
// purpose, because a string, a number, `true`, `false`, `null` and `Undefined` have nothing
// inside them to walk. SK2009 reported it on Skala's own tree (Tools/Rikarin.Skala.Cli.Tests/
// SarifPathTests.cs:36) and #280 read it as one of the genuine findings; it is not.
//
// The rule declines it for the same reason it declines the SyntaxKind filters: two values handled
// against six omitted is a selection, not an attempt at exhaustiveness that forgot something.

using System.Collections.Generic;
using System.Text.Json;

sealed class Walker {
    public static void Walk(JsonElement node, List<string> found) {
        switch (node.ValueKind) {
            case JsonValueKind.Object:
                foreach (var property in node.EnumerateObject()) {
                    Walk(property.Value, found);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in node.EnumerateArray()) {
                    Walk(item, found);
                }

                break;
        }
    }
}
