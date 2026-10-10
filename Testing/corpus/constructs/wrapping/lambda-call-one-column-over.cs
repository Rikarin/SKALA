// One column past the margin, a local's lambda over a call (#453 round 8): the gap between the type and the
// name breaks under a type at least `39 - name` wide (28 to 32 for names of ten or more) where the arguments
// are at most 35 columns; a ten-column-or-wider name under a narrower type breaks its `=` once the call's `(`
// is far enough past it; otherwise the arrow or the arguments decide as they do past 121. Rows taken from the
// measured grids.
class C {
    void M() {
        Func<TTTTTTTTTT> nnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTT> nnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTT> nnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxx, yyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxx, yyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxx, yyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> n = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxx, yyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxx, yyyyyyyyyyyy);
        Func<TTTTTTTTTTTT> nnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxx, yyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxx, yyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTT> nnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTT> n = () => CCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTT> nnnnn = () => CCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTT> n = () => CCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> n = () => CCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnn = () => CCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTT> nnnnnnnnnnnnnnnnnn = () => CCC(xxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTT> nnnnnnnnnnnnnn = () => CCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnn = () => CCCCCCCC(xxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnn = () => CCCCCCCCC(xxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxx, yyy);
        Func<TTTTTTTTTTTTTT> nnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxx, yyyyyyy);
        Func<TTTTTTTTTTTTTT> nnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxx, yyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxx, yyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxx, yyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCC(xxxxxxxxx, yyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxx, yyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxx, yyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxx, yyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxx, yyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxx, yyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxx, yyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxx, yyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxx, yyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxx, yyyyyyyyyyyyyy);
    }
}
