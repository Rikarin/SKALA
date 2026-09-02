// An initializer runs while the constructor runs, where the parameter is an ordinary parameter
// and nothing is captured. This is also the repair the rule's message asks for.
namespace Fixtures {
    sealed class Retry(int attempts) {
        int remaining = attempts;

        public bool Next() {
            remaining--;
            return remaining > 0;
        }
    }
}
