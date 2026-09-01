// ⚠ A cast binds at unary precedence and `>>>` binds below every arithmetic operator, so the bare
// rewrite would be `hash >>> (16 + 1)`.
public sealed class Rebinding {
    public int High(int hash) => (int)((uint)hash >> 16) + 1;
}
