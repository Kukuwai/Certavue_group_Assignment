using System.Data;
//using static ScheduleState;
<<<<<<< HEAD
using static Program;
using static Person;
using static GreedyAlg;

public class MoveByConflict
{


    public MoveByConflict(ScheduleState state, List<Project> projects)
    {
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
                foreach (var c in ProjectsWithConflict)
                {
                    //Console.WriteLine(c.id);
                }
            }

        }
    }
=======

public class MoveByConflict
{
    public MoveByConflict(ScheduleState state)
    {
        bool run = true;
        while (run)
        {
            foreach (var week in state.PersonWeekGrid)
            {
                //Console.WriteLine(week.Key.PersonId + " " + week.Key.Week + " " + week.Value);
            }
        }
    }


    
>>>>>>> e504683 (add output and test function)
}