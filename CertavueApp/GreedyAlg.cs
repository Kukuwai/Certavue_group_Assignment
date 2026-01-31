using System;
using System.Collections.Generic;
using System.Linq;

public class GreedyAlg
{
    public void StartGreedy(List<Person> people, List<Project> projects)
    {
        //state keeps track of shifts
        var state = new ScheduleState(people, projects);
        BuildGreedySchedule(state);
    }

    public void BuildGreedySchedule(ScheduleState state)
    {
        Console.WriteLine("Greedy algorithm running");
        //grabs stats before algorithm
        int totalAssignments = state.PersonWeekGrid.Values.Sum();
        int nonConflictAssignments = state.PersonWeekGrid
            .Where(kv => kv.Value == 1)
            .Sum(kv => kv.Value);
        double pctNotDoubleBooked = totalAssignments == 0
            ? 100
            : (double)nonConflictAssignments / totalAssignments * 100;

        Console.WriteLine("Before running. Total assignments=" + totalAssignments
            + " % not double-booked=" + pctNotDoubleBooked.ToString("0.##"));

        //Harder weeks go first aka projects with most X's
        var ordered = state.Projects
            .OrderByDescending(p => p.people.Count * state.GetDuration(p))
            .ToList();


        //takes the ordered list and looks for each ones best shift and prints stats 
        foreach (var project in ordered)
        {
            int bestShift = 0; //starts by assuming no shift is best
            var bestMetrics = (int.MaxValue, int.MaxValue, int.MaxValue);

            foreach (int shift in state.GetValidShifts(project))
            {
                var metrics = EvaluateShift(state, project, shift);

                //prefers fewer shifts, then fewer new double books, if it is a tie do the smaller move
                //this is only used if extra overlaps ties with the new double bookings
                if (metrics.extraOverlaps < bestMetrics.Item1 ||
                    (metrics.extraOverlaps == bestMetrics.Item1 && metrics.newDoubleBooked < bestMetrics.Item2) ||
                    (metrics.extraOverlaps == bestMetrics.Item1 && metrics.newDoubleBooked == bestMetrics.Item2 && metrics.shiftDistance < bestMetrics.Item3))
                {
                    bestShift = shift;
                    bestMetrics = metrics;
                }
            }

            state.ApplyShift(project, bestShift);
        }

        totalAssignments = state.PersonWeekGrid.Values.Sum();
        nonConflictAssignments = state.PersonWeekGrid
            .Where(kv => kv.Value == 1)
            .Sum(kv => kv.Value);
        pctNotDoubleBooked = totalAssignments == 0
            ? 100
            : (double)nonConflictAssignments / totalAssignments * 100;

        Console.WriteLine("Done. Total assignments=" + totalAssignments
            + " % not double-booked=" + pctNotDoubleBooked.ToString("0.##"));
    }

    //Counts how many conflicts someone has, returns extra overlaps, new double booked weeks, and shift distance
    //input is the state, project being evaluated, and the shift being evaluated
    public (int extraOverlaps, int newDoubleBooked, int shiftDistance) EvaluateShift(
    ScheduleState state, Project project, int shift)
    {
        int extraOverlaps = 0;
        int newDoubleBooked = 0;

        foreach (var (personId, week) in state.GetGrid(project, shift))
        {
            int currentCount = state.PersonWeekGrid.GetValueOrDefault((personId, week), 0);

            if (currentCount > 0)
            {
                extraOverlaps++;  //Has logic to count worse double bookings

                if (currentCount == 1)
                {
                    newDoubleBooked++;  //Week is double booked
                }
            }
        }

        return (extraOverlaps, newDoubleBooked, Math.Abs(shift));
    }
}

//Since we designed it without a schedule class in the loader we keep track here making the "grid" in the CSV form
public class ScheduleState
{
    private const int Weeks = 52;
    public List<Person> People { get; } //list of all people in that schedule
    public List<Project> Projects { get; } //list of projects
    public Dictionary<(int personId, int week), int> PersonWeekGrid { get; } = new(); //dictionary to track projects a person has each week

