// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class C {
    C() { }

    C(int a) { }

    object A() => new C();
    object B() => new C(1);
    C D() => new ();
    C E() => new (1);
}
