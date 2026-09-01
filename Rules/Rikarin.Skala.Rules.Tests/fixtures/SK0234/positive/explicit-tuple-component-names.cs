public static class People {
    public static (string Name, int Age) Create(string name, int age) {
        (string Name, int Age) person = (Name: name, Age: age);
        return person;
    }
}
