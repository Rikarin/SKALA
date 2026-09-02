// ⚠ Added because a sabotage stayed green and the reason turned out to be a missing fixture rather
// than a dead guard. A static initializer cannot name an instance field directly — that is CS0236 —
// so it looked as though "the target must be static" could never matter. It can, through an
// instance: `head.next` is an instance field of this very type, declared below and carrying an
// initializer, and every other condition holds. It is still not a defect, because an instance
// field's initializer runs when the object is constructed, which here has already happened.
sealed class Node {
    static readonly Node Head = new Node();
    static readonly int First = Head.next;

    int next = 7;

    public int Next => next;

    public static int Read() => First;
}
