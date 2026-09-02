public sealed class Box {
    public int? Value { get; set; }
}

public static class InitializerMember {
    public static Box Go(int value) => new Box { Value = new int?(value) };
}
