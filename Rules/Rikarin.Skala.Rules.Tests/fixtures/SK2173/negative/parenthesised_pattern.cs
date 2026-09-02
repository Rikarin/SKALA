// The parentheses are a grouping choice the fix would have to unpick, so the whole shape is declined.
class C {
    bool M(object? result) => result is not ({ });
}
