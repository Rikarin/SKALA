namespace Sales {
    sealed class Ticket { }
}

namespace Support {
    sealed class Ticket { }
}

static class Route {
    // Two source types share the simple name, so a fix would have to pick one.
    public static bool IsTicket(object entity) => entity.GetType().Name == "Ticket";
}
