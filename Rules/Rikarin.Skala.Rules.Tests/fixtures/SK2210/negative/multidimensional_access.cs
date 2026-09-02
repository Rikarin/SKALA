// A rectangular array's access carries two arguments, and the rule reads a single one. `^` and
// ranges are not available on it at all, so there is nothing here the constants decide.
class C {
    int At(int[,] grid) => grid[1, 1];
}
