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

    public SolveResult OptimizeShifts(ScheduleState state, double maxTime = 3000)
    {
        var model = new CpModel();
        var choose = new Dictionary<(Project P, int S), BoolVar>(); //Decision variable that lists what shift is actually chosen and is used to map the final move
        var validShiftsByProject = new Dictionary<Project, List<int>>(); //caches valid shifts so only used once
        var activeChoicesByPersonWeek = new Dictionary<(int personId, int Week), List<BoolVar>>(); //Keeps a list of work options for each person/week combo (used in load and conflict)
        var movementTerms = new List<LinearExpr>(); //Tracks movement terms and penalties for tiebreaker
        long maxMovementUpperBound = 0; //Max movement for normalization, used to ensure conflict minimization is the goal


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

        var conflicts = new List<BoolVar>();

        foreach (var k in activeChoicesByPersonWeek.OrderBy(x => x.Key.personId).ThenBy(x => x.Key.Week))
        {
            int personId = k.Key.personId;
            int week = k.Key.Week;
            var activeChoices = k.Value; //decisions that can place a person into the week

            if (activeChoices.Count <= 1) //if 0/1 possible assignments then conflict can't happen
            {
                continue;
            }

            var sum = LinearExpr.Sum(activeChoices); //counts how many decisions in a cell
            var conflict = model.NewBoolVar($"conflict_person{personId}_w{week}");
            model.Add(sum >= 2).OnlyEnforceIf(conflict); //enforce the conflict ie it is double booked
            model.Add(sum <= 1).OnlyEnforceIf(conflict.Not()); //do not enforce conflict 
            conflicts.Add(conflict);//minimizes total conflict


        }

        long conflictWeight = maxMovementUpperBound + 1; //conflict always worse than movement savings
        var totalConflictEx = LinearExpr.Sum(conflicts); //total conflict cells
        var totalMovementEx = LinearExpr.Sum(movementTerms); //total shifts
        model.Minimize(conflictWeight * totalConflictEx + totalMovementEx); //gives it conflicts first priority and movement as tie breaker
        var solver = new CpSolver(); //solver finds best assignment
        int workers = Math.Max(1, Environment.ProcessorCount); //using full core capacity now (hopefully no melting)
        solver.StringParameters = $"max_time_in_seconds:{maxTime},num_search_workers:{workers}"; //limits solve time and parallel searches
        var status = solver.Solve(model); //runs the solve and captures its status ie feasilbe, optimal
        var result = new SolveResult { Status = status };//inspects outcome

        if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible) //only decode if feasible or optimal solution
        {
            foreach (var p in state.Projects)
            {
                int chosenShift = validShiftsByProject[p][0];//starts at first possible shift in case a person/week only has 1 move ie no moves
                foreach (var s in validShiftsByProject[p])
                {
                    if (solver.Value(choose[(p, s)]) == 1) //returns 1 if a move was selected by solver
                    {
                        chosenShift = s;
                        break;
                    }
                }
                result.ChosenShiftByProject[p] = chosenShift; //saved decision

            }
            result.TotalConflicts = conflicts.Count(v => solver.Value(v) == 1); //counts total number of conflicts
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
}