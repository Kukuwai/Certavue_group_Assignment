

public class Person
{
    private static int idCounter = 0;
    public int id { get; }
    public string name { get; set; }
    public Dictionary<Project, List<int>> projects { get; } = new();
    public int capacity { get; set; }
    public string role { get; set; }

    public Person(string name, string role)
    {
        id = ++idCounter;
        this.name = name;
        this.role = role;
    }

    public Person(string name, int capacity, string role)
    {
        id = ++idCounter;
        this.name = name;
        this.capacity = capacity;
        this.role = role;
    }
}
