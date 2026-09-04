// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Utilities;

namespace Newtonsoft.Json.Linq.JsonPath {
    internal class FieldFilter : PathFilter {
        internal string? Name;

        public FieldFilter(string? name) {
            Name = name;
        }

        public override IEnumerable<JToken> ExecuteFilter(
            JToken root,
            IEnumerable<JToken> current,
            JsonSelectSettings? settings
        ) {
            foreach (JToken t in current) {
                if (t is JObject o) {
                    if (Name != null) {
                        JToken? v = o[Name];
                        if (v != null) {
                            yield return v;
                        } else if (settings?.ErrorWhenNoMatch ?? false) {
                            throw new JsonException(
                                "Property '{0}' does not exist on JObject.".FormatWith(
                                    CultureInfo.InvariantCulture,
                                    Name
                                )
                            );
                        }
                    } else {
                        foreach (KeyValuePair<string, JToken?> p in o) {
                            yield return p.Value!;
                        }
                    }
                } else {
                    if (settings?.ErrorWhenNoMatch ?? false) {
                        throw new JsonException(
                            "Property '{0}' not valid on {1}.".FormatWith(
                                CultureInfo.InvariantCulture,
                                Name ?? "*",
                                t.GetType().Name
                            )
                        );
                    }
                }
            }
        }
    }
}