    private readonly Dictionary<Project, (int start, int end)> _window; //considers start and end dates to know what valid moves are 
    private readonly Dictionary<Project, int> _shift; //current shift of project

    public ScheduleState(List<Person> people, List<Project> projects)
    {
        People = people;
        Projects = projects;
        _window = projects.ToDictionary(p => p, p => (p.startDate, p.endDate));
        _shift = projects.ToDictionary(p => p, _ => 0);
        RebuildGrid();
    }


    //duration should be from start date week to end date week -1 but need to check my maths on this one on paper
    public int GetDuration(Project p)
    {
        var weeks = p.people
            .SelectMany(person => person.projects[p]) //weeks on proj for this person
            .Distinct() //remove duplicate weeks ie 2 people working week 10
            .ToList();

        int leftmost = weeks.Min();
        int rightmost = weeks.Max();
        int duration = rightmost - leftmost + 1;

        return duration;
    }
    public int GetShift(Project p) => _shift[p];
    public void SetShift(Project p, int shift) => _shift[p] = shift;

    //finds all valid shifts for each project 
    public List<int> GetValidShifts(Project p)
    {
        var weeks = p.people //all work weeks on project
            .SelectMany(person => person.projects[p])  //returns weeks a person works
            .Distinct()  //removes duplicates aka 2 people workinf
            .ToList();

        if (weeks.Count == 0)
        {
            return new List<int> { 0 }; //breaks without this do not remove
        }

        int baselineStart = weeks.Min();   //earliest x
        int baselineEnd = weeks.Max();     //latest x

        int duration = baselineEnd - baselineStart + 1; //how wide the project is or long

        int earliestStart = _window[p].start + 1;  //valid moves left
        int latestEnd = _window[p].end - 1;        //valid moves right

        int minShift = Math.Max(-baselineStart + 1, earliestStart - baselineStart); //how far left can it go
        int maxShift = Math.Min(52 - baselineEnd, latestEnd - baselineEnd); // how far right

        if (maxShift < minShift)
        {
            return new List<int>();  //gave an error on some if constraints didn't allow movement
        }

        return Enumerable.Range(minShift, maxShift - minShift + 1).ToList(); //list of valid shfts
    }


    //This is what actually moves the weeks. It takes a project and shift and moves ever person's weeks by the shift #
    public IEnumerable<(int personId, int week)> GetGrid(Project p, int shift)
    {
        foreach (var person in p.people) //every person in a project looped
        {
            if (person.projects.ContainsKey(p))  //Only does work for that week people
            {
                foreach (var originalWeek in person.projects[p])  //loops every week that person is on the project and makes the + or - shift
                {
                    int shiftedWeek = originalWeek + shift;
                    if (shiftedWeek >= 1 && shiftedWeek <= 52) //out of bounds protector. Was getting an error on edges prior
                    {
                        yield return (person.id, shiftedWeek);
                    }
                }
            }
        }
    }

    //used at beginning to build the grid and add projects to it
    public void RebuildGrid()
    {
        PersonWeekGrid.Clear();
        foreach (var p in Projects)
        {
            AddProjectToGrid(p);

        }
    }

    //updates the grid with shifts
    public void ApplyShift(Project p, int shift)
    {
        RemoveProjectFromGrid(p);
        SetShift(p, shift);
        AddProjectToGrid(p);
    }
    //removes a project from the grid when it is being shifted

    private void RemoveProjectFromGrid(Project p)
    {
        int shift = GetShift(p);
        foreach (var (personId, week) in GetGrid(p, shift))
        {
            if (week is < 1 or > Weeks) continue;
            var key = (personId, week);
            if (!PersonWeekGrid.TryGetValue(key, out var count)) continue;
            if (--count == 0) PersonWeekGrid.Remove(key);
            else PersonWeekGrid[key] = count;
        }
    }

    //current shift added 
    private void AddProjectToGrid(Project p)
    {
        int shift = GetShift(p);
        foreach (var (personId, week) in GetGrid(p, shift))
        {
            if (week is < 1 or > Weeks) continue;
            var key = (personId, week);
            PersonWeekGrid[key] = PersonWeekGrid.GetValueOrDefault(key) + 1;
        }
    }
}
