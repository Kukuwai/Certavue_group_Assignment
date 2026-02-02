using System.Data;
//using static ScheduleState;
using static Program;
using static Person;
using static GreedyAlg;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.CompilerServices;

public class MoveByConflict
{

    const int maxShift = 3;
    const int maxPasses = 6;



    public ScheduleState start(ScheduleState state, List<Project> projects)
    {
        Dictionary<Project, List<int>> canidates = new Dictionary<Project, List<int>>(); //determines max shift #
        foreach (Project p in projects)
        {
            int current = state.GetShift(p);     //current shift on project
            List<int> shifts = new List<int>(); //list of valid shifts for this project
            foreach (int s in state.GetValidShifts(p)) //considers max shift ie 3 or s/e
            {
                if (Math.Abs(s - current) <= maxShift)
                {
                    shifts.Add(s);  //adds if allowed
                }
            }
            if (shifts.Count == 0)
            {
                shifts.Add(current);        //
            }
            canidates[p] = shifts;
        }

        List<Project> ordered = GetOrderedProjects(projects, state);

        int bestConflicts = CountConflicts(state);
        double bestPct = CalcPct(state);
        Dictionary<Project, int> bestShifts = new Dictionary<Project, int>();
        foreach (Project p in projects)
        {
            bestShifts[p] = state.GetShift(p);
        }

        void CaptureBest()
        {
            foreach (Project p in projects)
            {
                bestShifts[p] = state.GetShift(p);
            }
        }

        void applyBest()
        {
            foreach (Project p in projects)
            {
                state.ApplyShift(p, bestShifts[p]);
            }
        }

        void Search(int index)
        {
            if (index == ordered.Count)
            {
                int conflicts = CountConflicts(state);
                double pct = CalcPct(state);
                if (conflicts < bestConflicts || (conflicts == bestConflicts && pct > bestPct))
                {
                    bestConflicts = conflicts;
                    bestPct = pct;
                    CaptureBest();
                }
                return;
            }
            Project proj = ordered[index];
            int original = state.GetShift(proj);
        }



        // // reference score
        // var scorer = new GreedyAlg();


        // // foreach (var sort in ProjectsSortedByConflict) 
        // //     Console.WriteLine(sort.Key.name + " " + sort.Value);
        // // }




        // //where the alg actually start
        // //ShiftScore baseline = GreedyAlg.EvaluateShift(state, project, currentShift); 

        // int startTotal = state.PersonWeekGrid.Values.Sum();
        // int startNonConflict = state.PersonWeekGrid.Where(kv => kv.Value == 1).Sum(kv => kv.Value);
        // int startDouble = state.PersonWeekGrid.Count(kv => kv.Value >= 2);
        // double startPct;
        // if (startTotal == 0)
        // {
        //     startPct = 100.0;
        // }
        // else
        // {
        //     startPct = (double)startNonConflict / startTotal * 100.0;
        // }

        // Console.WriteLine("Next algorithm running: ");

        // Console.WriteLine("Start total: " + startTotal + ", double-booked=" + startDouble + ", % not double-booked=" + startPct.ToString("0.##"));


        // for (int pass = 1; pass <= maxPasses; pass++)
        // {

        // var ProjectsWithConflict = new Dictionary<Project, int>();
        // foreach (KeyValuePair<ScheduleState.WeekKey, int> entry in state.PersonWeekGrid)
        // {
        //     foreach (Project p in projects)
        //     {
        //         foreach (var person in p.people)
        //         {
        //             if (person.id == entry.Key.PersonId)
        //             {
        //                 if (ProjectsWithConflict.ContainsKey(p))
        //                 {
        //                     ProjectsWithConflict[p]++;
        //                 }
        //                 else
        //                 {
        //                     ProjectsWithConflict[p] = 1;
        //                 }
        //             }
        //         }
        //     }

        // }

        // var ProjectsSortedByConflict = ProjectsWithConflict.OrderByDescending(pair => pair.Value);


        // // shift monitor
        // bool shifted = false;

        // foreach (var project in ProjectsSortedByConflict)
        //     {
        //         //get and set shift should be recyclable
        //         // get shift in state for comparison
        //         int originalShift = state.GetShift(project.Key);
        //         // make current the best, used to compare later
        //         int bestShift = originalShift;

        //         // get conflicts count for comparison
        //         int originalConflicts = CountConflicts(state);
        //         int bestConflicts = originalConflicts;



        //         // get current shift in the schedule state (from greedy)
        //         int currentShift = state.GetShift(project.Key);


        //         //Evaluate shift does the scoring of overlaps. 

        //         //ShiftScore bestScore = scorer.EvaluateShift(state, project.Key, currentShift);

        //         //getvalidshifts in schedulestate, can't call because we will have +/- 3 logic but can copy and paste into this class and refactor

        //         var potentialShifts = getSetShifts(state, project.Key, maxShift);
        //         //Get grid handles some of the removing and placing logic
        //         //build greedy schedule lines 46-73 has the bulk of the actual greedy logic so it can be repurposed and moved here
        //         //apply shift actually sets the shift when we confirm the best move
        //         //
        //         foreach (var potentialshift in potentialShifts)
        //         {
        //             // sanity check (not needed really) if shift is already optimal shift, skip it
        //             if (potentialshift == originalShift)
        //             {
        //                 continue;
        //             }

        //             //need to get compare if doing the shift is better than current shift
        //             // probably need to get current greedy shift score everytime, to compare

        //             //ShiftScore testScore = scorer.EvaluateShift(state, project.Key, potentialshift);

        //             // get best shift distance to compare to proposed shift
        //             //int bestDistance = Math.Abs(bestShift - currentShift);
        //             //int proposedDistance = Math.Abs(potentialshift - currentShift);
        //             // compare scores to see if a new best 
        //             // prioritise whats better???? conflixct???
        //             //bool isBetter = false;

        //             //if (proposedDistance < bestDistance)

        //             // I think we need to also factor in other factors like double-booked/overlap which I think will be easy
        //             // as we have the variables alreayt in the Shiftscore data type i.e DoubleBooked variable
        //             // RESOLVED - COPIED SCORING FROM GREEDY

        //             /*if (testScore.DeltaDoubleBooked < bestScore.DeltaDoubleBooked ||
        //             (testScore.DeltaDoubleBooked == bestScore.DeltaDoubleBooked && testScore.OverlapAfter < bestScore.OverlapAfter) ||
        //             (testScore.DeltaDoubleBooked == bestScore.DeltaDoubleBooked && testScore.OverlapAfter == bestScore.OverlapAfter && proposedDistance < bestDistance))
        //             {
        //                 isBetter = true;
        //             }*/

        //             state.ApplyShift(project.Key, potentialshift);
        //             int conflicts = CountConflicts(state);

        //             if (conflicts < bestConflicts)
        //             {
        //                 bestConflicts = conflicts;
        //                 bestShift = potentialshift;
        //             }

        //             // go back to original state so to compare against original
        //             state.ApplyShift(project.Key, originalShift);


        //             // test print to see potential shifts (making sure not showing only 1 shift option)

        //             //int availableShiftCount = getSetShifts(state, project.Key, maxShift).Count;
        //             //Console.WriteLine("Project: " + project.Key.name + " Availble shifts: " + availableShiftCount);
        //         }
        //         if (bestShift != originalShift && bestConflicts < originalConflicts)
        //         {
        //             state.ApplyShift(project.Key, bestShift);
        //             shifted = true;
        //         }
        //     }
        //     if (shifted == false)
        //     {
        //         break;
        //     }
        // }
        return state;

    }

