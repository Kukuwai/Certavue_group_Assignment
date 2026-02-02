using System;
using System.Collections.Generic;
using System.Linq;
using static ScheduleState;

public class GreedyAlg
{
    public ScheduleState StartGreedy(List<Person> people, List<Project> projects)
    {
        //state keeps track of shifts
        var state = new ScheduleState(people, projects);
        BuildGreedySchedule(state);

        return state; // I added this for to work with finding conflicts. 
    }
    public void BuildGreedySchedule(ScheduleState state)
    {
        const int maxPasses = 6; //seeing if this improves perfornmancesince greedy is cheap.it can be any number really
        //for file 75 large should start with 1036 and 36.39% double booked
        int startTotal = state.PersonWeekGrid.Values.Sum();
        int startNonConflict = state.PersonWeekGrid.Where(kv => kv.Value == 1).Sum(kv => kv.Value);
        int startDouble = state.PersonWeekGrid.Count(kv => kv.Value >= 2);
        double startPct;
        if (startTotal == 0)
        {
            startPct = 100.0;
        }
        else
        {
            startPct = (double)startNonConflict / startTotal * 100.0;
        }
        Console.WriteLine("Greedy algorithm running: ");

        Console.WriteLine("Start total: " + startTotal + ", double-booked=" + startDouble + ", % not double-booked=" + startPct.ToString("0.##"));


        for (int pass = 1; pass <= maxPasses; pass++)
        {
            var ordered = state.Projects
            .OrderByDescending(p => state.GetDuration(p))   //longest projs first
            .ThenByDescending(p => p.people.Count)          //breaks tie by most people on proj
            .ToList();

            bool anyShifted = false; //track if any schedules are moved 

            foreach (var project in ordered)  //tracks projects in order
            {
                int currentShift = state.GetShift(project);
                int bestShift = currentShift;
                ShiftScore best = EvaluateShift(state, project, currentShift); // baseline

                foreach (int candidate in state.GetValidShifts(project)) //all allowed shifts ie within dates
                {
                    ShiftScore test = EvaluateShift(state, project, candidate);

                    //goes in order fewer double booked, overlap and then shortest move
                    bool better =
                        test.DeltaDoubleBooked < best.DeltaDoubleBooked ||
                        (test.DeltaDoubleBooked == best.DeltaDoubleBooked && test.OverlapAfter < best.OverlapAfter) ||
                        (test.DeltaDoubleBooked == best.DeltaDoubleBooked && test.OverlapAfter == best.OverlapAfter && test.ShiftDistance < best.ShiftDistance);

                    if (better)
                    {
                        bestShift = candidate; //tracks best shift
                        best = test; //keeps the best
                    }
                }

                if (bestShift != currentShift)
                {
                    state.ApplyShift(project, bestShift); //implements the move
                    anyShifted = true;  //used to make sure changes occurred
                }
            }
            int total = state.PersonWeekGrid.Values.Sum();  //all time slots
            int nonConflict = state.PersonWeekGrid.Where(kv => kv.Value == 1).Sum(kv => kv.Value);  //clean slots aka not double booked
            int doubleBooked = state.PersonWeekGrid.Count(kv => kv.Value >= 2); //double booked
            double pct;  //% not double booked
            if (total == 0)
            {
                pct = 100;
            }

            else
            {
                pct = (double)nonConflict / total * 100;

            }

            Console.WriteLine("After pass " + pass + ", total: " + total + ", double-booked=" + doubleBooked + ", % not double-booked=" + pct.ToString("0.##"));

            if (!anyShifted) break; //ends if nothing moves so we really could have the passes be pretty high for safety
        }
    }

    // Holds the scoring results for a candidate shift
    public class ShiftScore
    {
        public int DeltaDoubleBooked { get; set; } //double booked change
        public int OverlapAfter { get; set; } //remaining double booked after a move
        public int ShiftDistance { get; set; } //shift size aka smaller may = better
    }

    // Returns a ShiftScore
    public ShiftScore EvaluateShift(ScheduleState state, Project project, int candidateShift)
    {   
        const int CONFLICT_WEIGHT = 50;
        int currentShift = state.GetShift(project);

        List<WeekKey> current = new List<WeekKey>(state.GetGrid(project, currentShift)); //weeks at current shift
        List<WeekKey> candidate = new List<WeekKey>(state.GetGrid(project, candidateShift)); //weeks at new shift


        List<WeekKey> touched = new List<WeekKey>(current); //any cells impacted by change
        foreach (WeekKey k in candidate)
        {
            if (!touched.Contains(k))
                touched.Add(k);
        }

        int delta = 0; //change in 2x bookings
        int overlapAfter = 0; //how many double bookings after move

        foreach (WeekKey key in touched)
        {
            int baseCount = 0;
            state.PersonWeekGrid.TryGetValue(key, out baseCount); //current bookings from all projects
            if (current.Contains(key)) baseCount -= 1; //removes current projects placement

            //adds the canidate project p;cement
            int newCount;
            if (candidate.Contains(key))
            {
                newCount = baseCount + 1;
            }
            else
            {
                newCount = baseCount + 0;
            }
            if (baseCount >= 2) delta -= 1; //double booked pre move
            if (newCount >= 2) //double booked after move
            {
                delta += 1;
                overlapAfter++;
            }
            int conflictPenalty = 0;
            foreach (var kv in state.PersonWeekGrid)
            {
                if (kv.Value > 1)
                    conflictPenalty += (kv.Value - 1);
            }
        }


        return new ShiftScore  //result of shift
        {
            DeltaDoubleBooked = delta,
            OverlapAfter = overlapAfter,
            ShiftDistance = Math.Abs(candidateShift)
        };
    }
   
}