using System;

sealed class BenchmarkAttribute : Attribute { }

sealed class BenchmarkFixture {
    [Benchmark]
    public void Measure() {
        GC.Collect();
    }
}
