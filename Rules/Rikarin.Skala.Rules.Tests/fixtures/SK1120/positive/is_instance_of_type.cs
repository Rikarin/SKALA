using System.IO;

class InstanceOfType {
    public bool Test(object source) => typeof(Stream).IsInstanceOfType(source);
}
