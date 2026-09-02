using System.Collections.Generic;

// `typeof(List<>)` is legal; `value is List<>` is not a pattern C# has.
class UnboundGeneric {
    public bool Test(object value) => typeof(List<>).IsInstanceOfType(value);
}
