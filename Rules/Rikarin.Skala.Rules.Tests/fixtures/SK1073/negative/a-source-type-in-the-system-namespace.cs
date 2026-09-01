namespace System {
    public sealed class Guid { }

    public sealed class Registry {
        public Guid Fresh() => new Guid();
    }
}
