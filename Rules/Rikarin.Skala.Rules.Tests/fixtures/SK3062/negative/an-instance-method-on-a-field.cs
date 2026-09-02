using System.Collections.Generic;

// `this` handed to an object this type owns. The receiver's lifetime is this object's lifetime, so
// the reference has gone from the object into its own field and nothing has escaped — there is no
// second reader. The unqualified `Record(this)` has no receiver at all and is declined a step
// earlier.
public sealed class Registrar {
    readonly List<object> seen = new();

    public Registrar() {
        seen.Add(this);
        Record(this);
    }

    public int Count => seen.Count;

    void Record(object item) => seen.Add(item);
}
