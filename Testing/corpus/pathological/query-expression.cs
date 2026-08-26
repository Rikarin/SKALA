using System.Linq;

class C {
    object M(int[] xs) =>
        from x in xs
        where x > 0
        orderby x descending
        group x by x % 2
        into g
        select g;
}
