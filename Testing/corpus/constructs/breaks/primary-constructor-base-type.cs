// Issue #427. A primary constructor's base type with arguments is laid out as an initializer: the break
// goes before the ':' when the list then fits, and otherwise ': B(' stays on the declaration's line and
// only the arguments chop. Skala broke after the ':' and moved a chopped base type a level in.
class L(int a, int b) : B(a, b // e
) {
}

class L2(int a, int b) : B(a, b /*e*/
) {
}

record R(int a, int b) : B(a, b // e
);

struct S(int a, int b) : I(a, b // e
) {
}

class L3(int a, int b) : B(a, b // e
), I1, I2 {
}

class L4<T>(int a, int b) : B(a, b // e
) where T : class {
}

class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb) {
}

class L7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd) {
}

class L8(int a) : B(a,
    b) {
}

namespace N {
    class L(int a, int b) : B(a, b // e
    ) {
    }

    class L7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd) {
    }

    record R(int a, int b) : B(a, b // e
    ), I1;
}

class M1VeryLongClassNameeeeeeeeeeeeeeeeeeeeeeeeeeeeeee(int aaaaaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbb) {
}

class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd), IAaaaa, IBbbbb {
}

class M3(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa), IAaaaaaaaaaaaaaaaaaaaaaaaaa, IBbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb {
}

class M4(
    int a,
    int b
) : B(a, b) {
}

class M5(int a, int b) : B(a, b // e
), I1 {
}

class C : X {
    C(int a, int b) : base(a, b // e
    ) {
    }

    C(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccc) : base(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb) {
    }
}

namespace N {
    namespace O {
        class L3(int a, int b) : B(a, b // e
        ), I1, I2 {
        }

        class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd), IAaaaa, IBbbbb {
        }

        class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbb) {
        }
    }
}
