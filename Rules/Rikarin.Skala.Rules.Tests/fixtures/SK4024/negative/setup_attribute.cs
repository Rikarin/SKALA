using System;

sealed class GlobalSetupAttribute : Attribute { }

sealed class SetupFixture {
    [GlobalSetup]
    public void Prepare() {
        GC.Collect();
    }
}
