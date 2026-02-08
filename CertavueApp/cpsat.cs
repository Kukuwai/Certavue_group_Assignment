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

    public SolveResult OptimizeShifts(ScheduleState state, double maxTime = 300)
    {
        var model = new CpModel();
        var choose = new Dictionary<(Project P, int S), BoolVar>(); //project and shift, 1 when selected
        var validShiftsByProject = new Dictionary<Project, List<int>>(); //caches valid shifts so only used once
        var activeChoicesByPersonWeek = new Dictionary<(int personId, int Week), List<BoolVar>>(); //Keeps a list of work options for each person/week combo
        var movementTerms = new List<LinearExpr>(); //Tracks movement terms
        long maxMovementUpperBound = 0; //Max movement for normalization


        foreach (var p in state.Projects)
        {
            var shifts = state.GetValidShifts(p);

            if (shifts.Count == 0)
            {
                shifts = new List<int> { state.GetShift(p) };
            }
            validShiftsByProject[p] = shifts;
            foreach (var s in shifts)
            {
                choose[(p, s)] = model.NewBoolVar($"choose_p{p.id}_s{s}");
            }
            model.Add(LinearExpr.Sum(shifts.Select(s => choose[(p, s)])) == 1); //takes one shift for every project

            int currentShift = state.GetShift(p);
            int maxDistanceForProject = 0;


            foreach (var s in shifts)
            {
                int distance = Math.Abs(s - currentShift);
                maxDistanceForProject = Math.Max(maxDistanceForProject, distance);
                if (distance > 0)//penalty for moving
                {
                    movementTerms.Add(distance * choose[(p, s)]);
                }
                var grid = new HashSet<(int PersonId, int Week)>();

                foreach (var cell in state.GetGrid(p, s))
                {
                    var key = (cell.PersonId, cell.Week);

                    if (!grid.Add(key))
                    {
                        continue;
                    }

                    if (!activeChoicesByPersonWeek.TryGetValue(key, out var varsHere))
                    {
                        varsHere = new List<BoolVar>();
                        activeChoicesByPersonWeek[key] = varsHere;
                    }
                    varsHere.Add(choose[(p, s)]);
                }
            }

            maxMovementUpperBound += maxDistanceForProject;

        }

        var conflicts = new List<BoolVar>();

        foreach (var k in activeChoicesByPersonWeek.OrderBy(x => x.Key.personId).ThenBy(x => x.Key.Week))
        {
            int personId = k.Key.personId;
            int week = k.Key.Week;
            var activeChoices = k.Value;

            if (activeChoices.Count <= 1)
            {
                continue;
            }
            var load = model.NewIntVar(0, activeChoices.Count, $"load_person{personId}_w{week}");
            model.Add(load == LinearExpr.Sum(activeChoices));
            var conflict = model.NewBoolVar($"conflict_person{personId}_w{week}");
            model.Add(load >= 2).OnlyEnforceIf(conflict);
            model.Add(load <= 1).OnlyEnforceIf(conflict.Not());
            conflicts.Add(conflict);
        }

        long conflictWeight = maxMovementUpperBound + 1;
        var totalConflictEx = LinearExpr.Sum(conflicts);
        var totalMovementEx = LinearExpr.Sum(movementTerms);
        model.Minimize(conflictWeight * totalConflictEx + totalMovementEx);
        var solver = new CpSolver();
        int workers = Math.Max(1, Environment.ProcessorCount - 1);
        solver.StringParameters = $"max_time_in_seconds:{maxTime},num_search_workers:{workers}";
        var status = solver.Solve(model);
        var result = new SolveResult { Status = status };

        if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
        {
            foreach (var p in state.Projects)
            {
                int chosenShift = validShiftsByProject[p][0];
                foreach (var s in validShiftsByProject[p])
                {
                    if (solver.Value(choose[(p, s)]) == 1)
                    {
                        chosenShift = s;
                        break;
                    }
                }
                result.ChosenShiftByProject[p] = chosenShift;

            }
            result.TotalConflicts = conflicts.Count(v => solver.Value(v) == 1);
        }

        return result;

    }

    public void ApplySolution(ScheduleState state, SolveResult result)
    {
        foreach(var k in result.ChosenShiftByProject)
        {
            state.ApplyShift(k.Key, k.Value);
        }
    }
}