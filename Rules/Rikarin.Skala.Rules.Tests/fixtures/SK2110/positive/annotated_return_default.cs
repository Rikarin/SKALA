// `default` is the same constant wearing a different word, and the branch that produces it is the
// one a caller will hit first.
namespace Fixtures {
    sealed class Coupon {
        readonly bool redeemed;

        public Coupon(bool redeemed) => this.redeemed = redeemed;

        public override string? ToString() {
            if (redeemed) {
                return default;
            }

            return "coupon";
        }
    }
}
