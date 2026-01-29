using System;
using System.Collections.Generic;
using System.Linq;

public class GreedyAlg
{
    public ScheduleState StartGreedy(List<Person> people, List<Project> projects)
    {
        //state keeps track of shifts
        var state = new ScheduleState(people, projects);
        BuildGreedySchedule(state);
        return state; // I added this for to work with finding conflicts. 
    }

    public void BuildGreedySchedule(ScheduleState state)
    {
        Console.WriteLine("Greedy algorithm running");

        //Harder weeks go first aka projects with most X's
        var ordered = state.Projects
            .OrderByDescending(p => p.people.Count * state.GetDuration(p))
            .ToList();


        //takes the ordered list and looks for each ones best shift
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
            Console.WriteLine(project.name + " -> shift " + bestShift + ", conflicts " + bestConflicts);
        }

        int doubleBooked = state.PersonWeekGrid.Count(kv => kv.Value > 1);
        Console.WriteLine("Done. Double-booked weeks: " + doubleBooked);
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

//Since we designed it without a schedule class in the loader we keep track here
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

    public IEnumerable<(int personId, int week)> GetFootprintForShift(Project p, int shift)
    {
        foreach (var person in p.people)
            for (int w = _window[p].start; w <= _window[p].end; w++)
            {
                yield return (person.id, w + shift);

            }
    }

    public void RebuildGrid()
    {
        PersonWeekGrid.Clear();
        foreach (var p in Projects)
        {
            AddProjectToGrid(p);

        }
    }

    public void ApplyShift(Project p, int shift)
    {
        RemoveProjectFromGrid(p);
        SetShift(p, shift);
        AddProjectToGrid(p);
    }

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
