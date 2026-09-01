// A left shift does not sign-extend and has no unsigned variant to move to.
public sealed class Leftwards {
    public int Low(int hash) => (int)((uint)hash << 16);
}
