// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
class C {
    C() { }

    C(int a) { }

    object A() => new C();
    object B() => new C(1);
    C D() => new ();
    C E() => new (1);
}
