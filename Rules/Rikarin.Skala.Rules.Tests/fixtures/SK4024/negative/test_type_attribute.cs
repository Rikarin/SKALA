using System;

sealed class TestAttribute : Attribute { }

[Test]
sealed class TestTypeFixture {
    public void Prepare() {
        GC.Collect();
    }
}
