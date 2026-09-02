// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
class C {
    bool A(bool a) => !a;
    int B(int b) => -b;
    int C1(int b) => +b;
    int D(int b) => ~b;
    int E(int b) => ++b;
    int F(int b) => --b;
    unsafe int G(int* p) => *p;

    unsafe int* H(int b) {
        return &b;
    }
}
