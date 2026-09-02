using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // An array is a legal value for IList<T>, which puts the exception type back out of reach.
    public static int Third(IList<int> entries) => entries.ElementAt(2);
}
