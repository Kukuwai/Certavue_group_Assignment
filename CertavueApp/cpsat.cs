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
        }

        SolveResult solve = null;
        return solve;
    }
}