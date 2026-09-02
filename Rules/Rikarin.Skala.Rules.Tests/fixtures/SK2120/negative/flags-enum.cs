// The declaration says the members are bits. Combining them is the point.
using System;

[Flags]
enum Access {
    None = 0,
    Read = 1,
    Write = 2,
    All = Read | Write
}

sealed class Gate {
    public Access Combine(Access left, Access right) => left | right;

    public bool Allows(Access held, Access wanted) => (held & wanted) == wanted;

    public Access Clear(Access held, Access unwanted) => held & ~unwanted;

    public Access Toggle(Access held, Access bit) => held ^ bit;

    public Access? Lifted(Access? left, Access? right) => left | right;
}
