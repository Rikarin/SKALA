using System;

public abstract class ApplicationLayerException : Exception {
    protected ApplicationLayerException(string message) : base(message) { }
}

public sealed class OrderNotFoundException : ApplicationLayerException {
    public OrderNotFoundException(string message) : base(message) { }
}
