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

    // the class to evaulate shift
    // summary : The Fitness Score is our overall target—it tells us how good the schedule is. 
    //But it doesn't tell us why it's bad.By adding RoleGapReport, we give the system 'diagnostic' power.
    // When the score drops, the $RoleGapReport$ tells the user: 'It's because you need 1 more Developer.' Without this, the user just sees a low score and won't know how to fix it.
    public class ShiftPerformance
    {
        public int Shift { get; set; }
        public double Score { get; set; }
        public string PrimaryConflict { get; set; }
        public bool IsOptimal { get; set; }
    }

    // the class to pass the gap of resource after shifting or simulate shift
    public class RoleGapReport
    {
        public double Saturation { get; set; }
        public int MissingHours { get; set; }
        public double RecommendedStaff { get; set; }
        public string Status => Saturation > 1.0 ? "Critical" : "Healthy";
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
    double conflict = GetConflictScore(state);
    double duration = GetDurationScore(state);
    double movement = GetMovementScore(state);
    double focus = GetFocusScore(state);
    double continuity = GetContinuityScore(state);

    return (conflict * 0.4) + (duration * 0.1) + (movement * 0.2) + (focus * 0.2) + (continuity * 0.1 );
}


public double GetConflictScore(ScheduleState state)
{
    if (state.PersonWeekGrid == null || state.PersonWeekGrid.Count == 0) return 1.0;

    double totalPenalty = 0;
    int totalAssignments = 0; 

    foreach (var entry in state.PersonWeekGrid)
    {
        int projectsInThisCell = entry.Value;
        totalAssignments += projectsInThisCell;

        if (projectsInThisCell > 1)
        {
            // 使用 1.5 次方保持对大冲突的敏感性
            totalPenalty += Math.Pow(projectsInThisCell - 1, 1.5);
        }
    }

    if (totalAssignments <= 0) return 1.0;

    // 计算惩罚比率
    double ratio = totalPenalty / totalAssignments;

    // --- 核心改进：指数衰减函数 ---
    // 逻辑：Score = e^(-ratio)
    // 当 ratio = 0 (无冲突) 时，结果是 1.0
    // 当 ratio = 1 时，结果是 0.36
    // 当 ratio = 10 (极端冲突) 时，结果是 0.000045
    // 这样分数永远不会是 0，算法在任何时候都能看到细微的优化趋势
    double finalScore = Math.Exp(-ratio);

    return finalScore;
}




    public double GetMovementScore(ScheduleState state)
    {
        // sum shift
        double totalShift = state.Projects.Sum(p => Math.Abs(state.GetShift(p)));
        // normalization socre
        double avgShift = totalShift / state.Projects.Count;
        return Math.Max(0, 1.0 - (avgShift / 20.0)); // if it near with 10 it will be 0
    }

    public double GetFocusScore(ScheduleState state)
    {
        // sum on average, how much projects each person takes on every week
        var multiTaskWeeks = state.PersonWeekGrid.Values.Count(v => v > 1);
        // normalization
        return Math.Max(0, 1.0 - ((double)multiTaskWeeks / state.PersonWeekGrid.Count));
    }

    public double GetContinuityScore(ScheduleState state)
    {
        if (state.Projects.Count == 0) return 1.0;

        double totalScore = 0;

        foreach (var project in state.Projects)
        {
            int originalCount = project.originalPeopleIds.Count;

            // If no baseline team exists, guess treat it as perfect for this project???
            if (originalCount == 0)
            {
                totalScore += 1.0;
                continue;
            }

            int overlap = 0;
            foreach (var person in project.people)
            {
                // Count how many current people were in the original team
                if (project.originalPeopleIds.Contains(person.id))
                    overlap++;
            }

            // Score is the share of original people still on the project.
            double projectScore = (double)overlap / originalCount;
            totalScore += projectScore;
        }

        // Average continuity across all projects.
        return totalScore / state.Projects.Count;
    }


    public double GetDurationScore(ScheduleState state)
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




    // evaluate move socre/delta in order to allow alg evaluate
    public double EvaluateMoveDelta(Project p, int candidateShift)
    {
        double scoreBefore = CalculateFitnessScore(_state);

        int originalShift = _state.GetShift(p);
        _state.ApplyShift(p, candidateShift); // simulate shift

        double scoreAfter = CalculateFitnessScore(_state);

        _state.ApplyShift(p, originalShift); // 

        return scoreAfter - scoreBefore; // return delta
    }



    // Compares the overall fitness improvement (Delta) to find the optimal project timing.
    public int GetBestMoveForProject(Project p)
    {
        int bestShift = _state.GetShift(p);
        double maxDelta = -999;   //Initial baseline: start with the project's current position

        // try valid shifts
        foreach (int shift in _state.GetValidShifts(p)) //Looping to compare each delta
        {
            double delta = EvaluateMoveDelta(p, shift);//Evaluation using Delta
            Console.WriteLine($"Project {p.name} try Shift {shift} | Delta: {delta}");
            if (delta > maxDelta)
            {
                maxDelta = delta;
                bestShift = shift;
            }
        }
        return bestShift; // Return the shift that balances all 5 weighted criteria most effectively.
    }


    // Performs a "vertical" resource optimization by searching for the best qualified 
    // candidate to replace a current team member without changing the project timeline.
    public Person DetermineBestReplacement(Project project, Person currentPerson)
    {
        // Identify all eligible candidates who possess the same professional Role.
        var candidates = new List<Person>();
        foreach (Project p in _state.Projects)
        {
            foreach (Person person in p.people)
            {
                if (person.role.Equals(currentPerson.role))
                {
                    candidates.Add(person);
                }
            }
        }

        Person bestCandidate = currentPerson;

        //  Establish the baseline score to compare against during simulation.
        double maxScore = CalculateFitnessScore(_state);

        foreach (var candidate in candidates)
        {
            // Optimization: Skip evaluation if the candidate is already assigned.
            if (candidate.id == currentPerson.id) continue;

            // Simulation: Tentatively swap the resource in the project to test the impact.
            _state.SwapPersonInProject(project, currentPerson, candidate);

            // Assess how this specific swap affects the global Fitness Score.
            // It primarily impacts Focus Score (20%) and Conflict Score (40%).
            double newScore = CalculateFitnessScore(_state);

            // Comparison: Update the "bestCandidate" if this resource swap improves overall schedule health.
            if (newScore > maxScore)
            {
                maxScore = newScore;
                bestCandidate = candidate;
            }

            // Rollback: Revert the project assignment to its original state for the next iteration.
            _state.SwapPersonInProject(project, candidate, currentPerson);
        }

        // Return the candidate that yields the highest efficiency without changing the project timeline.
        return bestCandidate;
    }




    //--------------evaluate if add a new project what is the ideal time and optimal resource------
    public double EvaluateNewProjectInsertion(Project newProject)
    {
        // record the socre when if without new project 
        double originalScore = CalculateFitnessScore(_state);

        // find the ideal time windo
        int bestShift = GetBestMoveForProject(newProject);
        _state.ApplyShift(newProject, bestShift);

        // find the ideal huamn resouce
        foreach (var person in newProject.people.ToList())
        {
            Person betterStaff = DetermineBestReplacement(newProject, person); //The DetermineBestReplacement method automatically filters candidates 
                                                                               // based on role and selects the one with the highest Fitness Score.
            if (betterStaff.id != person.id)
            {
                _state.SwapPersonInProject(newProject, person, betterStaff);
            }
        }

        // evaluate if insert new project , what socre of it
        double newScore = CalculateFitnessScore(_state);

        // returen delta socre to evaluate does insert successs, if score is positive it present success
        return newScore - originalScore;
    }


    // Returns a comprehensive "Search Map" of how moving this project (p) 
    // to different weeks impacts the global schedule's Fitness Score.
    // This might be useful for the Analyzer to determine if a project is "un-placeable" 
    // due to time constraints or resource shortages.
    public List<ShiftPerformance> GetScoreTrendForProject(Project p)
    {
        var trend = new List<ShiftPerformance>();
        int originalShift = _state.GetShift(p);
        var options = _state.GetValidShifts(p);

        foreach (int shift in options)
        {
            _state.ApplyShift(p, shift);
            double totalScore = CalculateFitnessScore(_state);

            trend.Add(new ShiftPerformance
            {
                Shift = shift,
                Score = totalScore,
                IsOptimal = false
            });

            _state.ApplyShift(p, originalShift); // roll back
        }
        return trend.OrderByDescending(t => t.Score).ToList();
    }



    // a method to help adding new projetc since we need to find optimal resouce with same role
    public Dictionary<string, RoleGapReport> GetRoleSaturation(int startWeek, int endWeek)
    {
        var report = new Dictionary<string, RoleGapReport>();
        var allRoles = _state.People.Select(p => p.role).Distinct();

        foreach (var role in allRoles)
        {
            int supplyWeeks = _state.People.Count(p => p.role == role) * (endWeek - startWeek + 1);

            int demandWeeks = 0;
            foreach (var proj in _state.Projects)
            {
                var shift = _state.GetShift(proj);
                var cells = _state.GetGrid(proj, shift)
                                 .Where(c => c.Week >= startWeek && c.Week <= endWeek);

                foreach (var cell in cells)
                {
                    var person = _state.People.First(p => p.id == cell.PersonId);
                    if (person.role == role) demandWeeks++;
                }
            }

            double saturation = supplyWeeks == 0 ? 0 : (double)demandWeeks / supplyWeeks;

            report[role] = new RoleGapReport
            {
                Saturation = saturation,
                MissingHours = (demandWeeks > supplyWeeks) ? (demandWeeks - supplyWeeks) * 40 : 0,
                RecommendedStaff = Math.Max(0, Math.Ceiling((double)(demandWeeks - supplyWeeks) / (endWeek - startWeek + 1)))
            };
        }
        return report;
    }



    public void DebugConflictDetails(ScheduleState state)
{
    int totalConflictCells = 0;   // 受灾的格子数（受苦的周数）
    int totalExtraTasks = 0;      // 对齐工时的冲突数（多出来的任务单元）
    int totalOvertimeHours = 0;
    var conflictingPeople = new HashSet<int>();

    Console.WriteLine("\n========== [DEBUG Analyze] ==========");

    foreach (var entry in state.PersonWeekGrid)
    {
        if (entry.Value > 1) 
        {
            totalConflictCells++;
            
            // --- 核心改进：统计超载的任务单元数 ---
            int extraTasks = entry.Value - 1; 
            totalExtraTasks += extraTasks;

            int overtime = extraTasks * 40;
            totalOvertimeHours += overtime;
            conflictingPeople.Add(entry.Key.PersonId);

            Console.WriteLine($"[CONFLICTS🚨] Person ID: {entry.Key.PersonId.ToString().PadRight(4)} | The {entry.Key.Week.ToString().PadRight(2)} week | count of project: {entry.Value} | Overtime: {overtime}h");
        }
    }

    if (totalConflictCells == 0)
    {
        Console.WriteLine(">>> Success！👍 No conflicts");
    }
    else
    {
        Console.WriteLine("------------------------------------------");
        Console.WriteLine($"Total Conflicts (Extra Tasks): {totalExtraTasks}");
        Console.WriteLine($"Affected Week Grids: {totalConflictCells}");
        Console.WriteLine($"Related Persons: {conflictingPeople.Count}");
        Console.WriteLine($"Total Double Booking time: {totalOvertimeHours} hours。");
    }
    Console.WriteLine("==========================================\n");
}



    //----return conflicts detail: exist conflict in which project whose overloap and which week overloap
    // public List<string> GetDetailedConflictList()
    // {
    //     var details = new List<string>();

    //     foreach (var entry in _state.PersonWeekGrid.Where(kv => kv.Value >= 2))
    //     {
    //         var key = entry.Key;
    //         var person = _state.People.First(p => p.id == key.PersonId);

    //         var conflictingProjects = _state.Projects
    //             .Where(proj => _state.GetGrid(proj, _state.GetShift(proj))
    //             .Any(cell => cell.PersonId == key.PersonId && cell.Week == key.Week))
    //             .Select(proj => proj.name)
    //             .ToList();

    //         details.Add($"Week {key.Week} | {person.name} | Projects: {string.Join(" & ", conflictingProjects)}");
    //     }
    //     return details;
    // }



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


