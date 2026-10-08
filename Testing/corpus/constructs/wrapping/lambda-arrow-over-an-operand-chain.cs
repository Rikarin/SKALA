// A sole lambda argument whose body is an operand chain or a binary pattern on one line (#578): the arrow
// breaks once it reaches a column the parameter text and the first operand set, which moves out with the
// line's end, and a modifier counts as parameter text (`static node` as an eleven-column name).
class C {
    void M() {
        UU(nnnnnnnn => aaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccccccccccccccccccccc);
        UU(nnnnnnnn => aaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccccccccccccccccccccccccccc);
        UU(nnnnnnnn => aaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccccccccccccc);
        UU(static node => aaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccccccccccccccccccc);
        UU(static node => aaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccccccccccccccccccccccccccccc);
        UU(static node => aaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccccccccccccccc);
        UU(nnnnnnnnnnnnnnn => aaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && ccccccccccccccccccccccccccccccccccccccc);
        UU(nnnnnnnnnnnnnnn => aaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccccccccccccccccccccccccccc);
        UU(nnnnnnnnnnnnnnn => aaaaaaaaaaaaaaaaaaaaaa && bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb && cccccccccccccccccccccccccccccccccc);
        UU(x => x is Aaaaaaaaaaaa or Bbbbbbbbbbbbbbbb or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC);
        UUUUUUUU(x => x is Aaaaaaaaaaaa or Bbbbbbbbbbbbbbbb or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC);
        UUUUUUUU(x => x is Aaaaaaaaaaaa or Bbbbbbbbbbbbbbbb or CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC);
    }
}
