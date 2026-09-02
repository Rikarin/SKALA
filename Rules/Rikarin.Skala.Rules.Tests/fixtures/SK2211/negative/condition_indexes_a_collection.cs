// An element access reads state the loop body may well change through the collection rather than
// through the variable, so it is declined with the other non-locals.
class C {
    void M(int[] flags) {
        var i = 0;
        while (flags[i] > 0) {
            System.Console.WriteLine(i);
        }
    }
}
