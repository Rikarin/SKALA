struct Box<T> {
    public T Value;
}

class C {
    bool M(Box<int> a, object b) => a.Equals(b);
}
