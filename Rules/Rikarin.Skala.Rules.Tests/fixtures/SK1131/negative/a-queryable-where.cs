using System.Linq;

// ⚠ Measured: on an `IQueryable<int>` the anonymous method binds to `Enumerable.Where` and runs; the
// lambda picks `Queryable.Where` and is CS0834. The candidates are in two different static classes.
public static class Filtering {
    public static int[] Positive(IQueryable<int> values) {
        return values.Where(delegate(int v) { return v > 0; }).ToArray();
    }
}
