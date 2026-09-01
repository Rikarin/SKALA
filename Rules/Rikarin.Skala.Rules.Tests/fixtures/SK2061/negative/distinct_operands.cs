class Range {
    public int start;
    public int end;
}

class C {
    int M(Range r) => r.end - r.start;

    bool N(bool a, bool b) => a && b;

    int P(int bits, int mask) => bits ^ mask;
}
