// fixture-option: AllowUnsafe = true
public unsafe delegate int Deref(int* pointer);

public static unsafe class Pointers {
    public static readonly Deref Read = delegate(int* pointer) { return *pointer; };
}
