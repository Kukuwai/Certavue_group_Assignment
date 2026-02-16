

public class Person
{
    private static int idCounter = 0;
    public int id { get; }
    public string name { get; set; }
    public Dictionary<Project, Dictionary<int, int>> projects { get; } = new();
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


    public List<Project> getProjectForPerson()
    {
        List<Project> projectsForPerson = new List<Project>();
        foreach (var entry in this.projects)
        {
            projectsForPerson.Add(entry.Key);
        }
        return projectsForPerson;
    }

    public void printProjectsForPerson()
    {
        foreach (var entry in projects)
        {
            Console.WriteLine($"{entry.Key.name} | {this.name}");
        }
    }

    public int getHoursForProjecForWeek(Project project, int week)
    {
        var weekkey = projects.GetValueOrDefault(project);
        return weekkey.GetValueOrDefault(week);
    }

}

