using System;

// `nameof` names the type rather than using it, and `nameof(int?)` is not legal C#.
public sealed class Naming {
    public static string TypeName() => nameof(Nullable<int>);
}
