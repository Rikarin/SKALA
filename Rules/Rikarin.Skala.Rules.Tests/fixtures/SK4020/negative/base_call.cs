using System;

class BaseFixture {
    public override string ToString() => "base";
}

sealed class DerivedFixture : BaseFixture {
    public Func<string> Describe() => () => base.ToString();
}
