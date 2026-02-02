using System.Data;
//using static ScheduleState;
using static Program;
using static Person;

public class MoveByConflict
{


    public MoveByConflict(ScheduleState state, List<Project> projects)
    {
        var ProjectsWithConflict = new List<Project>();
        foreach (var (key, count) in state.PersonWeekGrid)
        {
            if (count > 1)
            {
                foreach (Project p in projects)
                {
                    foreach (var person in p.people)
                    {
                        if (person.id == key.PersonId && !ProjectsWithConflict.Contains(p))
                        {
                            ProjectsWithConflict.Add(p);
                            break;
                        }
                    }
                }
                foreach (var c in ProjectsWithConflict)
                {
                    Console.WriteLine(c.id);
                }
            }

        }
    }
}