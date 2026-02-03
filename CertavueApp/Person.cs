

public class Person
{
    private static int idCounter = 0; 
    public int id {get; }
    public string name {get; set;}
    public Dictionary<Project, List<int>> projects { get; } = new();

    public Person(string name)
    {
        id = ++idCounter;
        this.name = name;
    }

}
