using System.Data;
//using static ScheduleState;
using static Program;
using static Person;
using static GreedyAlg;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.CompilerServices;

public class MoveByConflict
{


    public MoveByConflict(ScheduleState state, List<Project> projects)
    {
        // reference score
        var scorer = new GreedyAlg();

        var ProjectsWithConflict = new Dictionary<Project, int>();
        foreach (KeyValuePair<ScheduleState.WeekKey, int> entry in state.PersonWeekGrid)
        {
            foreach (Project p in projects)
            {
                foreach (var person in p.people)
                {
                    if (person.id == entry.Key.PersonId)
                    {
                        if (ProjectsWithConflict.ContainsKey(p))
                        {
                            ProjectsWithConflict[p]++;
                        }
                        else
                        {
                            ProjectsWithConflict[p] = 1;
                        }
                    }
                }
            }

        }

        var ProjectsSortedByConflict = ProjectsWithConflict.OrderByDescending(pair => pair.Value);

        
        // foreach (var sort in ProjectsSortedByConflict) 
        //     Console.WriteLine(sort.Key.name + " " + sort.Value);
        // }




        //where the alg actually start
        //ShiftScore baseline = GreedyAlg.EvaluateShift(state, project, currentShift); 

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

        Console.WriteLine("Next algorithm running: ");

        Console.WriteLine("Start total: " + startTotal + ", double-booked=" + startDouble + ", % not double-booked=" + startPct.ToString("0.##"));

        bool shifted = false;

        foreach (var project in ProjectsSortedByConflict)
        {
            //some helpful methods
            
            //get and set shift should be recyclable

            // get current shift in the schedule state (from greedy)
            int currentShift = state.GetShift(project.Key);
            int bestShift = currentShift;

            //Evaluate shift does the scoring of overlaps. 

            ShiftScore best = scorer.EvaluateShift(state, project.Key, currentShift);

            //getvalidshifts in schedulestate, can't call because we will have +/- 3 logic but can copy and paste into this class and refactor

            var potentialShifts = getSetShifts(state....project, 3)
            //Get grid handles some of the removing and placing logic
            //build greedy schedule lines 46-73 has the bulk of the actual greedy logic so it can be repurposed and moved here
            //apply shift actually sets the shift when we confirm the best move
            //
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

    }
}