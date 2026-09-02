using System;

// SK2181 owns this: `contract` is already a `Type`, so `GetType()` returns `System.RuntimeType`
// for every input. That is a defect, and this rule's edit would contradict the one that says so.
class OnAType {
    public bool Test(Type contract) => typeof(Type).IsAssignableFrom(contract.GetType());
}
