using System.Collections.Generic;

// `typeof(List<int>).Name` is "List`1"; `nameof(List<int>)` is "List".
public sealed class Generics {
    public string TypeName() => typeof(List<int>).Name;
}
