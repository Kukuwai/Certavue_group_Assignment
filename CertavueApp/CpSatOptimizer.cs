using System;
using System.Collections.Generic;
using System.Linq;
using Google.OrTools.Sat;

public class CpSatOptimizer
{
    // Penalty weights: give Conflict high to make it the absolute priority.
    // Lateness and movement are secondary "nice-to-haves" for the solver.
    private const int W_CONFLICT = 1000000; 
    private const int W_LATENESS = 1000;    
    private const int W_MOVEMENT = 1;       

    public class StrategyReport
    {
        public double ConflictReduced { get; set; }
        public int TotalDelayWeeks { get; set; }
        public int ResourceSwaps { get; set; }
        public string Summary { get; set; }
    }

    public class SolveResult
    {
        public CpSolverStatus Status { get; set; }
        public double FinalOverload { get; set; }
        public StrategyReport Report { get; set; } = new StrategyReport();
        
        public Dictionary<(int PersonId, Project Project, int RawWeek, int TaskIdx), int> Assignments { get; set; } = new();
    }


// Re-optimizes the schedule using Google OR-Tools (CP-SAT Solver).
// Aim: Minimize lateness and task movement while resolving resource overloads.
public SolveResult Optimize(ScheduleState state, Dictionary<int, List<(int PersonId, int Week, int Hours)>> originalTaskMap,double maxSeconds = 30) // 🚨 修改这里double maxSeconds = 30)
{
    // 1. intialization
    double initialOverload = state.PersonWeekHours.Values.Sum(v => Math.Max(0, v - 40));
    int maxWeek = state.Projects.Max(p => p.endDate > 0 ? p.endDate : 52);
    // Initialize CP-SAT model and the multi-objective expression builder.
    var model = new CpModel();
    var objective = LinearExpr.NewBuilder();
    
    var personLoadMap = state.People.ToDictionary(
        p => p.id, 
        p => (double)state.PersonWeekHours.Where(kv => kv.Key.PersonId == p.id).Sum(kv => kv.Value)
    );
    // Group people by role to identify valid substitutes during resource re-assignment.
    var rolePool = state.People.GroupBy(p => p.role ?? "").ToDictionary(g => g.Key, g => g.ToList());
    
    // 🚨 Add a tIdx field to the dictionary Key to ensure that multiple tasks in the same week are not overwritten
    var x = new Dictionary<(int pId, Project prj, int tW, int tIdx), BoolVar>();
    // Track linear expressions for cumulative weekly workload per person.
    var personWeekLoad = new Dictionary<(int pId, int tW), LinearExprBuilder>();//this is a Working hours ledger

    // 2. loop for each state/assigns/candidates/windows
    foreach (var p in state.Projects)
    {
        if (!originalTaskMap.TryGetValue(p.id, out var rawAssigns)) 
        {
            Console.WriteLine($"[WARNING] Project {p.id} not found in backup map.");
            continue;
        }
        int pDeadline = p.endDate > 0 ? p.endDate : 52;

        // Iterate through each task assignment in the original project plan.
        for (int i = 0; i < rawAssigns.Count; i++)
        {
            var assign = rawAssigns[i];
            var choices = new List<BoolVar>();
            // Identify qualified staff who can take this task based on role and current load.
            var candidates = GetResourceExpansionCandidates(assign, state, rolePool, personLoadMap);

            foreach (var cand in candidates)   
            {
                // Determine valid time windows for the task, allowing for softened deadlines.
                var windows = GetSoftenedTimeWindows(assign, state, p, pDeadline, maxWeek);

                foreach (var tW in windows)
                {   
                    // Create a boolean decision variable for this specific assignment possibility.
                    var bv = model.NewBoolVar($"p{cand.id}_prj{p.id}_idx{i}_tw{tW}");
                    x[(cand.id, p, tW, i)] = bv; // Use 'i' to ensure uniqueness
                    choices.Add(bv);

                    // Define multi-objective penalties: 
                    // 1. W_LATENESS: Penalizes finishing after the project deadline.
                    if (tW > pDeadline)
                        objective.AddTerm(bv, (tW - pDeadline) * W_LATENESS);

                    var key = (cand.id, tW);
                    if (!personWeekLoad.ContainsKey(key)) personWeekLoad[key] = LinearExpr.NewBuilder();
                    personWeekLoad[key].AddTerm(bv, assign.Hours);
                    
                    // 2. W_MOVEMENT: Penalizes shifting tasks from their original weeks to maintain stability.
                    objective.AddTerm(bv, Math.Abs(tW - assign.Week) * W_MOVEMENT);
                }
            }
            // ENFORCEMENT: Each original task must be assigned to exactly one resource and one time slot.
            if (choices.Count > 0) 
            {
                model.AddExactlyOne(choices);
            }
            else 
            {
                
                var roleName = state.People.FirstOrDefault(per => per.id == assign.PersonId)?.role ?? "Unknown";
                Console.WriteLine($"[CRITICAL] Missing: Prj {p.id}, Week {assign.Week}, Hours {assign.Hours}, Role: {roleName}");
            }
        } 
    } 
    // Apply global constraints (e.g., preventing a person from working the same project in the same week twice).
    ApplyConflictConstraints(model, objective, personWeekLoad, state);
    // 3. SOLVING PHASE
    // Minimize the total penalty (Lateness + Movement).
    model.Minimize(objective);
    var solver = new CpSolver();
    // Set solver parameters: time limit and parallel execution threads.
    solver.StringParameters = $"max_time_in_seconds:{maxSeconds}, num_search_workers:6";
    var status = solver.Solve(model);
    // Map mathematical results back to ScheduleState and calculate final heuristic metrics.
    return ProcessResult(status, solver, x, personWeekLoad, initialOverload, state, originalTaskMap);
}



// Helper: Find qualified backups. Prioritize the original owner, then look for the least-loaded peers.
private List<Person> GetResourceExpansionCandidates((int PersonId, int Week, int Hours) assign, ScheduleState state, Dictionary<string, List<Person>> rolePool, Dictionary<int, double> loadMap)
{   // Retrieve the ID and object of the person originally assigned to this task
    int originalId = assign.PersonId;
    var original = state.People.FirstOrDefault(pe => pe.id == originalId);
    // Safety check: if person or role group doesn't exist, fall back to the original owner only
    if (original == null || !rolePool.TryGetValue(original.role ?? "", out var allCandidates)) 
        return state.People.Where(pe => pe.id == originalId).ToList();
    // Generate a prioritized list of up to 4 candidates
    return allCandidates
        .OrderBy(c => c.id == originalId ? 0 : 1) // Rule 1: Always prioritize the original owner (0) over others (1)
        .ThenBy(c => loadMap.GetValueOrDefault(c.id, 0.0)) // Rule 2: Among peers, pick those with the lowest current workload
        .Take(4) // Limitation: Only return the top 4 candidates to keep the solver's search space manageable
        .ToList();
}

//Helper: Create a +/- 15 week window for each task. Max delay allowed is 8 weeks past deadline.
private List<int> GetSoftenedTimeWindows((int PersonId, int Week, int Hours) assign, ScheduleState state, Project p, int deadline, int maxWeek)
{
    // Calculate the task's current position by adding the project's global shift to the original week
    int currentShift = state.GetShift(p);
    int center = assign.Week + currentShift;
    
    var weeks = new List<int>();
    // Iterate through a potential +/- 15 week window around the current center
    for (int i = center - 15; i <= center + 15; i++)
    {
        // Validation logic: Ensure the week is within calendar bounds (>= 1) 
        // and doesn't exceed the softened deadline (deadline + 8 weeks) or the global max week
        if (i >= 1 && i <= deadline + 8 && i <= maxWeek) 
            weeks.Add(i);
    }
    return weeks;
}



