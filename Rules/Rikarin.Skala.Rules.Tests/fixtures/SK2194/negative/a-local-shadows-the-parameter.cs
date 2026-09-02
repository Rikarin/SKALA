namespace Fixtures {
    sealed class Retry(int attempts) {
        public int Drain() {
            var remaining = attempts;
            while (remaining > 0) {
                remaining--;
            }

            return remaining;
        }
    }
}
