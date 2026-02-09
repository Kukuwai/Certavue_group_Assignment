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
        var conflictVars = new List<BoolVar>();  // ← ADD THIS
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
            var conflict = model.NewBoolVar($"conflict_person{personId}_w{week}");

            model.Add(sum >= 2).OnlyEnforceIf(conflict);
            model.Add(sum <= 1).OnlyEnforceIf(conflict.Not());

            conflictVars.Add(conflict);
            conflictLoads[conflict] = sum; // Save for reporting only
        }

        // This matches your Program.testAlgo metric:
        // double-booked = sum(load where load >= 2) = sum(overage + conflictWeek)
        LinearExpr totalConflictEx;
        if (conflictVars.Count == 0)
        {
            totalConflictEx = LinearExpr.Constant(0);
        }
        else
        {
            totalConflictEx = LinearExpr.Sum(conflictVars);
        }

        LinearExpr totalMovementEx;
        if (movementTerms.Count == 0)
        {
            totalMovementEx = LinearExpr.Constant(0);
        }
        else
        {
            totalMovementEx = LinearExpr.Sum(movementTerms);
        }

        //Explicit vars so we can read totals and reuse in phase 2
        var totalConflictVar = model.NewIntVar(0, 1_000_000, "totalConflictVar");
        model.Add(totalConflictVar == totalConflictEx);

        var totalMovementVar = model.NewIntVar(0, (int)maxMovementUpperBound, "totalMovementVar");
        model.Add(totalMovementVar == totalMovementEx);


        model.Minimize(totalConflictVar);


        // long conflictWeight = maxMovementUpperBound + 1; //conflict always worse than movement savings
        // model.Minimize(conflictWeight * totalConflictEx + totalMovementEx); //gives it conflicts first priority and movement as tie breaker

        var solver = new CpSolver();
        int workers = Math.Max(1, Environment.ProcessorCount);

        // exact objective solve (remove relative_gap_limit so "Optimal" is true optimal)
        solver.StringParameters = $"max_time_in_seconds:{maxTime}," + $"num_search_workers:{workers}," + $"search_branching:PORTFOLIO_SEARCH," + $"randomize_search:true," + $"relative_gap_limit:0.01," + $"log_search_progress:false"; //stops within 1%


        // var stopCallback = new StopIfNoImprovementCallback(30.0); //30 seconds for improvement
        // var status = solver.Solve(model, stopCallback);
        // Stop if you hit 0 conflicts OR no improvement for 30s
        var stopCallback = new StopOnTargetOrIdleCallback(
            maxIdleSeconds: 30.0,
            totalConflictVar: totalConflictVar,
            targetConflicts: 0
        );

        var statusA = solver.Solve(model, stopCallback);



        CpSolver solverToDecode = solver;
        var statusFinal = statusA;

        if (statusA == CpSolverStatus.Optimal || statusA == CpSolverStatus.Feasible)
        {
            long bestConflicts = solver.Value(totalConflictVar);

            // Phase B: lock best conflicts, then minimize movement
            model.Add(totalConflictVar == bestConflicts);
            model.Minimize(totalMovementVar);

            var solverB = new CpSolver();
            solverB.StringParameters =
                $"max_time_in_seconds:{Math.Max(5, maxTime * 0.25)}," +
                $"num_search_workers:{workers}," +
                $"search_branching:PORTFOLIO_SEARCH," +
                $"randomize_search:true," +
                $"relative_gap_limit:0.02," +
                $"log_search_progress:false";

            var statusB = solverB.Solve(model);

            if (statusB == CpSolverStatus.Optimal || statusB == CpSolverStatus.Feasible)
            {
                solverToDecode = solverB;
                statusFinal = statusB;
            }
        }

        // CREATE RESULT *AFTER* statusFinal is known
        var result = new SolveResult { Status = statusFinal };

        // DECODE SOLUTION USING solverToDecode
        if (statusFinal == CpSolverStatus.Optimal || statusFinal == CpSolverStatus.Feasible)
        {
            foreach (var p in state.Projects)
            {
                int chosenShift = validShiftsByProject[p][0];
                foreach (var s in validShiftsByProject[p])
                {
                    if (solverToDecode.Value(choose[(p, s)]) == 1)
                    {
                        chosenShift = s;
                        break;
                    }
                }
                result.ChosenShiftByProject[p] = chosenShift;
            }
            // Calculate total conflicts from conflict vars directly
            int totalConflictCount = 0;
            foreach (var conflictVar in conflictVars)
            {
                if (solverToDecode.Value(conflictVar) == 1)
                {
                    // This week has a conflict
                    long load = solverToDecode.Value(conflictLoads[conflictVar]);
                    totalConflictCount += (int)load; // Total assignments in conflicted week
                }
            }
            result.TotalConflicts = totalConflictCount;
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