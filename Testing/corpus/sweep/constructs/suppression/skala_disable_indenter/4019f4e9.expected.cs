// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
// ⚠ Wrong in indentation on every line, and two statements that have to wrap.
//
// The wraps are the half that separates `disable_indenter` from "indent to zero". A line that
// existed in the input keeps the leading whitespace the author wrote. A line the wrap created has
// none to keep, and what the oracle writes in front of it is the break point's own flat rendering:
// nothing after `(` and before `)`, one space after `,` and before a binary operator. Both
// polarities are here because the first reading of this key — "created lines start at column zero"
// — is right on the first and wrong on the second, and one shape cannot tell them apart.

class C {
        public void Method(int alpha, int beta, int gamma, int delta, int epsilon, int zeta, int eta, int theta) {
    var value = Compute(alpha, beta)
 + Compute(gamma, delta)
 + Compute(epsilon, zeta)
 + Compute(eta, theta)
 + Compute(alpha, theta)
 + Compute(beta, eta);
      var chopped = Compute(
alpha + beta + gamma + delta,
 epsilon + zeta + eta + theta + alpha + beta + gamma + delta + epsilon + zeta + eta + theta
);
    }

    static int Compute(int left, int right) => left + right;
}
