// Static code writing static state is what static state is for.
sealed class Widget {
    static int created;

    static Widget() {
        created = 0;
    }

    public static Widget Create() {
        created++;
        return new Widget();
    }
}
