

public class Person
{
    private static int idCounter = 0; 
    public int id {get; }
    public string name {get; set;}
    public Dictionary<Project, List<int>> projects { get; } = new();
    public int capacity { get; set;}

    public Person(string name, int capacity)
    {
        id = ++idCounter;
        this.name = name;
        this.capacity = capacity;
    }

}
