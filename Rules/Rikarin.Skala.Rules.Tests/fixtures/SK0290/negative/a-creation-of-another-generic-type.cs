using System.Collections.Generic;

public static class OtherGeneric {
    // The created type has to be `System.Nullable<T>` itself. A one-type-argument generic whose
    // constructor takes a value of that argument's type is the shape closest to this one — here the
    // capacity overload, where `T` and the parameter are both `int` — and it is not it.
    public static List<int> Go(int value) => new List<int>(value);
}
