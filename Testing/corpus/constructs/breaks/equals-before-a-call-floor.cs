// An `=` before a call with two arguments, past the margin (#446, SK-DIV-0211): the oracle breaks after
// the `=` exactly while the argument list is narrower than a floor that depends on where the call's `(`
// lands, on the statement's indent, on the callee's width and on what owns the `=` — and chops the
// arguments otherwise. Each pair sits one column either side of its measured floor.
class EqualsBeforeACallFloor {
    private static readonly object ffffffffffffffffffffffffffffffff = Compute(xxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyy);
    private static readonly object fffffffffffffffffffffffffffffffff = Compute(xxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyy);
    private static readonly object fffffffffffffffffffffffffffffffff = Compute(xxxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyy);
    private static readonly object ffffffffffffffffffffffffffffffffffffffffffffff = Compute(xxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyy);
    private static readonly object ffffffffffffffffffffffffffffffffffffffffffffff = Compute(xxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyy);
    private static readonly object ffffffffffffffffffffffffffffffffffffffffffffffffffffffffff = Compute(xxxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyy);
    private static readonly object ffffffffffffffffffffffffffffffffffffffffffffffffffffffffff = Compute(xxxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyyy);

    void M() {
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyy);
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyy);
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyy);
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyy);
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyy);
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyy);
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyy);
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyy);
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyy);
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyy);
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Cccccccccccccccccccc(xxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyy);
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Cccccccccccccccccccc(xxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyy);
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Cccccccccccccccccccc(xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyy);
        var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Cccccccccccccccccccc(xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);
        int[] wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww = Compute(xxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyy);
        int[] wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww = Compute(xxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyy);
        int[] wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww = Compute(xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);
        int[] wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww = Compute(xxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);
        int[] wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww = Compute(xxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyy);
        int[] wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww = Compute(xxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyy);
        _aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = Compute(xxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);
        _aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = Compute(xxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyy);
        _aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = Compute(xxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyy);
        _aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = Compute(xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyy);
        _aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = Compute(xxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);
        _aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = Compute(xxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyy);
        {
            {
                var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyy);
                var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyy);
                var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyy);
                var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyy);
                var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyy);
                var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxx, yyyyyyyyyy);
                var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxx, yyyyyyyyyyyy);
                var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxx, yyyyyyyyyyyyy);
                var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxx, yyyyyyyyyyyyyyy);
                var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = Ccccccc(xxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyy);
            }
        }
    }
}
