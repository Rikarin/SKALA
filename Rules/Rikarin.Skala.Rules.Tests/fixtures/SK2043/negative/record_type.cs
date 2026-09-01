record Ticket {
    public string Holder { get; set; } = "";

    public override int GetHashCode() => Holder.GetHashCode();
}
