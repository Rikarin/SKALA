using System;

// The receiver is a value, not a `typeof`. There is no type to write to the right of `is`.
class TypeVariable {
    public bool Test(Type contract, object value) => contract.IsInstanceOfType(value);
}
