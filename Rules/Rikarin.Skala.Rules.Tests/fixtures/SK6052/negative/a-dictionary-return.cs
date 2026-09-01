using System.Collections.Generic;

namespace Contoso.Design;

// ⚠ A stated gap rather than a judgement that this is fine. A collection expression does not
// target-type to `IDictionary<K, V>`, so `[]` would not compile, and a fix that did not compile would
// be worse than a finding never made. `hasFix: true` is a promise about every finding.
public sealed class Settings {
    public IDictionary<string, string> All(bool empty) {
        return null;
    }
}
