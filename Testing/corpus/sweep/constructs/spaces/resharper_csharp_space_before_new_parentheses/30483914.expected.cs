// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
class C {
    C() { }

    C(int a) { }

    object A() => new C();
    object B() => new C(1);
    C D() => new ();
    C E() => new (1);
}
