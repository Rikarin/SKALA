class C {
    C() {
    }

    C(int a) {
    }

    object A() => new C();
    object B() => new C(1);
    C D() => new();
    C E() => new(1);
}
