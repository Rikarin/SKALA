using System;

public static class Copying {
    public static Action Duplicate(Action original) {
        Action copy = new Action(original);
        return copy;
    }
}
