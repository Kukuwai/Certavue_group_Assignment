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

public SolveResult Optimize(ScheduleState state, Dictionary<int, List<(int PersonId, int Week, int Hours)>> originalTaskMap,double maxSeconds = 30) // 🚨 修改这里double maxSeconds = 30)
{
    // 1. intialization
    double initialOverload = state.PersonWeekHours.Values.Sum(v => Math.Max(0, v - 40));
    int maxWeek = state.Projects.Max(p => p.endDate > 0 ? p.endDate : 52);

    var model = new CpModel();
    var objective = LinearExpr.NewBuilder();
    
    var personLoadMap = state.People.ToDictionary(
        p => p.id, 
        p => (double)state.PersonWeekHours.Where(kv => kv.Key.PersonId == p.id).Sum(kv => kv.Value)
    );
    var rolePool = state.People.GroupBy(p => p.role ?? "").ToDictionary(g => g.Key, g => g.ToList());
    
    // 🚨 Add a tIdx field to the dictionary Key to ensure that multiple tasks in the same week are not overwritten
    var x = new Dictionary<(int pId, Project prj, int tW, int tIdx), BoolVar>();
    var personWeekLoad = new Dictionary<(int pId, int tW), LinearExprBuilder>();

    // 2. loop for each state/assigns/candidates/windows
    foreach (var p in state.Projects)
    {
        if (!originalTaskMap.TryGetValue(p.id, out var rawAssigns)) 
        {
            Console.WriteLine($"[WARNING] Project {p.id} not found in backup map.");
            continue;
        }
        int pDeadline = p.endDate > 0 ? p.endDate : 52;

        // 🚨 修复 B: 改为 for 循环以获取索引 i
        for (int i = 0; i < rawAssigns.Count; i++)
        {
            var assign = rawAssigns[i];
            var choices = new List<BoolVar>();
            var candidates = GetResourceExpansionCandidates(assign, state, rolePool, personLoadMap);

            foreach (var cand in candidates)
            {
                var windows = GetSoftenedTimeWindows(assign, state, p, pDeadline, maxWeek);

                foreach (var tW in windows)
                {
                    // 🚨 修复 C: 变量名加入索引 i，字典 Key 包含 i
                    var bv = model.NewBoolVar($"p{cand.id}_prj{p.id}_idx{i}_tw{tW}");
                    x[(cand.id, p, tW, i)] = bv; // 使用 i 保证唯一性
                    choices.Add(bv);

                    if (tW > pDeadline)
                        objective.AddTerm(bv, (tW - pDeadline) * W_LATENESS);

                    var key = (cand.id, tW);
                    if (!personWeekLoad.ContainsKey(key)) personWeekLoad[key] = LinearExpr.NewBuilder();
                    personWeekLoad[key].AddTerm(bv, assign.Hours);
                    
                    objective.AddTerm(bv, Math.Abs(tW - assign.Week) * W_MOVEMENT);
                }
            }

            if (choices.Count > 0) 
            {
                model.AddExactlyOne(choices);
            }
            else 
            {
                // 此时你可以根据 Role 打印出更详细的 Debug 信息
                var roleName = state.People.FirstOrDefault(per => per.id == assign.PersonId)?.role ?? "Unknown";
                Console.WriteLine($"[CRITICAL] Missing: Prj {p.id}, Week {assign.Week}, Hours {assign.Hours}, Role: {roleName}");
            }
        } 
    } 

    // 3. 应用约束与求解
    ApplyConflictConstraints(model, objective, personWeekLoad, state);

    model.Minimize(objective);
    var solver = new CpSolver();
    solver.StringParameters = $"max_time_in_seconds:{maxSeconds}, num_search_workers:6";
    var status = solver.Solve(model);

    // 4. 返回结果
    return ProcessResult(status, solver, x, personWeekLoad, initialOverload, state, originalTaskMap);
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

//Helper: Create a +/- 15 week window for each task. Max delay allowed is 8 weeks past deadline.
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
    private SolveResult ProcessResult(
        CpSolverStatus status, 
        CpSolver solver, 
        Dictionary<(int pId, Project prj, int tW, int tIdx), BoolVar> x, 
        Dictionary<(int pId, int tW), LinearExprBuilder> loadMap, 
        double initial, 
        ScheduleState state,
        Dictionary<int, List<(int PersonId, int Week, int Hours)>> originalTaskMap)

    {
        var res = new SolveResult { Status = status };

        if (status != CpSolverStatus.Feasible && status != CpSolverStatus.Optimal) return res;

        int delays = 0;
        int totalDelayWeeks = 0;
        int resourceSwaps = 0;
        double totalRecoveredHours = 0;

        foreach (var entry in x)
        {
            if (solver.Value(entry.Value) == 1)
            {
                var k = entry.Key; 
                var projectTasks = state.GetOriginalAssignments(k.prj);
                var task = originalTaskMap[k.prj.id][k.tIdx];
                
                totalRecoveredHours += task.Hours;

                // 🚨 现在这里的 4 参数 Key 能与 SolveResult 定义匹配了
                res.Assignments[(k.pId, k.prj, task.Week, k.tIdx)] = k.tW;

                int deadline = k.prj.endDate > 0 ? k.prj.endDate : 52;
                if (k.tW > deadline) 
                { 
                    delays++; 
                    totalDelayWeeks += (k.tW - deadline); 
                }
                
                int defaultId = k.prj.people.FirstOrDefault()?.id ?? -1;
                if (k.pId != defaultId) resourceSwaps++;
            }
        }

        Console.WriteLine($"\n========== [RECONCILIATION REPORT] ==========");
        Console.WriteLine($"Expected Total Hours: {initial}");
        Console.WriteLine($"Actual Recovered Hours: {totalRecoveredHours}");
        Console.WriteLine($"Difference: {Math.Round(initial - totalRecoveredHours, 2)}");
        Console.WriteLine($"==============================================\n");

        res.Report = new StrategyReport {
            ConflictReduced = initial - res.FinalOverload,
            TotalDelayWeeks = totalDelayWeeks,
            ResourceSwaps = resourceSwaps 
        };

        return res;
    }

}