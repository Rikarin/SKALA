unsafe class C {
    int _field;

    void M() {
        fixed (int* p = &_field) {
            System.Console.Write(*p);
        }
    }
}
