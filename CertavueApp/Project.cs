using System.Dynamic;
using static Person;

public class Project
{
    private static int idCounter = 0; 
    public int id {get;}
    public string name {get; set;}
    public HashSet<Person> people { get; } = new();
    public int startDate {get; set;}
    public int endDate {get; set;}
    public int duration {get; set;}
    public Dictionary<int, int> totalResource { get; set; }

    public Project(string name, int startDate, int endDate)
    {
        id = ++idCounter;
        this.name = name;
        this.totalResource = new Dictionary<int,int>();
        this.endDate = endDate;     
        this.startDate = startDate;
    }
}