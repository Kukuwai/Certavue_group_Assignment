

public class Person
{
    private static int idCounter = 0;
    public int id { get; }
    public string name { get; set; }
    public Dictionary<Project, List<int>> projects { get; } = new();
    public int capacity { get; set; }
    public string role { get; set; }

    public Person(string name, string role, int capacity)
    {
        id = ++idCounter;
        this.name = name;
        this.role = role;
        this.capacity = capacity;
    }

    // Returns Dictionary<Project, List<int>>. The method returns projects and weeks consistent with the tests in FinderTest. 
    public Dictionary<Project, List<int>> GetProjectForPerson()
    {
        return new Dictionary<Project, List<int>>(this.projects);
    }
}
