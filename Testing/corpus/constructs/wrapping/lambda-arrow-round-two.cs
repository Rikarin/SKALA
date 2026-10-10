// Round 2 of #453 and #586 (SK-DIV-0050, SK-DIV-0377, SK-DIV-0420): a local named in ten columns or more
// breaks its `=` before a lambda over a call by a reach that grows with the name and the type separately and
// a floor on the arguments; a field's lambda over a call keeps its `=` and reads the arrow's floor further
// right; an assignment to a target of four to nine columns reads it as a local does; an operand arrow's cap
// rises past 36 columns of parameter text; a receiver lambda's operand chain takes a level of its own while
// the arrow stays. Rows taken from the measured grids.
class C {
    Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(ppppppppppppppppppp, qqqqqqqqqqqqqqqqqqq);
    Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> n = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(ppppppppppppppppppppppppppppppppppppppppppp);
    Func<TTTTTTTTTTTTTTTTT> nn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(ppppppppppp, qqqqqqqqqqq);
    Func<TTTTTTTTTTTTTTTT> nnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);
    Func<TTTTTTT> nnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppppppppppppppppppppp);
    Func<TTTTTTTTTTTTTTTTTT> nnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);
    Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnn = () => CCCCCCCCC(pppppppppppppppppppppppppppppppppp, qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq);
    Func<TTTTTTTTTTTTTTTTTTTTTT> nnnn = () => CCCC(ppppppppppppppppppppppppppppppppppppppppp, qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq);
    Func<TTTTTTTTT> nnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppppppppppp, qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq);

    void M() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy);
        Func<TT> nnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyy);
        Action nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = (a, b) => CCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppppppppppppppppppppppppp);
        TTTTTTTTTTTTTTTTTTTTTTTTT nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = (int first, string second) => CCCCCCCC(ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);
        Func<TT> nnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyy);
        Func<TT> nnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy);
        Func<TT> nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy);
        Action nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);
        Func<TT> nnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnn = () => CCCCCCCCCCCCC(xxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyy);
        Func<TTTTTTTTTTTTTT> nnnnnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCC(xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy);
        TTTTTTT nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = (a, b) => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppppppp, qqqqqqqqqqqqqqqqqqqqqqqqqqqqqq, rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr);
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> nnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCC(xxxxxxxxxxxxxxxxx, yyyyyyyyyyyyyyyyy);
        TTTTTTTTTTTTTTTTTTTTT nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(ppppppppppppppppppp, qqqqqqqqqqqqqqqqqqqq);
        var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppp, qqqqqq, rrrrrrr);
        nnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppp);
        nnnnnnnnn = () => CCCCC(pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);
        nnnnnnn = () => CCCCCCCCCCCCCCCCCCCCCC(ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);
        nnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);
        nnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);
        nnnnnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(pppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);
        n = () => CCCCCCCCCCCCCCCCCCCCCCCCCCC(ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);
        nnn = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(ppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppppp);
        nnnnnnnnn = () => CCC(pppppppppppppppppppppppppppppppppppppppppppppppp, qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq);
        var g = iiiiiii.Where(nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccccccccccccccc);
        var g = iiiiiiiiiiiiiiiiiiiiii.Where(nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbb && ccccccccccccc);
        var g = iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii.Where(nnnnnnnnnnnnnnnnnnnn => aaaaaaaa && bbbbb && ccccc).ToList();
        var g = iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii.Where(nnnnnnnnnnnnnnnnnnnn => aaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccc).ToList();
        var g = iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii.Where(nnnn => aaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbb && cccccccccccccccccc).ToList();
        var g = ii.Where(nnnn => aaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccccccccccccccccccccccccccccccccccc).ToList();
        U((TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT x) => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccc);
        UUUUUUUUUUUUUUUUUUUUUUUUUUUUUUU(nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn => aaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbb && cccccccccccccccccc);
        var g = iiiiiiii.Where(static node => aaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbb && cccccccccccccccc).ToList();
        var g = iiiiiiiiiiiiiiiiiiiiiiiiiiiii.Where(static node => aaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccccccccccccccccc).ToList();
        var g = iiiiiiiiiiiiiiiiiiiiiiiiii.Where(x => aaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccccccccc).ToList();
        var g = iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii.Where(x => aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccc).ToList();
        var g = iiiiiii.Where((TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT x) => x is AAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC);
        var g = iiiiiiiiiiiiiiiiiiiiiiiiiiiiiii.Where((TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT x) => x is AAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCC);
        var g = iiiiiiii.Where(nnnnnnnnnnnnnnnnnnnn => x is AAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBB or CCCCCCCCCCCCCC).ToList();
        var g = ii.Where(nnnnnnnnnnnnnnnnnnnn => x is AAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCC).ToList();
        var g = iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii.Where(nnnn => x is AAA or BBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCC).ToList();
        var g = iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii.Where(nnnn => x is AAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC).ToList();
        U((TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT x) => x is AAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC);
        UUUUUUUUUUUUUUUUUUUUUUUUU(nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn => x is AAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCC);
        var g = iiiii.Where(static node => x is AAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCC).ToList();
        var g = ii.Where(static node => x is AAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC).ToList();
        var g = iiiii.Where(x => x is AAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC).ToList();
        var g = iiiiiiiiiii.Where(x => x is AAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC).ToList();
    }
}
