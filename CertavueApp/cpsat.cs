using System;
using System.Collections.Generic;
using System.Linq;
using Google.OrTools.Sat;

public class cpsat
{
    public class SolveResult
    {
        public CpSolverStatus Status { get; set; } //using this to set if it solved or not
        public int TotalConflicts { get; set; } //counts # of conflicts for final solution, will report final #
        public Dictionary<Project, int> ChosenShiftByProject { get; set; } = new(); //final shift for each prohect
    }

    public SolveResult OptimizeShifts(ScheduleState state, double maxTime = 3600)
    {
        var model = new CpModel();
        var choose = new Dictionary<(Project P, int S), BoolVar>(); //Decision variable that lists what shift is actually chosen and is used to map the final move
        var validShiftsByProject = new Dictionary<Project, List<int>>(); //caches valid shifts so only used once
        var activeChoicesByPersonWeek = new Dictionary<(int personId, int Week), List<BoolVar>>(); //Keeps a list of work options for each person/week combo (used in load and conflict)
        var movementTerms = new List<LinearExpr>();
        var conflictVars = new List<IntVar>();  // ← ADD THIS
        var conflictLoads = new Dictionary<BoolVar, LinearExpr>();  // ← ADD THIS
        long maxMovementUpperBound = 0;


        foreach (var p in state.Projects)
        {
            var shifts = state.GetValidShifts(p); //finds the allowed shifts and stores for each project

            if (shifts.Count == 0) //no legal moves still needs an option
            {
                shifts = new List<int> { state.GetShift(p) }; //sets only choice as current spot
            }
            validShiftsByProject[p] = shifts; //saves options for after solve
            foreach (var s in shifts)
            {
                choose[(p, s)] = model.NewBoolVar($"choose_p{p.id}_s{s}"); //makes a pick or not pick decision 
            }
            model.AddExactlyOne(shifts.Select(s => choose[(p, s)])); //takes one shift for every project

            int currentShift = state.GetShift(p);//takes how far the move is 
            if (shifts.Contains(currentShift))
            {
                model.AddHint(choose[(p, currentShift)], 1);
            }

            int maxDistanceForProject = 0; //worst movement possible for binding


            foreach (var s in shifts)
            {
                int distance = Math.Abs(s - currentShift); //measures how much the shift was
                maxDistanceForProject = Math.Max(maxDistanceForProject, distance); //bound calculation

                if (distance > 0)//penalty for moving
                {
                    movementTerms.Add(distance * choose[(p, s)]); //cost paid for moving, penalty tiebreaker like above
                }
                var grid = new HashSet<(int PersonId, int Week)>(); //hash to prevent duplicates

                foreach (var cell in state.GetGrid(p, s))
                {
                    var key = (cell.PersonId, cell.Week);

                    if (!grid.Add(key)) //skips if dup
                    {
                        continue;
                    }

                    if (!activeChoicesByPersonWeek.TryGetValue(key, out var varsHere)) //list if this is a new person and week combo
                    {
                        varsHere = new List<BoolVar>(); //list of all possible decisions
                        activeChoicesByPersonWeek[key] = varsHere;
                    }
                    varsHere.Add(choose[(p, s)]); //records the load of options
                }
            }

            maxMovementUpperBound += maxDistanceForProject; //worst case movement

        }

        foreach (var k in activeChoicesByPersonWeek)
        {
            var activeChoices = k.Value;
            if (activeChoices.Count <= 1) continue;

            int personId = k.Key.personId;
            int week = k.Key.Week;

            var sum = LinearExpr.Sum(activeChoices);
            var overage = model.NewIntVar(0, activeChoices.Count, $"overage_p{k.Key.personId}_w{k.Key.Week}");
    
    // 核心约束：overage >= sum - 1
    // 如果 sum 为 1，overage 为 0；如果 sum 为 3，overage 为 2
            model.Add(overage >= sum - 1);
    
            conflictVars.Add(overage); // 注意：这里现在存的是 IntVar，代表超载深度
        }

        // This matches your Program.testAlgo metric:
        // double-booked = sum(load where load >= 2) = sum(overage + conflictWeek)
        // --- 1. 目标函数整合 (替换原有的 totalConflictEx, totalMovementEx 及 Phase A/B) ---
        LinearExpr totalConflictScore = LinearExpr.Constant(0);
        LinearExpr totalMovementScore = LinearExpr.Constant(0);
        // 定义冲突权重。10000 意味着减少 1 个冲突比减少 10000 个位移单位更重要。
        long conflictWeight = 10000;

        // 计算加权后的总分：(超载总数 * 10000) + (位移总数)
        // 这里的 conflictVars 必须是之前建议修改后的记录“超载深度”的变量
        // --- 修正后的目标函数计算 ---
        LinearExpr finalObjective = LinearExpr.Constant(0);
        
        // 1. 加入冲突项 (权重 10000)
        if (conflictVars != null && conflictVars.Count > 0)
        {
            finalObjective += LinearExpr.WeightedSum(conflictVars, Enumerable.Repeat(conflictWeight, conflictVars.Count));
        }

        // 2. 加入位移项 (权重 1)
        if (movementTerms != null && movementTerms.Count > 0)
        {
            finalObjective += LinearExpr.Sum(movementTerms);
        }

        // 3. 告诉求解器最小化这个包含冲突和位移的总分
        model.Minimize(finalObjective);

        // --- 2. 配置求解器 (只跑一次) ---

        var solver = new CpSolver();
        int workers = Math.Max(1, Environment.ProcessorCount);

        // 开启 log_search_progress: true 可以让你在控制台看到分数如何一步步下降
        solver.StringParameters = $"max_time_in_seconds:{maxTime}," + 
                                 $"num_search_workers:{workers}," + 
                                 $"search_branching:PORTFOLIO_SEARCH," + 
                                 $"randomize_search:true," + 
                                 $"relative_gap_limit:0.01," + 
                                 $"log_search_progress:true"; 

        // 依然可以使用回调函数来检测“0冲突”或“长时间无改善”
        // 注意：这里的 target 设为很小的值，因为现在 objective 包含了位移
        var stopCallback = new StopOnTargetOrIdleCallback(
            maxIdleSeconds: 30.0,
            totalConflictVar: model.NewIntVar(0, 1000000, "temp"), // 仅占位，回调逻辑需适配新目标
            targetConflicts: 0 
        );

        // 开始求解
        var statusFinal = solver.Solve(model);
        CpSolver solverToDecode = solver;

        // --- 3. 结果解码 ---

        // --- 1. 创建结果对象 ---
        var result = new SolveResult { Status = statusFinal };

        // --- 2. 如果求解成功（找到可行解或最优解） ---
        if (statusFinal == CpSolverStatus.Optimal || statusFinal == CpSolverStatus.Feasible)
        {
            // 2a. 记录每个项目选中的 Shift
            foreach (var p in state.Projects)
            {
                // 默认选第一个合法位移（防止意外）
                int chosenShift = validShiftsByProject[p][0]; 
                foreach (var s in validShiftsByProject[p])
                {
                    // solverToDecode.Value 为 1 表示这个 (项目,位移) 组合被选中了
                    if (solverToDecode.Value(choose[(p, s)]) == 1)
                    {
                        chosenShift = s;
                        break;
                    }
                }
                result.ChosenShiftByProject[p] = chosenShift;
            }

            // 2b. 重新计算冲突数 (这是修复 78 的关键)
            int finalConflictCount = 0;
            foreach (var overageVar in conflictVars)
            {
                // overageVar 存的是 (该槽位总人数 - 1)
                // 如果一个人，值就是 0；如果两个人，值就是 1
                long val = solverToDecode.Value(overageVar);
                if (val > 0)
                {
                    finalConflictCount += (int)val;
                }
            }
            
            // 确保把算出来的 0 赋值给 result
            result.TotalConflicts = finalConflictCount; 
        }
        else
        {
            // 如果没找到解，冲突数设为 -1 表示失败
            result.TotalConflicts = -1;
        }

        return result;
     }

    public void ApplySolution(ScheduleState state, SolveResult result) //applies the solveronto the state and does the actual updating
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
                // Console.WriteLine($"[Improvement] conflicts={conflicts}");
            }

            // Stop immediately if we hit the target (usually 0 conflicts)
            if (conflicts <= _targetConflicts)
            {
                StopSearch();
                return;
            }

            // Stop if no improvement recently
            if ((DateTime.Now - _lastImprovement).TotalSeconds > _maxIdleSeconds)
            {
                StopSearch();
            }
        }
    }

}