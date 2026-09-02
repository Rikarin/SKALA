namespace Fixtures {
    sealed class Retry(int attempts) {
        public bool Next() {
            attempts--;
            return attempts > 0;
        }
    }
}
