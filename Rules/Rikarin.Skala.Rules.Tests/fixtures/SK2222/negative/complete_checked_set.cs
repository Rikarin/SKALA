// Every operator that has a checked form, with its checked form. Nothing to report.
struct Careful {
    public long Value;

    public static Careful operator +(Careful a, Careful b) => new Careful { Value = a.Value + b.Value };

    public static Careful operator checked +(Careful a, Careful b) =>
        checked(new Careful { Value = a.Value + b.Value });

    public static Careful operator -(Careful a, Careful b) => new Careful { Value = a.Value - b.Value };

    public static Careful operator checked -(Careful a, Careful b) =>
        checked(new Careful { Value = a.Value - b.Value });

    public static Careful operator -(Careful a) => new Careful { Value = -a.Value };

    public static Careful operator checked -(Careful a) => checked(new Careful { Value = -a.Value });

    public static Careful operator ++(Careful a) => new Careful { Value = a.Value + 1 };

    public static Careful operator checked ++(Careful a) => checked(new Careful { Value = a.Value + 1 });

    public static explicit operator int(Careful a) => (int)a.Value;

    public static explicit operator checked int(Careful a) => checked((int)a.Value);
}