    // calculate "Overload = TotalHours - 40" and apply the massive conflict penalty.
    private void ApplyConflictConstraints(CpModel model, LinearExprBuilder objective, Dictionary<(int pId, int tW), LinearExprBuilder> loadMap, ScheduleState state)
    {   // Iterate through every (Person, Week) combination that has assigned hours
        foreach (var kv in loadMap)
        {  
            // Define a variable representing the amount of hours exceeding the 40h capacity
            var overload = model.NewIntVar(0, 2000, "");
            // Constraint: overload must be greater than or equal to (Total Hours - 40)
            // If Total Hours <= 40, the solver will keep overload at 0 due to minimization
            model.Add(overload >= kv.Value - 40);
            // Add the overload to the objective function multiplied by the high-priority Conflict Weight
            objective.AddTerm(overload, W_CONFLICT);
        }
    }

    // Translate the solver's binary output back into our project assignments and stats.
    private SolveResult ProcessResult(
        CpSolverStatus status, 
        CpSolver solver, 
        Dictionary<(int pId, Project prj, int tW, int tIdx), BoolVar> x, 
        Dictionary<(int pId, int tW), LinearExprBuilder> loadMap, 
        double initial, 
        ScheduleState state,
        Dictionary<int, List<(int PersonId, int Week, int Hours)>> originalTaskMap)

