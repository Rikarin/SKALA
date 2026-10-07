// A static local function's parameter of the same name is a different symbol.
namespace Fixtures {
    sealed class Retry(int attempts) {
        public int Remaining() {
            return Drain(attempts);

            static int Drain(int attempts) {
                attempts--;
                return attempts;
            }
        }
    }
}
