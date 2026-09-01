using System;

sealed class Ticket {
    public int Id { get; init; }

    public string Holder { get; set; } = "";

    public override bool Equals(object? other) => other is Ticket ticket && ticket.Id == Id;

    public override int GetHashCode() => HashCode.Combine(Id, Holder);
}
