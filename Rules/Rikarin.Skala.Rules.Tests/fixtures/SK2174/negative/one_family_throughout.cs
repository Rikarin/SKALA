// One precedence family throughout is left-associative and obvious, and adding parentheses to it
// would be noise rather than clarity.
class C {
    int Bitwise(int a, int b, int c) => a & b & c;

    int Shifts(int a, int b, int c) => a << b << c;

    int Arithmetic(int a, int b, int c) => a + b - c;

    int Ors(int a, int b, int c) => a | b | c;
}