    {   // Initialize result object with solver status .
        var res = new SolveResult { Status = status };
        if (status != CpSolverStatus.Feasible && status != CpSolverStatus.Optimal) return res;

        double finalOverloadValue = 0;
        foreach (var kv in loadMap)
        {
        double totalHours = solver.Value(kv.Value); 
        if (totalHours > 40)
        {
            finalOverloadValue += (totalHours - 40);
        }
        }
        res.FinalOverload = finalOverloadValue; 

        // Local counters for quantifying the "cost" of the optimization.
        int delays = 0;
        int totalDelayWeeks = 0;
        int resourceSwaps = 0;
        double totalRecoveredHours = 0;
        // Iterate through all possible decision variables (assignments).
        foreach (var entry in x)
        {   // Check if the solver selected this specific assignment (Value == 1).
            if (solver.Value(entry.Value) == 1)
            {
                var k = entry.Key; 
                // Retrieve the original task data using the project ID and task index.
                var projectTasks = state.GetOriginalAssignments(k.prj);
                var task = originalTaskMap[k.prj.id][k.tIdx];
                // Track total workload processed to ensure data integrity (no hours lost).
                totalRecoveredHours += task.Hours;
                // Record the final optimized week (k.tW) into the result assignments dictionary in order to let C# understand.
                res.Assignments[(k.pId, k.prj, task.Week, k.tIdx)] = k.tW;
                // Check for lateness: compare solver's chosen week vs. project deadline.
                int deadline = k.prj.endDate > 0 ? k.prj.endDate : 52;
                if (k.tW > deadline) 
                { 
                    delays++; 
                    totalDelayWeeks += (k.tW - deadline); 
                }
                // Check for resource changes: compare assigned person vs. original lead.
                int defaultId = k.prj.people.FirstOrDefault()?.id ?? -1;
                if (k.pId != defaultId) resourceSwaps++;
            }
        }


        res.Report = new StrategyReport {
            ConflictReduced = initial - res.FinalOverload,
            TotalDelayWeeks = totalDelayWeeks,
            ResourceSwaps = resourceSwaps 
        };

        Console.WriteLine("\n🚀 [Optimization Strategy Report]");
        Console.WriteLine($"-----------------------------------");
        Console.WriteLine($"Initial Conflicts: {initial,8} hrs");
        Console.WriteLine($"Final Overload:    {res.FinalOverload,8} hrs");
        Console.WriteLine($"Conflicts Reduced: {res.Report.ConflictReduced,8} hrs ({(res.Report.ConflictReduced/initial*100):F1}%)");
        Console.WriteLine($"-----------------------------------\n");

        return res;
        
    }
}