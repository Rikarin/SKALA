class C {
    int A(int a) => a << 2;
    int B(int a) => a >> 1;
    int C1(int a) => a << 2 >> 1;
    int D(int a, int b) => a >> b;
    int E(int a) => a >>> 1;

    void F(int a) {
        a <<= 2;
        a >>= 1;
    }
}
