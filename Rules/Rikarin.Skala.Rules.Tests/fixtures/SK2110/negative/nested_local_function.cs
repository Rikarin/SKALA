// The `return null` belongs to the local function, not to ToString().
namespace Fixtures {
    sealed class Batch {
        public override string? ToString() {
            string? Inner() {
                return null;
            }

            return Inner() ?? "batch";
        }
    }
}
