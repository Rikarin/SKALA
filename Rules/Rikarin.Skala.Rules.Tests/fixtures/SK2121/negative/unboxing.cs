// Unboxing to a nullable value type is the other genuine `as`: it fails whenever the box holds
// something else.
sealed class Consumer {
    public int? Unbox(object value) => value as int?;

    public System.DayOfWeek? Day(object value) => value as System.DayOfWeek?;
}
