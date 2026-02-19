using GeneticAlgorithm.Components.CrossoverManagers;

public class NewProjectHandler
{
    public void addNewProject(ScheduleState state, int startWeek, int endWeek, Dictionary<string,int> roleRequirements)
    {
        // standard checks for invalid data
        if (startWeek < 1 || startWeek >= endWeek || endWeek > 52 || state.Equals(null) || roleRequirements.Keys.All(key => String.IsNullOrWhiteSpace(key)))
        {
            return;
        }

        var hoursNeededforProject = 0;
        foreach (var kp in roleRequirements)
        {
            hoursNeededforProject += kp.Value;
        }
        state.Projects.Add(new Project("new projected", startWeek, endWeek, hoursNeededforProject)); 
        
        // create new dictionary of available people, and the weeks;
        Dictionary<string, Person> potentialPeopleforRole = new();
        
        foreach (Person p in state.People)
        {
            // check if person has any of the required roles
            if (!potentialPeopleforRole.ContainsKey(p.role))
            {
               continue; 
            }
            for (int i = startWeek; i <= endWeek; i++)
            {
                var roleHours = roleRequirements.GetValueOrDefault(p.role, 0);
                if (roleHours != 0)
                {
                    if (CanTakeHours(state, p, startWeek, endWeek, roleHours))
                    {
                        Console.WriteLine("Added");
                    }
                }
            }
        }
    }

    private static bool CanTakeHours(ScheduleState state, Person person, int startWeek, int endWeek, int totalHoursRequired) //Weekly cap target
    {
        // start at 0 hour count
        int current = 0; 
        int duration = endWeek - startWeek + 1;
        for (int i = startWeek; i <= endWeek; i++)
        {
            ScheduleState.PersonWeekKey key = new ScheduleState.PersonWeekKey(person.id, i);
            state.PersonWeekHours.TryGetValue(key, out current); //Get current week total

            int capacity = person.capacity; //Person's capacity
            if (capacity <= 0)
            {
                capacity = 40; //default to 40
            }
            current += current; //Hours stay within limit 
        }
        if (current <= totalHoursRequired)
        {
            return true;
        }
        return false;
    }
}