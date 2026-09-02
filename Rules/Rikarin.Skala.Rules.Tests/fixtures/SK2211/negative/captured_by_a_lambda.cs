// ⚠ A delegate created before the loop and invoked inside it writes through the closure without
// the write appearing in the body's data flow at all. The guard over-bails: any lambda or local
// function in the member that so much as mentions the name is enough.
using System;

class C {
    void M(int count) {
        var i = 0;
        Action advance = () => i++;
        while (i < count) {
            advance();
        }
    }
}
