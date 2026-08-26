class C {
#if A
#if B
    int _a;
#else
    int _b;
#endif
#else
    int _c;
#endif
}
