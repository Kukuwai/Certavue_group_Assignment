using System.Dynamic;
using System.Security.Cryptography.X509Certificates;
using Google.OrTools.LinearSolver;
using static Person;

public class Project
{
    private static int idCounter = 0;
    public int id { get; }
    public string name { get; set; }
    public HashSet<Person> people { get; } = new();
    public HashSet<int> originalPeopleIds { get; } = new();
    public int startDate { get; set; }
    public int endDate { get; set; }
    public int duration { get; set; }
    public int hoursNeeded { get; set; }

    public int capacityStartWeek {get; set;}
    public int capacityEndWeek {get; set;}
    public int capacity {get; set;}
    public int OriginalDurationSpan {get; set;}

    public Project(string name, int startDate, int endDate, int hoursNeeded)
    {
        id = ++idCounter;
        this.name = name;
        this.hoursNeeded = hoursNeeded;
        this.endDate = endDate;
        this.startDate = startDate;
    }

    public Project(string name, int startDate, int endDate)
    {
        id = ++idCounter;
        this.name = name;
        this.endDate = endDate;
        this.startDate = startDate;
    }

    public void updateCapacity()
    {

        int? earliest = null;
        int? latest = null;
        // find the weeks where people are assigned to project.
        Dictionary<int, int> weeks = new Dictionary<int, int>();
        foreach (Person p in this.people)
        {
            // check if project is contained within dictiornary and that it onl has one person
            if (!p.projects.TryGetValue(this, out Dictionary<int, int> weeksForProject))
            {
                // skip if not
                continue;
            }
            // go through each week project is in
            foreach (int week in weeksForProject.Keys)
            {
                // get earliest week
                if (earliest == null || week < earliest)
                {
                    earliest = week;
                }
                // get latest week
                if (latest == null || week > latest)
                {
                    latest = week;
                }
            }
        }
        // default to 0 if invalid
        if (earliest == null || latest == null)
        {
            capacityStartWeek = 0;
            capacityEndWeek = 0;
            capacity = 0;
        }
        else
        {
            capacityStartWeek = earliest.Value;
            capacityEndWeek = latest.Value;
            capacity = capacityEndWeek - capacityStartWeek + 1;
        }
    }

    public int durationProjectFinder()
    {

        int? earliest = null;
        int? latest = null;
        // find the weeks where people are assigned to project.
        Dictionary<int, int> weeks = new Dictionary<int, int>();
        foreach (Person p in this.people)
        {
            // check if project is contained within dictiornary and that it onl has one person
            if (!p.projects.TryGetValue(this, out Dictionary<int, int> weeksForProject))
            {
                // skip if not
                continue;
            }
            // go through each week project is in
            foreach (int week in weeksForProject.Keys)
            {
                // get earliest week
                if (earliest == null || week < earliest)
                {
                    earliest = week;
                }
                // get latest week
                if (latest == null || week > latest)
                {
                    latest = week;
                }
            }
        }
        // default to 0 if invalid
        if (earliest == null || latest == null)
        {
            capacityStartWeek = 0;
            capacityEndWeek = 0;
            capacity = 0;
        }
        else
        {
            capacityStartWeek = earliest.Value;
            capacityEndWeek = latest.Value;
            capacity = capacityEndWeek - capacityStartWeek + 1;
        }
        return capacity;
    }

    public void ReplaceStaff(Person person, Person oldperson)
    {

        foreach (Person p in people)
        {
            if (p.Equals(person))
            {
                this.people.Remove(oldperson);
                this.people.Add(person);
            }
        }

    }

    public List<Person> getPeopleOnProject()
    {
        List<Person> peopleOnProject = new List<Person>();
        foreach (Person p in this.people)
        {
            peopleOnProject.Add(p);
        }
        return peopleOnProject;
    }

    public void printPeopleOnProject()
    {
        foreach (Person p in people)
        {
            Dictionary<int, int> projectWeekHours = p.projects.GetValueOrDefault(this);
            foreach (KeyValuePair<int, int> entry in projectWeekHours)
            {
                Console.WriteLine($"{p.name} | {p.role} | {this.name} | Week {entry.Key}: | {entry.Value}");
            }
        }
    }


}
