namespace Fixtures {
    readonly struct Tag {
        public Tag(int value) => Value = value;

        public int Value { get; }

        public static bool operator ==(Tag left, Tag right) => left.Value == right.Value;

        public static bool operator !=(Tag left, Tag right) => !(left == right);

        public override bool Equals(object? other) => other is Tag tag && tag.Value == Value;

        public override int GetHashCode() => Value;
    }

    sealed class Reader {
        public static bool Same(Tag left, Tag right) => left == right;
    }
}
