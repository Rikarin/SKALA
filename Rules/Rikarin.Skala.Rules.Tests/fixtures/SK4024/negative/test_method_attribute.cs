using System;

sealed class FactAttribute : Attribute { }

sealed class TestMethodFixture {
    [Fact]
    public void CollectsBeforeMeasuring() {
        GC.Collect();
    }
}
