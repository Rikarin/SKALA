class Draft {
    public int Status { get; set; }
}

class Review {
    public int Status { get; set; }
}

// Two different members that happen to share a name. Merging them is a different predicate.
class TypedAlternatives {
    public bool Editable(object d) => d is Draft { Status: 1 } or Review { Status: 2 };
}
