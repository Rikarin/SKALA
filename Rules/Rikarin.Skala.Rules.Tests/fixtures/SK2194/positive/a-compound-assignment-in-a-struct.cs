namespace Fixtures {
    struct Budget(decimal total) {
        public void Spend(decimal amount) => total -= amount;

        public decimal Remaining => total;
    }
}
