// ⚠ The assignment census reads one declaration. A type split across parts has writes this
// analysis cannot see, so it is declined rather than guessed at.
public partial class Split {
    public int Maximum { get; } = 1;
}

public partial class Split {
    public int Other => 2;
}
