// The whole point of `as`: a narrowing that can fail, answered without an exception.
class Base { }

sealed class Derived : Base { }

sealed class Consumer {
    public Derived? Narrow(Base value) => value as Derived;

    public string? Text(object value) => value as string;
}
