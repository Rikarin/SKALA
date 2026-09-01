// ⚠ The convention for saying the assignment was meant. gcc's -Wparentheses honours it and so does
// this rule; without the exemption there would be no way at all to write the intended program.
class C {
    bool ok;

    void M() {
        if ((ok = TryLoad())) {
            Use();
        }

        while ((ok = TryLoad())) {
            Use();
        }
    }

    static bool TryLoad() => false;

    static void Use() { }
}
