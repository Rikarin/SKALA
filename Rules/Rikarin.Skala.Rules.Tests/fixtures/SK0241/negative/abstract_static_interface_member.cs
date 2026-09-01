interface IAdd<T> where T : IAdd<T> {
    static abstract T Add(T left, T right);
}
