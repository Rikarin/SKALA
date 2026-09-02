public static class CommentInTheHead {
    public static int? Go(int value) {
        int? wrapped = new int?(/* the wrapper is deliberate */ value);
        return wrapped;
    }
}
