// Round 3 of #453, #605 and SK-DIV-0420: a held first call with a lambda for its argument moves below by its
// own limit, which weighs the head before the receiver; a lambda call overflowing by its `)` alone behind a
// head and receiver of 16 columns keeps the `)` alone; the wide name's `=` gate is one line in name and type,
// names of 10 to 12 included; and wide-named fields and assignment targets break their `=` by the same reach
// and floor. Rows taken from the measured grids.
class C {
    Func<TTTTTTT> nnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(ppppppppppppppppppppppppppppppppppppppppppppppppppppp);
    Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCC(ppppppppppppppppppppppppppp, qqqqqqqqqqqqqqqqqqqqqqqqqqqq);
    Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnn = () => CCCCCCCC(pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);
    Func<TT> nnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);
    Func<TTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);
    Func<TTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppppppppp, qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq);
    Func<TTTTTTTT> nnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppppppppppppppppppppppppppppp, qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq);
    Func<TTTTTT> nnnnnnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCC(ppppppppppppppppppppppppppppppppppppppppp, qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq);
    Func<TTTTTTTTTTTTTT> nnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppppppppppppppp, qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq);
    Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnnnnnn = () => CCCCCCC(pppppppppppppppppppppppppppppppppppppppppppppp);
    Func<TTTTTTTTTTTTTTTTTT> nnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppp);
    Func<TTTTT> nnnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppppppppppppppppppppppppp);

    void M() {
        List<Item> g = rrrrrrrrrrrrrrrrrrrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccc).ToList();
        List<Item> g = rrrrrrrrrrrrrrrrrrrrrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccc).ToList();
        List<Item> g = rrr.Where(x => aaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccc).ToList();
        List<Item> g = rr.Where(x => aaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccc).ToList();
        List<Item> g = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccccc).ToList();
        List<Item> g = rrrrrrrrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccccc).ToList();
        var g = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccc).ToList();
        var g = rrrrrrrrrrrrrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccc).ToList();
        var g = rrrrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccccc).ToList();
        var g = rrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccccc).ToList();
        var g = rrrrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccc).ToList();
        var g = rrr.Where(x => aaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccc).ToList();
        var g = rrrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccccccc).ToList();
        var g = rrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccc).ToList();
        var ggggggggg = rrrrrrrrrrrrrrrrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccc).ToList();
        var ggggggggg = rrrrrrrrrrrrrrrrrrrrrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccc).ToList();
        var ggggggggg = rr.Where(x => aaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccc).ToList();
        var ggggggggg = rrrrrrrrrrrrrrrrrrrrrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccccccccc).ToList();
        var ggggggggg = rrrrrrr.Where(x => aaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccc).ToList();
        var g = iiiiiiiiiiiiiiiiiiiiiiiiii.Where(x => x is AAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBB or CCCCCCCCCCCCC).ToList();
        var g = iiiiiiiiiiiiiiiii.Where(static node => x is AAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCC).ToList();
        var g = ii.Where(nnnnnnnnnnnnnnnnnnnn => aaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccc).ToList();
        var g = iiiii.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbb && cccccccccccccccccc).ToList();
        Func<TTTTTTTTTTTTTT> nnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyy);
        Func<TT> nnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxx, yyyyyyyyyyyyyy);
        Func<TTTTTT> nnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTT> nnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxx, yyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTT> nnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyy);
        Func<TT> nnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTT> nnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxx, yyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyy);
        Func<TTTTTT> nnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnn = () => CCCCCCCCCCCCC(xxxxxxxxx, yyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxx, yyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTT> nnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnn = () => CCCCCCCCCCCC(xxxxxxxxx, yyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTT> nnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTT> nnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnnn = () => CCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyy);
        nnnnnnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCC(pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);
        nnnnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(ppppppppppppppppppppppppppppppppppppp);
        nnnnnnnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppppppppppp);
        nnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);
        nnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppppppppppppppppppppppppppppppp);
        nnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppp, qqqqqqqqqqqqqqqqqqqqqqqqqq);
        nnnnnnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCC(pppppppppppppppppppppppppppppp, qqqqqqqqqqqqqqqqqqqqqqqqqqqqqq);
        nnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(ppppppppppppppppppppppppppppppppppp, qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq);
        nnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(ppppppppppppppppppppppppppppppppppppp, qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq);
        nnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCC(ppppppppppppppppppppppppppppppppppppppppppppp);
        nnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppppppppppppppp);
        nnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppp, qqqqqqqqqqqqqqqqqqqqqqqqq);
    }
}
