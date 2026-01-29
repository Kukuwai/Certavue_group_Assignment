using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Thin entry-point: run the greedy optimizer using already-loaded people/projects.
/// No file I/O, no changes to other classes.
/// </summary>
public class GreedyAlg
{
    public void StartGreedy(List<Person> people, List<Project> projects)
    {
        var state = new ScheduleState(people, projects);
        new GreedyAlgorithm().BuildGreedySchedule(state);
    }
}

/// <summary>
/// Tracks shifts and person-week occupancy, built from existing Project data.
/// Expects each Project to have startDate/endDate set and people populated.
/// </summary>
public class ScheduleState
{
    private const int Weeks = 52;

    public List<Person> People { get; }
    public List<Project> Projects { get; }
    public Dictionary<(int personId, int week), int> PersonWeekGrid { get; } = new();

    private readonly Dictionary<Project, (int start, int end)> _window;
    private readonly Dictionary<Project, int> _shift;

    public ScheduleState(List<Person> people, List<Project> projects)
    {
        People = people;
        Projects = projects;
        _window = projects.ToDictionary(p => p, p => (p.startDate, p.endDate));
        _shift = projects.ToDictionary(p => p, _ => 0);
        RebuildGrid();
    }

    public int GetDuration(Project p) => _window[p].end - _window[p].start + 1;
    public int GetShift(Project p) => _shift[p];
    public void SetShift(Project p, int shift) => _shift[p] = shift;

    public List<int> GetValidShifts(Project p)
    {
        var (start, end) = _window[p];
        int min = 1 - start;
        int max = Weeks - end;
        return Enumerable.Range(min, max - min + 1).ToList();
    }

    public (int origStart, int origEnd, int minShift, int maxShift) GetConstraints(Project p)
    {
        var (start, end) = _window[p];
        var valid = GetValidShifts(p);
        return (start, end, valid.First(), valid.Last());
    }

    public int GetCurrentStartWeek(Project p) => _window[p].start + GetShift(p);
    public int GetCurrentEndWeek(Project p) => _window[p].end + GetShift(p);

    public IEnumerable<(int personId, int week)> GetFootprintForShift(Project p, int shift)
    {
        foreach (var person in p.people)
        {
            for (int w = _window[p].start; w <= _window[p].end; w++)
            {
                yield return (person.id, w + shift);
            }
        }
    }

    public void RebuildGrid()
    {
        PersonWeekGrid.Clear();
        foreach (var p in Projects) AddProjectToGrid(p);
    }

    internal void RemoveProjectFromGrid(Project p)
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

    public void ApplyShift(Project p, int shift)
    {
        RemoveProjectFromGrid(p);
        SetShift(p, shift);
        AddProjectToGrid(p);
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

/// <summary>Greedy scheduling logic (concise).</summary>
public class GreedyAlgorithm
{
    public void BuildGreedySchedule(ScheduleState state)
    {
        foreach (var p in state.Projects) state.SetShift(p, 0);
        state.RebuildGrid();

        var sorted = state.Projects
            .OrderByDescending(p => p.people.Count * state.GetDuration(p))
            .ToList();

        foreach (var project in sorted)
        {
            state.RemoveProjectFromGrid(project); // avoid self-count
            int bestShift = FindBestShift(state, project);
            state.ApplyShift(project, bestShift);
        }

        PrintFinalMetrics(state);
    }

    private int FindBestShift(ScheduleState state, Project project)
    {
        var shifts = state.GetValidShifts(project);
        if (shifts.Count == 0) return 0;

        int best = shifts[0];
        var bestM = EvaluateShift(state, project, best);

        foreach (var s in shifts.Skip(1))
        {
            var m = EvaluateShift(state, project, s);
            if (m.IsBetterThan(bestM)) { best = s; bestM = m; }
        }
        return best;
    }

    private ShiftMetrics EvaluateShift(ScheduleState state, Project project, int shift)
    {
        int overlaps = 0, doubleBooked = 0;

        foreach (var (personId, week) in state.GetFootprintForShift(project, shift))
        {
            if (week is < 1 or > 52) continue;
            int count = state.PersonWeekGrid.GetValueOrDefault((personId, week));
            if (count >= 1) doubleBooked++;
            overlaps += count;
        }

        return new ShiftMetrics
        {
            ExtraOverlaps = overlaps,
            DoubleBookedPersonWeeks = doubleBooked,
            ShiftDistance = Math.Abs(shift)
        };
    }

    private ScheduleMetrics CalculateMetrics(ScheduleState state)
    {
        int extra = 0, doubleWeeks = 0;
        foreach (var count in state.PersonWeekGrid.Values)
        {
            if (count > 1)
            {
                extra += count - 1;
                doubleWeeks++;
            }
        }

        int totalWeeks = state.PersonWeekGrid.Count;
        double pct = totalWeeks == 0 ? 100 : (double)(totalWeeks - doubleWeeks) / totalWeeks * 100;
        int moved = state.Projects.Count(p => state.GetShift(p) != 0);
        int distance = state.Projects.Sum(p => Math.Abs(state.GetShift(p)));

        return new ScheduleMetrics
        {
            ExtraOverlaps = extra,
            DoubleBookedPersonWeeks = doubleWeeks,
            TotalPersonWeeks = totalWeeks,
            PercentNotDoubleBooked = pct,
            MovedProjects = moved,
            TotalShiftDistance = distance
        };
    }

    private void PrintFinalMetrics(ScheduleState state)
    {
        var m = CalculateMetrics(state);
        Console.WriteLine($"Conflicts: {m.DoubleBookedPersonWeeks} | Not double-booked: {m.PercentNotDoubleBooked:F1}% | Shift distance: {m.TotalShiftDistance}");
    }
}

public class ShiftMetrics
{
    public int ExtraOverlaps { get; set; }
    public int DoubleBookedPersonWeeks { get; set; }
    public int ShiftDistance { get; set; }

    public bool IsBetterThan(ShiftMetrics other) =>
        ExtraOverlaps != other.ExtraOverlaps ? ExtraOverlaps < other.ExtraOverlaps :
        DoubleBookedPersonWeeks != other.DoubleBookedPersonWeeks ? DoubleBookedPersonWeeks < other.DoubleBookedPersonWeeks :
        ShiftDistance < other.ShiftDistance;
}

public class ScheduleMetrics
{
    public int ExtraOverlaps { get; set; }
    public int DoubleBookedPersonWeeks { get; set; }
    public int TotalPersonWeeks { get; set; }
    public double PercentNotDoubleBooked { get; set; }
    public int MovedProjects { get; set; }
    public int TotalShiftDistance { get; set; }
}
