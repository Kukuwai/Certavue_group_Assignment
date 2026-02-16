using System;
using System.Collections.Generic;
using System.Linq;
using Google.OrTools.Sat;

public class CpSatOptimizer
{
    // 数字化镜像权重：基于 ScheduleHandler 的 FitnessScore 比例 (0.4, 0.2, 0.2, 0.1, 0.1)
    private const int W_CONFLICT = 400;   
    private const int W_MOVEMENT = 200;   
    private const int W_FOCUS = 200;      
    private const int W_CONTINUITY = 100; 
    private const int W_DURATION = 100;   

    public class SolveResult
    {
        public CpSolverStatus Status { get; set; }
        public double TotalOverloadHours { get; set; }
        public Dictionary<(int PersonId, Project Project, int RawWeek), int> FinalAssignments { get; set; } = new();
    }

    public SolveResult Optimize(ScheduleState state, double maxTimeInSeconds = 30)
    {
        var model = new CpModel();
        var objective = new LinearExprBuilder();

        // 1. 变量定义池
        var x = new Dictionary<(int pId, Project prj, int rW, int tW), BoolVar>();
        var personWeekLoad = new Dictionary<(int pId, int tW), LinearExprBuilder>();
        var personWeekProjects = new Dictionary<(int pId, int tW), List<BoolVar>>();
        var projectBoundaries = new Dictionary<Project, (IntVar Start, IntVar End)>();

        // 预分析：找出有冲突的人选
        var conflictedPersonIds = GetConflictedPersonIds(state);

        // --- 第一步：地基构建 (变量、换人逻辑、Deadline) ---
        foreach (var p in state.Projects)
        {
            var rawAssignments = state.GetOriginalAssignments(p);
            int currentShift = state.GetShift(p);
            int pDeadline = p.endDate > 0 ? p.endDate : 52; 

            var pStart = model.NewIntVar(1, 52, $"start_{p.id}");
            var pEnd = model.NewIntVar(1, 52, $"end_{p.id}");
            projectBoundaries[p] = (pStart, pEnd);

            foreach (var assign in rawAssignments)
            {
                var choices = new List<BoolVar>();
                foreach (var cand in p.people)
                {
                    int window = conflictedPersonIds.Contains(cand.id) ? 2 : 0;
                    int center = assign.Week + currentShift;

                    for (int tW = Math.Max(1, center - window); tW <= Math.Min(52, center + window); tW++)
                    {
                        if (tW > pDeadline) continue; // 硬约束：截止日期

                        var bv = model.NewBoolVar($"p{cand.id}_prj{p.id}_r{assign.Week}_t{tW}");
                        x[(cand.id, p, assign.Week, tW)] = bv;
                        choices.Add(bv);

                        // 1.1 累加负载 (Conflict Score 基石)
                        var loadKey = (cand.id, tW);
                        if (!personWeekLoad.ContainsKey(loadKey)) personWeekLoad[loadKey] = new LinearExprBuilder();
                        personWeekLoad[loadKey].AddTerm(bv, assign.Hours);

                        // 1.2 记录该周参与的项目 (Focus Score 基石)
                        if (!personWeekProjects.ContainsKey(loadKey)) personWeekProjects[loadKey] = new List<BoolVar>();
                        personWeekProjects[loadKey].Add(bv);

                        // 1.3 约束项目边界 (Duration Score 基石)
                        model.Add(pStart <= tW).OnlyEnforceIf(bv);
                        model.Add(pEnd >= tW).OnlyEnforceIf(bv);

                        // 1.4 Movement Score 惩罚项 (趋向保持原位)
                        int dist = Math.Abs(tW - center);
                        if (dist > 0) objective.AddTerm(bv, dist * W_MOVEMENT);
                    }
                }
                model.AddExactlyOne(choices);
            }

            // 2. Duration Score 镜像 (权重 10%)
            var span = model.NewIntVar(0, 52, $"span_{p.id}");
            model.Add(span == pEnd - pStart);
            objective.AddTerm(span, W_DURATION);
        }

        // --- 第二步：Conflict Score 镜像 (权重 40%) ---
        var overloadVars = new List<IntVar>();
        foreach (var kv in personWeekLoad)
        {
            var person = state.People.First(pe => pe.id == kv.Key.pId);
            int cap = person.capacity > 0 ? person.capacity : 40;
            var overload = model.NewIntVar(0, 200, $"ov_p{kv.Key.pId}_w{kv.Key.tW}");
            model.Add(overload >= kv.Value - cap);
            overloadVars.Add(overload);
            objective.AddTerm(overload, W_CONFLICT);
        }

        // --- 第三步：Focus Score 镜像 (权重 20%) ---
        foreach (var kv in personWeekProjects)
        {
            var isMulti = model.NewBoolVar($"multi_p{kv.Key.pId}_w{kv.Key.tW}");
            var sumProjects = LinearExpr.Sum(kv.Value);
            model.Add(sumProjects > 1).OnlyEnforceIf(isMulti);
            objective.AddTerm(isMulti, W_FOCUS);
        }

        // --- 第四步：Continuity Score 镜像 (权重 10%) ---
        ApplyContinuityScoring(model, objective, x, state.Projects);

        // --- 第五步：求解器配置 (LNS 策略) ---
        model.Minimize(objective);
        var solver = new CpSolver();
        solver.StringParameters = $"max_time_in_seconds:{maxTimeInSeconds}, num_search_workers:8, cp_model_presolve:true, relative_mip_gap:0.05, lns_focus_on_trained_model:true";

        // 注入 Hint (Greedy 2.0 成果)
        foreach (var entry in x)
            if (entry.Key.tW == entry.Key.rW + state.GetShift(entry.Key.prj))
                model.AddHint(entry.Value, 1);

        var status = solver.Solve(model);
        return DecodeResult(status, solver, x, overloadVars);
    }

