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
            int bestShift = 0;
            int bestConflicts = int.MaxValue;

            foreach (int shift in state.GetValidShifts(project))
            {
                int conflicts = CountConflicts(state, project, shift);
                if (conflicts < bestConflicts)
                {
                    bestConflicts = conflicts;
                    bestShift = shift;
                }
            }

            state.ApplyShift(project, bestShift);
            //Console.WriteLine(project.name + " -> shift " + bestShift + ", conflicts " + bestConflicts);
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

    //Counts how many conflicts someone has
    private int CountConflicts(ScheduleState state, Project project, int shift)
    {
        int conflicts = 0;
        foreach (var (personId, week) in state.GetFootprintForShift(project, shift))
        {
            if (state.PersonWeekGrid.GetValueOrDefault((personId, week)) > 0)
                conflicts++;
        }
        return conflicts;
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
    public int GetDuration(Project p) => _window[p].end - _window[p].start + 1;
    public int GetShift(Project p) => _shift[p];
    public void SetShift(Project p, int shift) => _shift[p] = shift;

    //finds all valid shifts for each project 
    public List<int> GetValidShifts(Project p)
    {

        var weeks = p.people
            .SelectMany(person => person.projects[p])
            .Distinct()
            .OrderBy(w => w)
            .ToList();

        if (weeks.Count == 0)
        {
            return new List<int> { 0 }; //Was getting an error for no movement allowed projects. I am not sure why this fixes it but please don't remove it
        }
        int baselineStart = weeks.First();
        int duration = weeks.Count;

        int earliestStart = _window[p].start + 1;
        int latestEnd = _window[p].end - 1;

        int minShift = earliestStart - baselineStart;
        int maxShift = latestEnd - (baselineStart + duration - 1);

        if (maxShift < minShift)
        {
            return new List<int>();
        }
        return Enumerable.Range(minShift, maxShift - minShift + 1).ToList();
    }

    //This is what actually moves the weeks. It takes a project and shift and moves ever person's weeks by the shift #
    public IEnumerable<(int personId, int week)> GetFootprintForShift(Project p, int shift)
    {
        foreach (var person in p.people)        //loops anyone on a project
        {
            if (!person.projects.TryGetValue(p, out var weeks))
            {
                continue;       //not skipping people without weeks on that project was giving an error so this fixed it 
            }
            foreach (var week in weeks)
            {
                yield return (person.id, week + shift);

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
        foreach (var (personId, week) in GetFootprintForShift(p, shift))
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
        foreach (var (personId, week) in GetFootprintForShift(p, shift))
        {
            if (week is < 1 or > Weeks) continue;
            var key = (personId, week);
            PersonWeekGrid[key] = PersonWeekGrid.GetValueOrDefault(key) + 1;
        }
    }
}
