class Order {
    public int Id { get; set; }

    public string Name { get; set; } = "";
}

class Projections {
    public string Build(Order order) {
        var head = new { order.Id, order.Name };
        var tail = new { order.Name, order.Id };
        return head.ToString() + tail.ToString();
    }
}
