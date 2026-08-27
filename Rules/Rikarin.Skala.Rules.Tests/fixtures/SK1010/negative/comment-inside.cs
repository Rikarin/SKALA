using System.Collections.Generic;

public sealed class Holder {
    public static bool Has(List<int> items) => items /* deliberately an operator */ != null;
}
