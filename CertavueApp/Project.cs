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
    public int hoursNeeded {get; set;}

    public Project(string name, int startDate, int endDate, int hoursNeeded)
    {
        id = ++idCounter;
        this.name = name;
        this.hoursNeeded = hoursNeeded;
        this.endDate = endDate;     
        this.startDate = startDate;
    }
    
}