    private List<int> getSetShifts(ScheduleState state, Project project, int maxShift)
    {
        int currentShift = state.GetShift(project);
        List<int> shifts = new List<int>();
        foreach (var shift in state.GetValidShifts(project))
        {
            // check shifts are valid in either direction (-/+)
            if (Math.Abs(shift - currentShift) <= maxShift)
            {
                // add if within maxShift 
                shifts.Add(shift);
            }
        }
        return shifts;
    }
    private double CalcPct(ScheduleState state)
    {
        int total = 0;
        int nonConflict = 0;
        foreach (int value in state.PersonWeekGrid.Values)
        {
            total += value;
            if (value == 1)
            {
                nonConflict += 1;
            }
        }
        if (total == 0)
        {
            return 100.0;
        }
        return (double)nonConflict / total * 100.0;
    }


    public List<Project> GetOrderedProjects(List<Project> projects, ScheduleState state)
    {
        List<Project> ordered = new List<Project>(projects);
        for (int i = 0; i < ordered.Count - 1; i++)
        {
            int bestIndex = i;
            for (int j = i + 1; j < ordered.Count; j++)
            {
                if (CompareProjects(ordered[j], ordered[bestIndex], state) < 0)
                {
                    bestIndex = j;
                }
            }
            if (bestIndex != i)
            {
                Project temp = ordered[i];
                ordered[i] = ordered[bestIndex];
                ordered[bestIndex] = temp;
            }
        }
        return ordered;
    }

    private int CompareProjects(Project a, Project b, ScheduleState state)
    {
        int durationA = state.GetDuration(a);
        int durationB = state.GetDuration(b);
        if (durationA != durationB)
        {
            return durationB.CompareTo(durationA); // longer first
        }

        int peopleA = a.people.Count;
        int peopleB = b.people.Count;
        if (peopleA != peopleB)
        {
            return peopleB.CompareTo(peopleA); // more people first
        }

        return a.id.CompareTo(b.id); // stable tie-breaker
    }

    public int CountConflicts(ScheduleState state)
    {
        int conflicts = 0;
        foreach (var personCount in state.PersonWeekGrid)
        {
            if (personCount.Value >= 2)
                conflicts += 1;
        }
        return conflicts;
    }
}
