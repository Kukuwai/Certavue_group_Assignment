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
        public Dictionary<(int PersonId, Project Project, int RawWeek), int> Assignments { get; set; } = new();
    }

    public SolveResult Optimize(ScheduleState state, double maxSeconds = 30)
    {
        // Calculate where we are starting from so we can measure improvement later.
        double initialOverload = state.PersonWeekHours.Values.Sum(v => Math.Max(0, v - 40));
        int maxWeek = state.Projects.Max(p => p.endDate > 0 ? p.endDate : 52);

        var model = new CpModel();
        var objective = LinearExpr.NewBuilder();
        
       // Setup a global resource pool that helps the solver find "backups" for overloaded people.
        var personLoadMap = state.People.ToDictionary(
        p => p.id, 
        p => (double)state.PersonWeekHours
        .Where(kv => kv.Key.PersonId == p.id)
        .Sum(kv => kv.Value)
        );
        var rolePool = state.People.GroupBy(p => p.role ?? "").ToDictionary(g => g.Key, g => g.ToList());
        
        // x stores our decision variables: [Who, Which Project, Original Week, New Target Week]
        var x = new Dictionary<(int pId, Project prj, int rW, int tW), BoolVar>();
        var personWeekLoad = new Dictionary<(int pId, int tW), LinearExprBuilder>();

        // iterate through every project and task to inject our strategies.
        foreach (var p in state.Projects)
        {
            var rawAssigns = state.GetOriginalAssignments(p);
            int pDeadline = p.endDate > 0 ? p.endDate : 52;

            foreach (var assign in rawAssigns)
            {
                var choices = new List<BoolVar>();
                
               // Strategy A: Resource Expansion. Find people with the same role who might be free.
                var candidates = GetResourceExpansionCandidates(assign, state, rolePool, personLoadMap);

                foreach (var cand in candidates)
                {
                    // Strategy B: Delay Softening. Look for better time slots around the current schedule.
                    var windows = GetSoftenedTimeWindows(assign, state, p, pDeadline, maxWeek);

                    foreach (var tW in windows)
                    {
                        var bv = model.NewBoolVar($"p{cand.id}_prj{p.id}_r{assign.Week}_t{tW}");
                        x[(cand.id, p, assign.Week, tW)] = bv;
                        choices.Add(bv);

                       // If the solver picks a week past the deadline, will hit it with a lateness penalty.
                        if (tW > pDeadline)
                            objective.AddTerm(bv, (tW - pDeadline) * W_LATENESS);

                        // Track the total hours per person per week so we can identify conflicts.
                        var key = (cand.id, tW);
                        if (!personWeekLoad.ContainsKey(key)) personWeekLoad[key] = LinearExpr.NewBuilder();
                        personWeekLoad[key].AddTerm(bv, assign.Hours);
                        
                       // Small penalty for moving tasks too far from their original slot (keeps the schedule stable).
                        objective.AddTerm(bv, Math.Abs(tW - (assign.Week + state.GetShift(p))) * W_MOVEMENT);
                    }
                }
                // Each original task must be assigned to exactly one person at one specific time.
                if (choices.Count > 0) model.AddExactlyOne(choices);
            }
        }

       // Apply penalties for any hours exceeding the 40h/week capacity.
        ApplyConflictConstraints(model, objective, personWeekLoad, state);

        // Solve the model. I've set it to parallelize across 6 workers to speed things up.
        model.Minimize(objective);
        var solver = new CpSolver();
        solver.StringParameters = $"max_time_in_seconds:{maxSeconds}, num_search_workers:6";
        var status = solver.Solve(model);

        // Decode the results and generate the final report for llm understanding
        return ProcessResult(status, solver, x, personWeekLoad, initialOverload, state);
    }

// Helper: Find qualified backups. Prioritize the original owner, then look for the least-loaded peers.
    private List<Person> GetResourceExpansionCandidates((int PersonId, int Week, int Hours) assign, ScheduleState state, Dictionary<string, List<Person>> rolePool, Dictionary<int, double> loadMap)
{
    int originalId = assign.PersonId;
    var original = state.People.FirstOrDefault(pe => pe.id == originalId);
    
    if (original == null || !rolePool.TryGetValue(original.role ?? "", out var allCandidates)) 
        return state.People.Where(pe => pe.id == originalId).ToList();

    return allCandidates
        .OrderBy(c => c.id == originalId ? 0 : 1) 
        .ThenBy(c => loadMap.GetValueOrDefault(c.id, 0.0))
        .Take(4) 
        .ToList();
}

// Helper: Create a +/- 15 week window for each task. Max delay allowed is 8 weeks past deadline.
private List<int> GetSoftenedTimeWindows((int PersonId, int Week, int Hours) assign, ScheduleState state, Project p, int deadline, int maxWeek)
{

    int currentShift = state.GetShift(p);
    int center = assign.Week + currentShift;
    
    var weeks = new List<int>();
    // search window range
    for (int i = center - 15; i <= center + 15; i++)
    {
        if (i >= 1 && i <= deadline + 8 && i <= maxWeek) 
            weeks.Add(i);
    }
    return weeks;
}

    // calculate "Overload = TotalHours - 40" and apply the massive conflict penalty.
    private void ApplyConflictConstraints(CpModel model, LinearExprBuilder objective, Dictionary<(int pId, int tW), LinearExprBuilder> loadMap, ScheduleState state)
    {
        foreach (var kv in loadMap)
        {
            var overload = model.NewIntVar(0, 2000, "");
            model.Add(overload >= kv.Value - 40);
            objective.AddTerm(overload, W_CONFLICT);
        }
    }

    // Translate the solver's binary output back into our project assignments and stats.
    private SolveResult ProcessResult(CpSolverStatus status, CpSolver solver, Dictionary<(int pId, Project prj, int rW, int tW), BoolVar> x, Dictionary<(int pId, int tW), LinearExprBuilder> loadMap, double initial, ScheduleState state)
    {
        var res = new SolveResult { Status = status };

        if (status != CpSolverStatus.Feasible && status != CpSolverStatus.Optimal) return res;

        int delays = 0;
        int totalDelayWeeks = 0;
        int resourceSwaps = 0;

        foreach (var entry in x)
        {
            if (solver.Value(entry.Value) == 1)
            {
                var k = entry.Key;
                res.Assignments[(k.pId, k.prj, k.rW)] = k.tW;

                int deadline = k.prj.endDate > 0 ? k.prj.endDate : 52;
                if (k.tW > deadline) { delays++; totalDelayWeeks += (k.tW - deadline); }
                
                int defaultId = k.prj.people.FirstOrDefault()?.id ?? -1;
                if (k.pId != defaultId) resourceSwaps++;
            }
        }

        res.Report = new StrategyReport {
            ConflictReduced = initial - res.FinalOverload,
            TotalDelayWeeks = totalDelayWeeks,
            ResourceSwaps = resourceSwaps 
        };
        return res;
    }


}