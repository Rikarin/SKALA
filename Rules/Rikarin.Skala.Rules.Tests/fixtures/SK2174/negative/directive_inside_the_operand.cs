// A `(` and a `)` inserted on either side of an `#if` do not necessarily both survive into the same
// compilation.
class C {
    int M(int value, int offset) =>
        value << offset
#if WIDE
            * 2
#else
            + 1
#endif
        ;
}
