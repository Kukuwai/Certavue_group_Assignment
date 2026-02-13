using System;
using System.Collections.Generic;
using System.Linq;
using Google.OrTools.Sat;

public class cpsat
{
    public class SolveResult
    {
        public CpSolverStatus Status { get; set; }
        public int TotalConflicts { get; set; }
        public Dictionary<Project, int> ChosenShiftByProject { get; set; } = new();
    }

    public SolveResult OptimizeShifts(ScheduleState state, double maxTime = 3600)
    {
        var model = new CpModel();
        
        // 1. 变量定义
        var choose = new Dictionary<(Project P, int S), BoolVar>();
        var validShiftsByProject = new Dictionary<Project, List<int>>();
        var activeChoicesByPersonWeek = new Dictionary<(int personId, int Week), List<BoolVar>>();
        var movementTerms = new List<LinearExpr>();
        var conflictVars = new List<IntVar>();

        // 2. 遍历项目，建立决策变量
        foreach (var p in state.Projects)
        {
            var shifts = state.GetValidShifts(p);

            // 给予足够的移动空间
            if (shifts.Count < 3)
            {
                int cur = state.GetShift(p);
                shifts = Enumerable.Range(cur - 2, 5).ToList();
            }
            validShiftsByProject[p] = shifts;

            foreach (var s in shifts)
            {
                var bv = model.NewBoolVar($"choose_p{p.id}_s{s}");
                choose[(p, s)] = bv;

                // 记录移动代价
                int distance = Math.Abs(s - state.GetShift(p));
                if (distance > 0)
                {
                    movementTerms.Add(distance * bv);
                }

                // 记录该选项涉及的所有人员-周格子
                var grid = new HashSet<(int PersonId, int Week)>();
                foreach (var cell in state.GetGrid(p, s))
                {
                    var key = (cell.PersonId, cell.Week);
                    if (!grid.Add(key)) continue;

                    if (!activeChoicesByPersonWeek.TryGetValue(key, out var varsHere))
                    {
                        varsHere = new List<BoolVar>();
                        activeChoicesByPersonWeek[key] = varsHere;
                    }
                    varsHere.Add(bv);
                }
            }
            
            // 每个项目必须选且只能选一个班次
            model.AddExactlyOne(shifts.Select(s => choose[(p, s)]));

            // 添加 Hint 帮助求解器快速找到初始解
            int currentShift = state.GetShift(p);
            if (shifts.Contains(currentShift))
            {
                model.AddHint(choose[(p, currentShift)], 1);
            }
        }

        // 3. 建立冲突（过载）变量
        foreach (var k in activeChoicesByPersonWeek)
        {
            var activeChoices = k.Value;
            if (activeChoices.Count <= 1) continue;

            // overage 代表除了第一个项目外，多出来的任务数（即冲突数）
            var overage = model.NewIntVar(0, activeChoices.Count, $"overage_p{k.Key.personId}_w{k.Key.Week}");
            model.Add(overage >= LinearExpr.Sum(activeChoices) - 1);
            
            conflictVars.Add(overage);
        }

        // 4. 获取 Handler 权重并映射到目标函数
        var config = new ScheduleHandler.FitnessScore();
        long baseWeight = 1_000_000;
        long cpConflictWeight = (long)(config.ConflictWeight * baseWeight);
        long cpMovementWeight = (long)(config.MovementWeight * baseWeight);

        LinearExpr totalConflictPenalty = LinearExpr.Constant(0);
        if (conflictVars.Count > 0)
        {
            totalConflictPenalty = LinearExpr.WeightedSum(conflictVars, Enumerable.Repeat(cpConflictWeight, conflictVars.Count));
        }

        LinearExpr totalMovementPenalty = LinearExpr.Constant(0);
        if (movementTerms.Count > 0)
        {
            // 将位移权重平摊到每个项目上，避免位移分数值过大遮盖冲突分数
            long movementStepWeight = cpMovementWeight / Math.Max(1, state.Projects.Count);
            totalMovementPenalty = LinearExpr.WeightedSum(movementTerms, Enumerable.Repeat(movementStepWeight, movementTerms.Count));
        }

        // 最小化惩罚：冲突最重要，位移次之
        model.Minimize(totalConflictPenalty + totalMovementPenalty);

        // 5. 配置求解器
        var solver = new CpSolver();
        int workers = Math.Max(1, Environment.ProcessorCount);
        solver.StringParameters = $"max_time_in_seconds:{maxTime}," +
                                 $"num_search_workers:{workers}," +
                                 $"log_search_progress:false," + // 彻底关掉日志
                                 $"randomize_search:true";

        // 绑定冲突变量到 Callback 以便观察进度
        IntVar totalConflictSumVar = model.NewIntVar(0, 1000000, "total_conflicts");
        model.Add(totalConflictSumVar == LinearExpr.Sum(conflictVars));

        var stopCallback = new StopOnTargetOrIdleCallback(
            maxIdleSeconds: 15.0, 
            totalConflictVar: totalConflictSumVar, 
            targetConflicts: 0 
        );

        // 6. 执行求解
        var statusFinal = solver.Solve(model, stopCallback);

        // 7. 解码结果
        var result = new SolveResult { Status = statusFinal };
        if (statusFinal == CpSolverStatus.Optimal || statusFinal == CpSolverStatus.Feasible)
        {
            foreach (var p in state.Projects)
            {
                foreach (var s in validShiftsByProject[p])
                {
                    if (solver.Value(choose[(p, s)]) == 1)
                    {
                        result.ChosenShiftByProject[p] = s;
                        break;
                    }
                }
            }
            result.TotalConflicts = (int)solver.Value(totalConflictSumVar);
        }
        else
        {
            result.TotalConflicts = -1;
        }

        return result;
    }

    public void ApplySolution(ScheduleState state, SolveResult result)
    {
        foreach (var k in result.ChosenShiftByProject)
        {
            state.ApplyShift(k.Key, k.Value);
        }
    }

    public class StopOnTargetOrIdleCallback : CpSolverSolutionCallback
    {
        private readonly double _maxIdleSeconds;
        private readonly IntVar _totalConflictVar;
        private readonly long _targetConflicts;
        private DateTime _lastImprovement;
        private long _bestConflicts = long.MaxValue;

        public StopOnTargetOrIdleCallback(double maxIdleSeconds, IntVar totalConflictVar, long targetConflicts)
        {
            _maxIdleSeconds = maxIdleSeconds;
            _totalConflictVar = totalConflictVar;
            _targetConflicts = targetConflicts;
            _lastImprovement = DateTime.Now;
        }

        public override void OnSolutionCallback()
        {
            long conflicts = Value(_totalConflictVar);
            if (conflicts < _bestConflicts)
            {
                _bestConflicts = conflicts;
                _lastImprovement = DateTime.Now;
            }

            if (conflicts <= _targetConflicts || (DateTime.Now - _lastImprovement).TotalSeconds > _maxIdleSeconds)
            {
                StopSearch();
            }
        }
    }
}