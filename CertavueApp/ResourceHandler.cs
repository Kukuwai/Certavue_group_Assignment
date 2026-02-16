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
    public class ShiftPerformance {
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




//     public double GetConflictScore(ScheduleState state)
// {
//     if (state.PersonWeekGrid == null || state.PersonWeekGrid.Count == 0) return 1.0;

//     int totalExtraTasks = 0;
    
//     // 1. 直接统计所有超载的任务单元
//     foreach (var entry in state.PersonWeekGrid)
//     {
//         if (entry.Value > 1)
//         {
//             totalExtraTasks += (entry.Value - 1);
//         }
//     }

//     if (totalExtraTasks == 0) return 1.0;
//     return 1.0 / (1.0 + (totalExtraTasks * 0.05));
// }


    // This is a overload punisher
    public double GetConflictScore(ScheduleState state)
    {
        if (state.PersonWeekHours.Count == 0) return 1.0;

        double totalOverworkHours = 0;
        double totalAssignedHours = 0;

        // create new dictionary for people and that personid for easy reference in loop below
        var peopleById = new Dictionary<int, Person>();
        foreach (Person p in state.People)
        {
            peopleById.Add(p.id, p);
        }

        // changed to iterate person week totals not person-project weeks assigned
        foreach (var personWeek in state.PersonWeekHours)
        {
            // get person id
            var personId = personWeek.Key.PersonId;
            // get assigned hours for that week / per person
            var assignedHours = personWeek.Value;

            int capacity = 40;
            // get person by id using new dictionary created above and check if capacity above 0
            if (peopleById.TryGetValue(personId, out var person) && person.capacity > 0)
            {
                capacity = person.capacity;
            }

            totalAssignedHours += assignedHours;
            totalOverworkHours += Math.Max(0, assignedHours - capacity);

            /* caculate the overwork hours
            if (hoursInThisCell > CAPACITY_LIMIT)
            { // caculate the total overwork hours
                totalOverworkHours += (hoursInThisCell - CAPACITY_LIMIT);
            }*/
        }
        // check if person is even assigned hours on project
        if (totalAssignedHours <= 0)
        {
            // if no return default score
            return 1.0;
        }

        //caculate percentage of overwork
        double conflictRatio = totalOverworkHours / totalAssignedHours;
        //Normalization
        return Math.Max(0, 1.0 - conflictRatio);
    }


    public double GetAverageProjectsPerActivePersonWeek(ScheduleState state)
    {
        if (state.PersonWeekGrid.Count == 0) return 1.0;

        var projectsPerPersonWeek = new Dictionary<ScheduleState.PersonWeekKey, int>();

        foreach (var cell in state.PersonWeekGrid)
        {
            if (cell.Value <= 0) continue;

            var key = new ScheduleState.PersonWeekKey(cell.Key.PersonId, cell.Key.Week);

            if (!projectsPerPersonWeek.ContainsKey(key))
                projectsPerPersonWeek[key] = 0;

            projectsPerPersonWeek[key] += 1;
        }

        if (projectsPerPersonWeek.Count == 0) return 1.0;

        return projectsPerPersonWeek.Values.Average();
    }




// value the cost of shift
    public double GetMovementScore(ScheduleState state)
    {
        int movedCount = 0;
        foreach (var p in state.Projects)
        {
            if (state.GetShift(p) != 0)
            {
                movedCount++;
            }
        }

        // 2. 计算“稳定性得分”
        // 公式： (总项目数 - 移动的项目数) / 总项目数
        return (double)(state.Projects.Count - movedCount) / state.Projects.Count;
    }
    

   public double GetFocusScore(ScheduleState state)
    {

        double totalScore = 0;
        int activePeopleCount = 0;

        foreach (var person in state.People)
        {
            // 1. Total Weeks
            int totalWeeksWorked = 0;
            
            // 2. Distinct Projects
            // person.projects 的 Key 是 Project 对象，所以 Count 就是项目数
            int distinctProjects = person.projects.Count;

            foreach (var weeks in person.projects.Values)
            {
                totalWeeksWorked += weeks.Count;
            }

            if (totalWeeksWorked == 0) continue;

            activePeopleCount++;

            // less project enaged high focus socre
            if (distinctProjects <= 1)
            {
                totalScore += 1.0;
            }
            else
            {
                // 假设工作了 10 周，做了 2 个项目。理想是 10-1=9，实际是 10-2=8。分数 8/9 = 0.88
                // 假设工作了 10 周，做了 10 个项目。实际 10-10=0。分数 0/9 = 0.0
                
                if (totalWeeksWorked > 1)
                {
                    double score = (double)(totalWeeksWorked - distinctProjects) / (totalWeeksWorked - 1);
                    totalScore += Math.Max(0, score); 
                }
                else
                {
                    totalScore += 0;
                }
            }
        }

        return activePeopleCount == 0 ? 1.0 : totalScore / activePeopleCount;
    }
 


public double GetContinuityScore(ScheduleState state)
{
    double totalScore = 0;
    int activeProjects = 0;

    foreach (var project in state.Projects)
    {
        // 1. 获取该项目被分配到的所有周
        int currentShift = state.GetShift(project);
        var cells = state.GetGrid(project, currentShift);
        
        if (!cells.Any()) continue;

        // 2. 提取周数并排序
        var weeks = cells.Select(c => c.Week).Distinct().OrderBy(w => w).ToList();
        
        if (weeks.Count <= 1) 
        {
            totalScore += 1.0; // 只有一周肯定连续
        }
        else
        {
            // 3. 检查有没有“断档”
            int gaps = 0;
            for (int i = 0; i < weeks.Count - 1; i++)
            {
                // 如果下一周 不是 当前周+1，说明断了
                if (weeks[i+1] != weeks[i] + 1)
                {
                    gaps++;
                }
            }

            // 4. 计算分数：断档越少分越高
            // 简单的公式： 1.0 / (1 + 断档数组)
            // 例如：0断档=1.0分，1次断档=0.5分，2次断档=0.33分
            totalScore += 1.0 / (1.0 + gaps);
        }
        activeProjects++;
    }

    return activeProjects == 0 ? 1.0 : totalScore / activeProjects;
}


public double GetDurationScore(ScheduleState state)
{
    double totalScore = 0;
    int count = 0;

    foreach (var project in state.Projects)
    {
        double ideal = project.InitialBaselineSpan; 
        double actual = state.GetCurrentSpan(project); 

        if (actual > 0 && ideal > 0)
        {
            totalScore += Math.Min(1.0, ideal / actual);
            count++;
        }
    }
    return count == 0 ? 1.0 : totalScore / count;
}


//----------------------------------------
    public ScheduleState GetCurrentState() => _state;
    public AvailabilityFinder GetFinder() => _finder;




    // public ShiftScore EvaluateMove(Project p, int candidateShift)
    // {
    //     int currentShift = _state.GetShift(p);

    //     int currentDoubleBooked = _state.PersonWeekGrid
    //                    .Where(kv => kv.Value >= 2)
    //                    .Sum(kv => kv.Value);
    //     if (candidateShift == currentShift)
    //     {


    //         int currentConflictCells = _state.PersonWeekGrid.Values.Count(v => v >= 2);

    //         double currentPenalty = (1000.0 * currentDoubleBooked) + (10.0 * currentConflictCells);

    //         return new ShiftScore
    //         {
    //             DeltaDoubleBooked = 0,
    //             OverlapAfter = currentConflictCells,
    //             ShiftDistance = 0,
    //             Fitness = 1.0 / (1.0 + currentPenalty)
    //         };
    //     }



    //     var currentCells = _state.GetGrid(p, currentShift);
    //     var candidateCells = _state.GetGrid(p, candidateShift);

    //     var touchedKeys = currentCells.Union(candidateCells).Distinct();

    //     int delta = 0;
    //     int overlapAfter = 0;


    //     foreach (var key in touchedKeys)
    //     {
    //         _state.PersonWeekGrid.TryGetValue(key, out int currentTotalCount);

    //         bool isOccupiedInCurrent = currentCells.Any(c => c.Equals(key));
    //         int baseCountWithoutProject = isOccupiedInCurrent ? currentTotalCount - 1 : currentTotalCount;

    //         bool isOccupiedInCandidate = candidateCells.Any(c => c.Equals(key));
    //         int newCountWithCandidate = isOccupiedInCandidate ? baseCountWithoutProject + 1 : baseCountWithoutProject;

    //         if (currentTotalCount >= 2) delta -= 1;
    //         if (newCountWithCandidate >= 2)
    //         {
    //             delta += 1;
    //             overlapAfter++;
    //         }
    //     }
    //     int distance = Math.Abs(candidateShift - currentShift);

    //     currentDoubleBooked = _state.PersonWeekGrid
    //         .Where(kv => kv.Value >= 2)
    //         .Sum(kv => kv.Value);

    //     int projectedDoubleBooked = Math.Max(0, currentDoubleBooked + delta);

    //     double penalty = (1000.0 * projectedDoubleBooked) + (10.0 * overlapAfter) + distance;
    //     double fitness = 1.0 / (1.0 + penalty);

    //     return new ShiftScore
    //     {
    //         DeltaDoubleBooked = delta,
    //         OverlapAfter = overlapAfter,
    //         ShiftDistance = distance,
    //         Fitness = fitness
    //     };

    // }


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
        if (delta > maxDelta)
        {
            maxDelta = delta;
            bestShift = shift;
        }
    }
    return bestShift; // Return the shift that balances all 5 weighted criteria most effectively.
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

          //  Console.WriteLine($"[CONFLICTS🚨] Person ID: {entry.Key.PersonId.ToString().PadRight(4)} | The {entry.Key.Week.ToString().PadRight(2)} week | count of project: {entry.Value} | Overtime: {overtime}h");
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


// Performs a "vertical" resource optimization by searching for the best qualified 
// candidate to replace a current team member without changing the project timeline.
public Person DetermineBestReplacement(Project project, Person currentPerson)
{
    // Identify all eligible candidates who possess the same professional Role.
    var candidates = _finder.FindPeopleByRole(currentPerson.role);
    Person bestCandidate = currentPerson;
    double maxScore = CalculateFitnessScore(_state);

     foreach (var candidate in candidates)
    {
        // Optimization: Skip evaluation if the candidate is already the person assigned.
        if (candidate.id == currentPerson.id) continue;

        // Simulation: Tentatively swap the resource in the project to test the impact.
        _state.SwapPersonInProject(project, currentPerson, candidate);

        // Assess how this specific swap affects the global Fitness Score.
        double newScore = CalculateFitnessScore(_state);

        // Comparison: If the new candidate provides a better overall score, they become the top choice.
        if (newScore > maxScore)
        {
            maxScore = newScore;
            bestCandidate = candidate;
        } 

        // 6. Rollback: CRITICAL! Revert the project assignment to the original state for the next iteration.
        _state.SwapPersonInProject(project, candidate, currentPerson);
    }

    // Return the candidate that yields the highest efficiency.
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




