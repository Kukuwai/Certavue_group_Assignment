using System;
using System.Collections.Generic;
using System.Linq;

public class Project
{
    private static int idCounter = 0; 
    public int id {get;}
    public string name {get; set;}
    public HashSet<Person> people { get; } = new();
    public HashSet<int> originalPeopleIds { get; } = new(); 
    public int startDate {get; set;}
    public int endDate {get; set;} 
    public int duration {get; set;}
    public int hoursNeeded {get; set;}

    public int capacityStartWeek {get; set;}
    public int capacityEndWeek {get; set;}
    public int capacity {get; set;}
    public double FixedInitialSpan { get; set; }
    public double InitialBaselineSpan { get; set; } = 0;

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

    // --- 修复版 updateCapacity ---
    public void updateCapacity()
    {
        int? earliest = null;
        int? latest = null;

        foreach (Person p in this.people)
        {
            // 修复：使用 List<int>
            if (!p.projects.TryGetValue(this, out List<int> weeksForProject))
            {
                continue;   
            }

            foreach (int week in weeksForProject)
            {
                if (earliest == null || week < earliest) earliest = week;
                if (latest == null || week > latest) latest = week;
            }
        }

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
            capacity = (capacityEndWeek - capacityStartWeek) + 1;
        }
    }

    // --- 修复版 durationProjectFinder ---
    public int durationProjectFinder()
    {
        int? earliest = null;
        int? latest = null;

        foreach (Person p in this.people)
        {
            // 修复：使用 List<int>
            if (!p.projects.TryGetValue(this, out List<int> weeksForProject))
            {
                continue;   
            }

            foreach (int week in weeksForProject)
            {
                if (earliest == null || week < earliest) earliest = week;
                if (latest == null || week > latest) latest = week;
            }
        }

        if (earliest == null || latest == null)
        {
            capacityStartWeek = 0;
            capacityEndWeek = 0;
            capacity = 0;
        }
        else
        {
            this.capacity = (latest.Value - earliest.Value) + 1;
            this.FixedInitialSpan = this.capacity;
        }
        return capacity;
    }

    public void ReplaceStaff(Person person, Person oldperson)
    {

        if (this.people.Contains(oldperson))
        {
            this.people.Remove(oldperson);
            this.people.Add(person);
        }
    }

    public List<Person> getPeopleOnProject()
    {
        return new List<Person>(this.people);
    }

    // --- 修复版 printPeopleOnProject ---
    public void printPeopleOnProject()
    {
        foreach (Person p in people)
        {
            // 修复：使用 List<int> 并且移除不存在的 projectWeekHours
            if (p.projects.TryGetValue(this, out List<int> weeksForProject))
            {

                weeksForProject.Sort(); 
                string weeksStr = string.Join(", ", weeksForProject);
                Console.WriteLine($"{p.name} | {p.role} | {this.name} | Weeks: [{weeksStr}]");
            }
        }
    }
}