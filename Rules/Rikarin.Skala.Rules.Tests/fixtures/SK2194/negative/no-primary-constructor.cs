namespace Fixtures {
    sealed class Retry {
        int attempts;

        public Retry(int attempts) => this.attempts = attempts;

        public bool Next() {
            attempts--;
            return attempts > 0;
        }
    }
}
