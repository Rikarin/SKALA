using System.Linq;

public sealed class Holder {
    public static bool Has(IQueryable<int> items) => items.Any();
}
