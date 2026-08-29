class C {
    bool A(int a, int b) => a < b;
    bool B(int a, int b) => a > b;
    bool C1(int a, int b) => a <= b;
    bool D(int a, int b) => a >= b;
    bool E(int a, int b) => a == b;
    bool F(int a, int b) => a != b;
    bool G(object a) => a is string;
    bool H(object a) => a as string != null;
}
