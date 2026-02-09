using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class ScheduleHandler
{
    private readonly ScheduleState _state;
    private readonly AvailabilityFinder _finder;

    public ScheduleHandler(ScheduleState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _finder = new AvailabilityFinder(_state);
    }

//---------add fitness socre logic
    public class FitnessScore
    {
    public double ConflictWeight = 0.40;      // conflicts sorce
    public double MovementWeight = 0.20;      // movement sorce： measure weather a project move frequently to different time axis in order to reduce conflicts
    public double FocusWeight = 0.20;         // measure weather a person be shifted frequently
    public double ContinuityWeight = 0.10;    // measure weather a project has most continuity 
    public double DurationWeight = 0.10;      // project duration
    }

//-------method of calculate fitness score
    public double CalculateFitnessScore(ScheduleState state)
   {
    double conflictScore = GetConflictScore(state);     
    double movementScore = GetMovementScore(state);
    double focusScore = GetFocusScore(state);
    double continuityScore = GetContinuityScore(state);
    double durationScore = GetDurationScore(state);

    // Final weighted sum
    return (conflictScore * 0.4) + 
           (movementScore * 0.2) + 
           (focusScore * 0.2) + 
           (continuityScore * 0.1) + 
           (durationScore * 0.1);
    }

    private double GetConflictScore(ScheduleState state) {
    int totalSlots = state.PersonWeekGrid.Count;
    if (totalSlots == 0) return 1.0;
    // find conflicts grid
    int conflicts = state.PersonWeekGrid.Values.Count(v => v > 1);
    // normalization 
    return Math.Max(0, 1.0 - ((double)conflicts / totalSlots * 5)); // make sure punlish is outstanding
    }

    private double GetMovementScore(ScheduleState state) {
    // sum shift
    double totalShift = state.Projects.Sum(p => Math.Abs(state.GetShift(p)));
    // normalization socre
    double avgShift = totalShift / state.Projects.Count;
    return Math.Max(0, 1.0 - (avgShift / 4.0)); // if it near with 4 it will be 0
    }

    private double GetFocusScore(ScheduleState state) {
    // sum on average, how much projects each person takes on every week
    var multiTaskWeeks = state.PersonWeekGrid.Values.Count(v => v > 1);
    // normalization
    return Math.Max(0, 1.0 - ((double)multiTaskWeeks / state.PersonWeekGrid.Count));
   }

   private double GetContinuityScore(ScheduleState state) {
    double totalPenalty = 0;
    foreach(var p in state.Projects) {
        // count how many people in a project
        int peopleCount = p.people.Distinct().Count(); 
        if(peopleCount > 1) totalPenalty += (peopleCount - 1);
    }
    return Math.Max(0, 1.0 - (totalPenalty / state.Projects.Count));
   }  


   private double GetDurationScore(ScheduleState state)
{
    double totalScore = 0;
    foreach (var project in state.Projects)
    {
        // Get the current shift (offset) applied to this project by the algorithm
        int currentShift = state.GetShift(project);
        // Retrieve all occupied cells (person-weeks) for this project at its current position
        var projectCells = state.GetGrid(project, currentShift);
        
        if (!projectCells.Any()) continue;
        // Calculate the actual timeline span by finding the earliest start and latest end across all team members assigned to this project.
        int actualStart = projectCells.Min(c => c.Week);
        int actualEnd = projectCells.Max(c => c.Week);
        int actualSpan = (actualEnd - actualStart) + 1;
        // Determine the original planned duration as the socre for efficiency
        int plannedSpan = (project.endDate - project.startDate) + 1;
        // A score of 1.0 means the project is perfectly compact.
        double score = (double)plannedSpan / actualSpan;
        totalScore += Math.Min(1.0, score);
    }
    return state.Projects.Count > 0 ? totalScore / state.Projects.Count : 1.0;
}










//----------------------------------------
    public ScheduleState GetCurrentState() => _state;
    public AvailabilityFinder GetFinder() => _finder;


    // --- Gets a list of valid shift offsets that keep the project within its allowed timeframe.
    public List<int> GetValidOptions(Project p)
    {
        return _state.GetValidShifts(p);
    }

    // Returns a dictionary mapping each person to their available (free) week ranges.
    public Dictionary<string, string> GetGapsPerPerson()
    {
        var report = new Dictionary<string, string>();
        foreach (var p in _state.People)
        {
            List<int> freeWeeks = _finder.GetAvailableWeeksForPerson(p.name);
            report[p.name] = FormatWeeksIntoRanges(freeWeeks);
        }
        return report;
    }

    public ShiftScore EvaluateMove(Project p, int candidateShift)
    {
        int currentShift = _state.GetShift(p);

        int currentDoubleBooked = _state.PersonWeekGrid
                       .Where(kv => kv.Value >= 2)
                       .Sum(kv => kv.Value);
        if (candidateShift == currentShift)
        {


            int currentConflictCells = _state.PersonWeekGrid.Values.Count(v => v >= 2);

            double currentPenalty = (1000.0 * currentDoubleBooked) + (10.0 * currentConflictCells);

            return new ShiftScore
            {
                DeltaDoubleBooked = 0,
                OverlapAfter = currentConflictCells,
                ShiftDistance = 0,
                Fitness = 1.0 / (1.0 + currentPenalty)
            };
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
        int distance = Math.Abs(candidateShift - currentShift);

        currentDoubleBooked = _state.PersonWeekGrid
            .Where(kv => kv.Value >= 2)
            .Sum(kv => kv.Value);

        int projectedDoubleBooked = Math.Max(0, currentDoubleBooked + delta);

        double penalty = (1000.0 * projectedDoubleBooked) + (10.0 * overlapAfter) + distance;
        double fitness = 1.0 / (1.0 + penalty);

        return new ShiftScore
        {
            DeltaDoubleBooked = delta,
            OverlapAfter = overlapAfter,
            ShiftDistance = distance,
            Fitness = fitness
        };

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


    private List<int> GetOverloadWeeksList(string personName)
    {
        List<int> overloads = new List<int>();
        for (int w = 1; w <= 52; w++)
        {
            if (_finder.GetPersonWorkload(personName, w) > 1)
            {
                overloads.Add(w);
            }
        }
        return overloads;
    }

    private string FormatWeeksIntoRanges(List<int> weeks)
    {
        if (weeks == null || !weeks.Any()) return "None";
        var sortedWeeks = weeks.Distinct().OrderBy(w => w).ToList();

        var ranges = new List<string>();
        int start = sortedWeeks[0];
        int end = sortedWeeks[0];

        for (int i = 1; i < sortedWeeks.Count; i++)
        {
            if (sortedWeeks[i] == end + 1)
            {
                end = sortedWeeks[i];
            }
            else
            {
                ranges.Add(start == end ? $"{start}" : $"{start}-{end}");
                start = end = sortedWeeks[i];
            }
        }
        ranges.Add(start == end ? $"{start}" : $"{start}-{end}");
        return string.Join(", ", ranges);
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

        if (overloadedStaff.Any())
        {
            foreach (var s in overloadedStaff)
                sb.AppendLine($"  ⚠️ {s.name}: {s.Weeks.Count} weeks conflicted ({FormatWeeksIntoRanges(s.Weeks)})");
        }
        else
        {
            sb.AppendLine("  ✅ No resource overloads detected.");
        }

        // 3. Conflict Breakdown ( What is happening)
        sb.AppendLine("\n[Conflict Breakdown - Project Overlaps]");
        var conflictDetails = GetDetailedConflictList();
        if (conflictDetails.Any())
        {
            foreach (var line in conflictDetails) sb.AppendLine($"  ❌ {line}");
        }
        else
        {
            sb.AppendLine("  ✅ All project assignments are isolated.");
        }

        // 4. Resource Capacity (Future planning)
        sb.AppendLine("\n[Resource Capacity / Gaps]");
        foreach (var p in _state.People)
        {
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
    public double Fitness { get; set; }

}