    private void ApplyContinuityScoring(CpModel model, LinearExprBuilder objective, Dictionary<(int pId, Project prj, int rW, int tW), BoolVar> x, List<Project> projects)
    {
        foreach (var p in projects)
        {
            foreach (var person in p.people)
            {
                var myTasks = x.Keys.Where(k => k.pId == person.id && k.prj == p).Select(k => k.rW).Distinct().OrderBy(w => w).ToList();
                if (myTasks.Count < 2) continue;
                for (int i = 0; i < myTasks.Count - 1; i++)
                {
                    var tW1 = GetTargetWeekExpr(person.id, p, myTasks[i], x);
                    var tW2 = GetTargetWeekExpr(person.id, p, myTasks[i+1], x);
                    var gap = model.NewIntVar(0, 52, "");
                    model.Add(gap >= tW2 - tW1 - 1);
                    objective.AddTerm(gap, W_CONTINUITY);
                }
            }
        }
    }

    private LinearExpr GetTargetWeekExpr(int pId, Project prj, int rW, Dictionary<(int pId, Project prj, int rW, int tW), BoolVar> x)
    {
        var expr = new LinearExprBuilder();
        var options = x.Where(kv => kv.Key.pId == pId && kv.Key.prj == prj && kv.Key.rW == rW);
        foreach (var opt in options) expr.AddTerm(opt.Value, opt.Key.tW);
        return expr ;
    }

    private HashSet<int> GetConflictedPersonIds(ScheduleState state)
    {
        return state.PersonWeekHours
            .Where(kv => kv.Value > (state.People.FirstOrDefault(p => p.id == kv.Key.PersonId)?.capacity ?? 40))
            .Select(kv => kv.Key.PersonId).ToHashSet();
    }

    private SolveResult DecodeResult(CpSolverStatus status, CpSolver solver, Dictionary<(int pId, Project prj, int rW, int tW), BoolVar> x, List<IntVar> overloadVars)
    {
        var result = new SolveResult { Status = status };
        if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
        {
            foreach (var entry in x)
                if (solver.Value(entry.Value) == 1)
                    result.FinalAssignments[(entry.Key.pId, entry.Key.prj, entry.Key.rW)] = entry.Key.tW;
            result.TotalOverloadHours = overloadVars.Sum(v => (double)solver.Value(v));
        }
        return result;
    }

    public void ApplySolution(ScheduleState state, SolveResult result)
    {
        state.UpdateFromFineGrainedAssignments(result.FinalAssignments);
    }
}