// ⚠ The other half of the same exclusion. A `ref` parameter assigned without being read is a
// method whose whole purpose is to produce a value in the caller's variable, and data flow reads
// it identically to the defect — the write is visible and the incoming value flows in nowhere.
namespace Fixtures {
    sealed class Resetter {
        public void Reset(ref int counter) {
            counter = 0;
        }
    }
}
