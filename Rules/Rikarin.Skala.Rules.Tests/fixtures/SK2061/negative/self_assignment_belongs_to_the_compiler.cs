// `=` is not examined: CS1717 says it, and SK2012 says the rest.
#pragma warning disable CS1717
class C {
    void M(int q) {
        q = q;
        System.Console.Write(q);
    }
}
