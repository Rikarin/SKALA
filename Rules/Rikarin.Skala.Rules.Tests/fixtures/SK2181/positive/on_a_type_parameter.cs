using System;
using System.Collections.Generic;

sealed class Registry {
    readonly Dictionary<Type, object> map = new();

    public void Register(Type contract, object value) {
        map[contract.GetType()] = value;
    }
}
