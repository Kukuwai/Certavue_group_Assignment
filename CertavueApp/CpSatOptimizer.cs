using System;
using System.Collections.Generic;
using System.Linq;
using Google.OrTools.Sat;

public class CpSatOptimizer
{
    // 核心权重：确保 Conflict 是绝对优先级
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
        double initialOverload = state.PersonWeekHours.Values.Sum(v => Math.Max(0, v - 40));
        int maxWeek = state.Projects.Max(p => p.endDate > 0 ? p.endDate : 52);

        var model = new CpModel();
        var objective = LinearExpr.NewBuilder();
        
        // 1. 准备全局资源池（用于人力策略）
        var personLoadMap = state.People.ToDictionary(
        p => p.id, 
        p => (double)state.PersonWeekHours
        .Where(kv => kv.Key.PersonId == p.id)
        .Sum(kv => kv.Value)
        );
        var rolePool = state.People.GroupBy(p => p.role ?? "").ToDictionary(g => g.Key, g => g.ToList());

        var x = new Dictionary<(int pId, Project prj, int rW, int tW), BoolVar>();
        var personWeekLoad = new Dictionary<(int pId, int tW), LinearExprBuilder>();

        // 2. 遍历并注入策略
        foreach (var p in state.Projects)
        {
            var rawAssigns = state.GetOriginalAssignments(p);
            int pDeadline = p.endDate > 0 ? p.endDate : 52;

            foreach (var assign in rawAssigns)
            {
                var choices = new List<BoolVar>();
                
                // --- 策略 A: 人力增援策略 ---
                var candidates = GetResourceExpansionCandidates(assign, state, rolePool, personLoadMap);

                foreach (var cand in candidates)
                {
                    // --- 策略 B: 延期缓解策略 (获取可选周) ---
                    var windows = GetSoftenedTimeWindows(assign, state, p, pDeadline, maxWeek);

                    foreach (var tW in windows)
                    {
                        var bv = model.NewBoolVar($"p{cand.id}_prj{p.id}_r{assign.Week}_t{tW}");
                        x[(cand.id, p, assign.Week, tW)] = bv;
                        choices.Add(bv);

                        // 注入延期代价
                        if (tW > pDeadline)
                            objective.AddTerm(bv, (tW - pDeadline) * W_LATENESS);

                        // 记录负载
                        var key = (cand.id, tW);
                        if (!personWeekLoad.ContainsKey(key)) personWeekLoad[key] = LinearExpr.NewBuilder();
                        personWeekLoad[key].AddTerm(bv, assign.Hours);
                        
                        // 移动代价
                        objective.AddTerm(bv, Math.Abs(tW - (assign.Week + state.GetShift(p))) * W_MOVEMENT);
                    }
                }
                if (choices.Count > 0) model.AddExactlyOne(choices);
            }
        }

        // 3. 冲突惩罚逻辑
        ApplyConflictConstraints(model, objective, personWeekLoad, state);

        // 4. 求解
        model.Minimize(objective);
        var solver = new CpSolver();
        solver.StringParameters = $"max_time_in_seconds:{maxSeconds}, num_search_workers:8";
        var status = solver.Solve(model);

        // 5. 解码并生成策略报告
        return ProcessResult(status, solver, x, personWeekLoad, initialOverload, state);
    }

// --- [独立方法] 延期缓解策略 ---
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

// --- [策略方法 B：延期缓解] ---
private List<int> GetSoftenedTimeWindows((int PersonId, int Week, int Hours) assign, ScheduleState state, Project p, int deadline, int maxWeek)
{
    // 获取当前的偏移量
    int currentShift = state.GetShift(p);
    int center = assign.Week + currentShift;
    
    var weeks = new List<int>();
    // 搜索窗口：前后15周，最大延期8周
    for (int i = center - 15; i <= center + 15; i++)
    {
        if (i >= 1 && i <= deadline + 8 && i <= maxWeek) 
            weeks.Add(i);
    }
    return weeks;
}

    private void ApplyConflictConstraints(CpModel model, LinearExprBuilder objective, Dictionary<(int pId, int tW), LinearExprBuilder> loadMap, ScheduleState state)
    {
        foreach (var kv in loadMap)
        {
            var overload = model.NewIntVar(0, 2000, "");
            model.Add(overload >= kv.Value - 40);
            objective.AddTerm(overload, W_CONFLICT);
        }
    }

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
            ResourceSwaps = resourceSwaps // 对齐名称
        };
        return res;
    }


}