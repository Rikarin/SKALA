public static class Labelling {
    public static int Kind(object value) {
        switch (value) {
            case int _:
                return 1;

            case string _ when value is not null:
                return 2;

            default:
                return 0;
        }
    }
}
