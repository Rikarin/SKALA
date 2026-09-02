// `Activator.CreateInstance(typeof(List<>))` throws too, and is the one shape where an author may
// be closing the type from it somewhere this rule cannot see.
using System;
using System.Collections.Generic;

public sealed class Factory {
    public Type Open() => typeof(List<>);
}
