using System.Collections.ObjectModel;
using System.Linq;

// `Last(p)` scans a non-List `IList<T>` backward, which only a predicate that runs nothing and cannot
// throw makes unobservable — and this one only reads its parameter.
public static class Probe {
    public static int Run() {
        var numbers = new ReadOnlyCollection<int>(new[] { 1, 2, 3, 4, 5 });
        return numbers.Where(n => n % 2 == 0).Last();
    }
}
