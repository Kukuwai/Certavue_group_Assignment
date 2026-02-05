using System;
using System.Collections.Generic;
using System.Linq;

public class ScheduleHandler
{
    private readonly ScheduleState _state;
    private readonly AvailabilityFinder _finder;

    public ScheduleHandler(ScheduleState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _finder = new AvailabilityFinder(_state);
    }

    // --- First stage : get newest state ---

    public ScheduleState GetCurrentState() => _state;
    public AvailabilityFinder GetFinder() => _finder;


    /// get possibity int that valid(not out overall timeline)
    public List<int> GetValidOptions(Project p)
    {
        return _state.GetValidShifts(p);
    }

    //----return each person's gap period
    public Dictionary<string, string> GetGapsPerPerson()
   {
    var report = new Dictionary<string, string>();
    foreach (var p in _state.People)
    {
        List<int> freeWeeks = GetAvailableWeeksForPerson(p.name);
        
        report[p.name] = string.Join(", ", freeWeeks);
    }
    return report;
    }

    public ShiftScore EvaluateMove(Project p, int candidateShift)
    {
        int currentShift = _state.GetShift(p);
        
       
        if (candidateShift == currentShift)
        {
            return new ShiftScore { DeltaDoubleBooked = 0, OverlapAfter = 0, ShiftDistance = 0 };
        }

        
        var currentCells = _state.GetGrid(p, currentShift);
        var candidateCells = _state.GetGrid(p, candidateShift);

        var touchedKeys = currentCells.Union(candidateCells).Distinct();

        int delta = 0;
        int overlapAfter = 0;


        foreach (var key in touchedKeys)
        {
            _state.PersonWeekGrid.TryGetValue(key, out int currentTotalCount);

            bool isOccupiedInCurrent = currentCells.Any(c => c.Equals(key));
            int baseCountWithoutProject = isOccupiedInCurrent ? currentTotalCount - 1 : currentTotalCount;

            bool isOccupiedInCandidate = candidateCells.Any(c => c.Equals(key));
            int newCountWithCandidate = isOccupiedInCandidate ? baseCountWithoutProject + 1 : baseCountWithoutProject;

            if (currentTotalCount >= 2) delta -= 1; 
            if (newCountWithCandidate >= 2)
            {
                delta += 1; 
                overlapAfter++; 
            }
        }

        return new ShiftScore
        {
            DeltaDoubleBooked = delta,
            OverlapAfter = overlapAfter,
            ShiftDistance = Math.Abs(candidateShift - currentShift)
        };
    }


    //--- return specifical overloaded perosn in which week is overloaded
    public string GetOverloadDetails(string personName)
    {
    var person = _state.People.FirstOrDefault(p => p.name == personName);
    if (person == null) return "Person not found";

    var overloadWeeks = new List<int>();

    for (int week = 1; week <= 52; week++)
    {
        if (GetPersonWorkload(personName, week) > 1)
        {
            overloadWeeks.Add(week);
        }
    }

    return FormatWeeksIntoRanges(overloadWeeks); 
    }


   //----return conflicts detail: exist conflict in which project whose overloap and which week overloap
    public List<string> GetDetailedConflictList()
{
    var details = new List<string>();
    
    foreach (var entry in _state.PersonWeekGrid.Where(kv => kv.Value >= 2))
    {
        var key = entry.Key; 
        var person = _state.People.First(p => p.id == key.PersonId);
        
        var conflictingProjects = _state.Projects
            .Where(proj => _state.GetGrid(proj, _state.GetShift(proj))
            .Any(cell => cell.PersonId == key.PersonId && cell.Week == key.Week))
            .Select(proj => proj.name)
            .ToList();

        details.Add($"Week {key.Week} | {person.name} | Projects: {string.Join(" & ", conflictingProjects)}");
    }
    return details;
}


    public void ExecuteMove(Project p, int newShift)
    {
        _state.ApplyShift(p, newShift);
    }


public string GenerateSummary()
{
    // 1. Calculations
    int conflictCells = _state.PersonWeekGrid.Values.Count(v => v >= 2);
    int totalOccupiedCells = _state.PersonWeekGrid.Count;
    double successPct = totalOccupiedCells == 0 ? 100 : (double)_state.PersonWeekGrid.Values.Count(v => v == 1) / totalOccupiedCells * 100;

    StringBuilder sb = new StringBuilder();
    sb.AppendLine("======= 📅 PROJECT SCHEDULE DIAGNOSTIC REPORT =======");
    sb.AppendLine($"[Status] Total Conflicts: {conflictCells} | Success Rate: {successPct:0.##}%");
    
    // 2. Critical Overloads (Who is busy)
    sb.AppendLine("\n[Critical Overloads]");
    var overloadedStaff = _state.People
        .Select(p => new { p.name, Weeks = GetOverloadWeeksList(p.name) })
        .Where(x => x.Weeks.Any())
        .ToList();

    if (overloadedStaff.Any()) {
        foreach (var s in overloadedStaff) 
            sb.AppendLine($"  ⚠️ {s.name}: {s.Weeks.Count} weeks conflicted ({FormatWeeksIntoRanges(s.Weeks)})");
    } else {
        sb.AppendLine("  ✅ No resource overloads detected.");
    }

    // 3. Conflict Breakdown (New - What is happening)
    sb.AppendLine("\n[Conflict Breakdown - Project Overlaps]");
    var conflictDetails = GetDetailedConflictList(); // 调用明细方法
    if (conflictDetails.Any()) {
        foreach (var line in conflictDetails) sb.AppendLine($"  ❌ {line}");
    } else {
        sb.AppendLine("  ✅ All project assignments are isolated.");
    }

    // 4. Resource Capacity (Future planning)
    sb.AppendLine("\n[Resource Capacity / Gaps]");
    foreach (var p in _state.People) {
        var gaps = _finder.GetAvailableWeeksForPerson(p.name);
        sb.AppendLine($"  💡 {p.name}: {gaps.Count} weeks free (Windows: {FormatWeeksIntoRanges(gaps)})");
    }

    sb.AppendLine("\n====================================================");
    return sb.ToString();
}

    public ScheduleState Finalize()
    {
        return _state;
    }
}


public class ShiftScore
{
    public int DeltaDoubleBooked { get; set; } 
    public int OverlapAfter { get; set; }     
    public int ShiftDistance { get; set; }     
}


