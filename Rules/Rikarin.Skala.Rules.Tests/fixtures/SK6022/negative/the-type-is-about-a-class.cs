public class BaseClass {
    public int Depth { get; init; }
}

public sealed class DerivedClass : BaseClass {
    public string Name { get; init; } = string.Empty;
}

public sealed class PrivateSetterTestClass {
    public string Value { get; private set; } = string.Empty;
}

public sealed class NestedClass {
    public int Level { get; init; }
}
