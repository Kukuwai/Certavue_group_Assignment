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

    public double GetConflictScore(ScheduleState state)
{
    // 1. 如果网格里没人，说明没活干，自然没冲突
    if (state.PersonWeekGrid == null || state.PersonWeekGrid.Count == 0) return 1.0;

    double totalOverloadPenalty = 0;
    
    // 2. 分母：当前所有有排班的“人-周”单元格的总承载力
    // 假设每个单元格标准工时是 40h
    double totalCapacityHours = state.PersonWeekGrid.Count * 40.0; 

    foreach (var entry in state.PersonWeekGrid)
    {
        int projectCount = entry.Value;

        if (projectCount > 1)
        {
            // 3. 计算超载的小时数
            // 2个项目重叠 = 超载 40h；3个项目重叠 = 超载 80h
            double extraHours = (projectCount - 1) * 40.0;
            
            // 4. 使用平方惩罚 (Quadratic Penalty)
            // 为什么要平方？为了告诉算法：让一个人干 3 份活（1600罚分）
            // 远比让两个人各干 2 份活（400+400=800罚分）要糟糕得多。
            totalOverloadPenalty += Math.Pow(extraHours, 2);
        }
    }

    // 5. 归一化：将惩罚值映射到 0-1 之间
    // 系数 400.0 是为了调节灵敏度，你可以根据 0.79 的变动来调整它
    double normalizedPenalty = totalOverloadPenalty / (totalCapacityHours * 10.0);
    
    return Math.Exp(-normalizedPenalty);
}

    public double GetMovementScore(ScheduleState state) {
    // sum shift
    double totalShift = state.Projects.Sum(p => Math.Abs(state.GetShift(p)));
    // normalization socre
    double avgShift = totalShift / state.Projects.Count;
    return Math.Max(0, 1.0 - (avgShift / 4.0)); // if it near with 4 it will be 0
    }

    public double GetFocusScore(ScheduleState state) {
    // sum on average, how much projects each person takes on every week
    var multiTaskWeeks = state.PersonWeekGrid.Values.Count(v => v > 1);
    // normalization
    return Math.Max(0, 1.0 - ((double)multiTaskWeeks / state.PersonWeekGrid.Count));
   }

//    public double GetContinuityScore(ScheduleState state) {
//     double totalPenalty = 0;
//     foreach(var p in state.Projects) {
//         // count how many people in a project
//         int peopleCount = p.people.Distinct().Count(); 
//         if(peopleCount > 1) totalPenalty += (peopleCount - 1);
//     }
//     return Math.Max(0, 1.0 - (totalPenalty / state.Projects.Count));
//    }  


public double GetContinuityScore(ScheduleState state)
    {
        if (state.Projects.Count == 0) return 1.0;

        double totalScore = 0;

        foreach (var project in state.Projects)
        {
            int originalCount = project.originalPeopleIds.Count;

            // 【关键修复】如果这个项目原本就没有人（比如新项目），或者基准线没设好
            if (originalCount == 0)
            {
                totalScore += 1.0; 
                continue;
            }

            int overlap = 0;
            foreach (var person in project.people)
            {
                // 检查现在的成员是否在原始名单里
                if (project.originalPeopleIds.Contains(person.id))
                    overlap++;
            }

            double projectScore = (double)overlap / originalCount;
            totalScore += projectScore;
        }

        return totalScore / state.Projects.Count;
    }


    public double GetDurationScore(ScheduleState state)
    {
        double totalScore = 0;
        int projectCount = 0;

        foreach (var project in state.Projects)
        {
            // 如果项目没人做，跳过
            if (project.people.Count == 0) continue;

            // 1. 获取理想时长 (Capacity)
            // 确保 capacity 已更新
            if (project.capacity == 0) project.updateCapacity();
            double idealDuration = project.capacity;

            // 2. 获取当前排班的实际时长 (Actual Duration)
            int minWeek = int.MaxValue;
            int maxWeek = int.MinValue;
            bool hasAssignments = false;

            foreach (var person in project.people)
            {
                // 使用 List<int> 获取周数
                if (person.projects.TryGetValue(project, out List<int> weeks))
                {
                    foreach (int w in weeks)
                    {
                        if (w < minWeek) minWeek = w;
                        if (w > maxWeek) maxWeek = w;
                        hasAssignments = true;
                    }
                }
            }

            if (!hasAssignments) continue;

            // 计算实际跨度
            double actualDuration = (maxWeek - minWeek) + 1;

            // 3. 计算得分
            if (actualDuration > 0)
            {
                // 理想时长 / 实际时长
                // 如果实际时长被拉得很长，分数就会变低
                double score = idealDuration / actualDuration;
                
                // 限制最高分 1.0
                if (score > 1.0) score = 1.0;
                
                totalScore += score;
                projectCount++;
            }
        }

        // 如果没有项目，默认返回 1.0，否则返回平均分
        return projectCount == 0 ? 1.0 : totalScore / projectCount;
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


