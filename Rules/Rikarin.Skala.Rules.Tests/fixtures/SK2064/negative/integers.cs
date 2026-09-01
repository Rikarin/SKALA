class C {
    int Mask(int value, int mask) => value & mask;

    int Set(int value, int bits) => value | bits;

    uint Both(uint a, uint b) => (a & b) | (a ^ b);
}
