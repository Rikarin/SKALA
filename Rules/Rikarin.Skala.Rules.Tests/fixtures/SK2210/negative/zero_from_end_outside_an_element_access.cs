// `^0` is a perfectly good `Index` value. It is only wrong where it fetches an element, so a
// variable holding one, and one passed to a method, are both left alone.
using System;

class C {
    Index End() => ^0;

    int Offset(int length) {
        Index end = ^0;
        return end.GetOffset(length);
    }
}
