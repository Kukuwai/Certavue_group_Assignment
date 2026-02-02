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
                shifts.Add(current);        //Stays place ie 0 shifts
            }
            canidates[p] = shifts;          //list of possible shifts
        }

        List<Project> ordered = GetOrderedProjects(projects, state); //saves the current shifts as the best

        int bestConflicts = CountConflicts(state);
        double bestPct = CalcPct(state);
        Dictionary<Project, int> bestShifts = new Dictionary<Project, int>(); //holds projects best shifts
        foreach (Project p in projects)
        {
            bestShifts[p] = state.GetShift(p);
        }

        void CaptureBest()   //takes the best shifts and applies them to the state
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

        void Search(int index)  //projects shift combinations
        {
            if (index == ordered.Count)
            {
                int conflicts = CountConflicts(state);  //evals current conflicts
                double pct = CalcPct(state);
                if (conflicts < bestConflicts || (conflicts == bestConflicts && pct > bestPct)) //either fewer conflicts or some conflicts but better % update best metrics
                {
                    bestConflicts = conflicts;
                    bestPct = pct;
                    CaptureBest();
                }
                return;
            }
            Project proj = ordered[index];
            int original = state.GetShift(proj);

            foreach (int shift in canidates[proj])
            {
                state.ApplyShift(proj, shift);
                int conflicts = CountConflicts(state);
                if (conflicts <= bestConflicts)
                {
                    Search(index + 1);
                }
                state.ApplyShift(proj, original);
            }

        }
        Search(0);
        applyBest();
        return state;
    }

    private List<int> getSetShifts(ScheduleState state, Project project, int maxShift)
    {
        int currentShift = state.GetShift(project);
        List<int> shifts = new List<int>();
        foreach (var shift in state.GetValidShifts(project))
        {
            //check shifts are valid -/+
            if (Math.Abs(shift - currentShift) <= maxShift)
            {
                //add if within maxShift 
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
            return durationB.CompareTo(durationA); 
        }

        int peopleA = a.people.Count;
        int peopleB = b.people.Count;
        if (peopleA != peopleB)
        {
            return peopleB.CompareTo(peopleA); 
        }

        return a.id.CompareTo(b.id); 
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
