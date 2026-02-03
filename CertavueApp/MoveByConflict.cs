using System.Data;
//using static ScheduleState;

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


    
}