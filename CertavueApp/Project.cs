using System.Dynamic;
using static Person;

public class Project
{
    private static int idCounter = 0;
    public int id { get; }
    public string name { get; set; }
    public HashSet<Person> people { get; } = new();
    public int startDate { get; set; }
    public int endDate { get; set; }
    public int duration { get; set; }
    public int hoursNeeded { get; set; }

    // Constructor with hoursNeeded consistent with test in FinderTest. The Loader object will need to be updated.
    public Project(string name, int startDate, int endDate, int hoursNeeded)
    {
        id = ++idCounter;
        this.name = name;
        this.hoursNeeded = hoursNeeded;
        this.endDate = endDate;
        this.startDate = startDate;
        this.hoursNeeded = hoursNeeded;
    }
    // Constructor without hoursNeeded this is consistent with our current Loader.
    public Project(string name, int startDate, int endDate)
    {
        id = ++idCounter;
        this.name = name;
        this.endDate = endDate;
        this.startDate = startDate;
    }

    // The new method added here. Returns people and their weeks on this project
    public Dictionary<string, List<int>> GetPeopleForProject()
    {
        var result = new Dictionary<string, List<int>>();

        foreach (var person in this.people)
        {
            if (person.projects.ContainsKey(this))
            {
                result[person.name] = person.projects[this];
            }
        }

        return result;
    }

}