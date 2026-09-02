// `d` is a `Derived`, so the conversion to `Base` is the one an assignment performs. The `as`
// promises a test that cannot fail, and the null check below is a null check.
class Base { }

sealed class Derived : Base { }

sealed class Consumer {
    public Base? Widen(Derived derived) => derived as Base;
}
