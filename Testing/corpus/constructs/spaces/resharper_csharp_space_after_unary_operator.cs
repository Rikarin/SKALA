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
