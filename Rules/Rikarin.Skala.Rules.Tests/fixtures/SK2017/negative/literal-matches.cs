using System;

public sealed class Legacy {
    // A hand-written literal that *does* name the parameter is not this rule's defect. It is
    // rename-fragile, which is a different concept and a different id.
    public void Check(string value, int count) {
        if (value is null) {
            throw new ArgumentNullException("value");
        }

        if (count < 0) {
            throw new ArgumentOutOfRangeException("count", count, "must not be negative");
        }
    }
}
