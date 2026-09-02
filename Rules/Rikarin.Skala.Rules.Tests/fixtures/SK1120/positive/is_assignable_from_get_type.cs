using System.IO;

class AssignableFrom {
    public bool Test(object source) => typeof(Stream).IsAssignableFrom(source.GetType());
}
