using GeneticAlgorithm.Components.CrossoverManagers;

public class NewProjectHandler
{
    public void addNewProject(ScheduleState state, int startWeek, int endWeek, Dictionary<string,int> roleRequirements)
    {
        // standard checks for invalid data
        if (startWeek < 1 || startWeek <= endWeek || endWeek > 52 || state == null || roleRequirements.Keys.All(null))
        {
            return;
        }

        var hoursNeededforProject = 0;
        foreach (var kp in roleRequirements)
        {
            hoursNeededforProject += kp.Value;
        }
        state.Projects.Add(new Project("new projected", startWeek, endWeek, hoursNeededforProject)); 
        
        foreach (Person p in state.People)
        {
            Dictionary<Person, int> weeksAvailable = new();
            foreach (var weekKey in state.PersonWeekGrid.Keys)
            {
                // check of person is in project else skip
                if (!weekKey.PersonId.Equals(p.id))
                {
                    continue;
                }
                // check compatible role
                foreach (var kp in roleRequirements)
                {
                    if (!p.role.Contains(kp.Key))
                    {
                       continue;
                    }
                }
                bool canWork = CanTakeHours(state, p, weekKey.Week, )
            }
        }
    }

    private static bool CanTakeHours(ScheduleState state, Person person, int week, int hoursToAdd) //Weekly cap target
    {
        int current = 0; //If nothing exists
        ScheduleState.PersonWeekKey key = new ScheduleState.PersonWeekKey(person.id, week);
        state.PersonWeekHours.TryGetValue(key, out current); //Get current week total

        int capacity = person.capacity; //Person's capacity
        if (capacity <= 0)
        {
            capacity = 40; //default to 40
        }

        return (current + hoursToAdd) <= capacity; //Hours stay within limit
    }
}