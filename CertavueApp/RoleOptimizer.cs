using System;
using System.Collections.Generic;
using System.Linq;

public class RoleOptimizer
{

    private List<ConflictTask> BuildConflictTasks(ScheduleState state, int week)
    {
        List<int> overloadedIds = state.PersonWeekGrid.Where(kv => kv.Key.Week == week && kv.Value > 1).Select(kv => kv.Key.PersonId).Distinct().ToList(); //list of person IDs that are over 40 hours for the week
        var tasks = new List<ConflictTask>(); //will be the output (maybe)
        foreach (int personId in overloadedIds) //finds which projects the overloaded persons are on 
        {
            Person overloaded = null;
            foreach (var p in state.People)
            {
                if (p.id == personId)
                {
                    overloaded = p;
                    break;
                }
            }
            if (overloaded == null)
            {
                continue;
            }
            foreach (Project project in state.Projects)
            {
                int rawWeek = week - state.GetShift(project); //converts because the displayed week is not the actual week 
                if (overloaded.projects.TryGetValue(project, out List<int> weeks) && weeks.Contains(rawWeek)) //checks if project is being worked on that week
                {
                    tasks.Add(new ConflictTask //if it is then adding it to the conflict tasks list
                    {
                        Week = week,
                        OverloadedPerson = overloaded,
                        Project = project
                    });
                }
            }
        }
        return tasks; //returns all tasks aka week/person that are overloaded
    }
    private List<Person> GetReplacementCandidates(ScheduleState state, ConflictTask task)
    {
        var candidates = new List<Person>();
        foreach (Person person in state.People)
        {
            if (person.id == task.OverloadedPerson.id) //avoids dups
            {
                continue;
            }

            if (!string.Equals(person.role, task.OverloadedPerson.role, StringComparison.OrdinalIgnoreCase)) //verifies same role
            {
                continue;
            }

            if (!GreedyAlg.IsPersonFree(state, person, task.Week)) //makes sure they are free this week
            {
                continue;
            }
            candidates.Add(person);  //passes all 3 then added to the canidates to replace list
        }
        return candidates;
    }
    private bool TryMove(ScheduleState state, ConflictTask task, Person replacement)
    {
        int rawWeek = task.Week - state.GetShift(task.Project); //apparently the displayed week after the move isn't the same as what is truly stored which was interesting
        if (!task.OverloadedPerson.projects.TryGetValue(task.Project, out List<int> weeks)) //Makes sure the original person owns the project still, ie was moved away prior
        {
            return false;
        }
        if (!weeks.Contains(rawWeek)) //has to be the exact same week working
        {
            return false;
        }
        if (!GreedyAlg.IsPersonFree(state, replacement, task.Week)) //replacement must still be free, ie not added another project
        {
            return false;
        }
        GreedyAlg.MoveWeekToReplacement(state, task.Project, task.OverloadedPerson, replacement, rawWeek); //does the actual moving
        return replacement.projects.TryGetValue(task.Project, out List<int> replacementWeeks) && replacementWeeks.Contains(rawWeek);
    }
    private AssignmentSnapshot CaptureSnapshot(ScheduleState state)  //keeps a snapshot of changes for comparison ak grid v grid
    {
        var personAssignments = new Dictionary<Person, Dictionary<Project, List<int>>>(); //Copy person/project/weeks map
        foreach (Person person in state.People)
        {
            var byProject = new Dictionary<Project, List<int>>();
            foreach (KeyValuePair<Project, List<int>> kv in person.projects) //takes list of people on project
            {
                byProject[kv.Key] = new List<int>(kv.Value); //copies s future changes don't impact it
            }
            personAssignments[person] = byProject; //saves this version 
        }
        var projectPeople = new Dictionary<Project, HashSet<Person>>(); //stores seperately because of overriding 
        foreach (Project project in state.Projects) //copies each project person set
        {
            projectPeople[project] = new HashSet<Person>(project.people); //avoids future changes
        }
        return new AssignmentSnapshot  // Return one snapshot object containing both views of the same schedule, person/project/weeks and project/people so we can build a full state
        {
            PersonAssignments = personAssignments,
            ProjectPeople = projectPeople
        };
    }
    private void RestoreSnapshot(ScheduleState state, AssignmentSnapshot snapshot) //takes an oild snapshot from storage 
    {
        foreach (Person person in state.People) //restores each person's projects/weeks
        {
            person.projects.Clear(); //removes current state
            if (!snapshot.PersonAssignments.TryGetValue(person, out Dictionary<Project, List<int>> byProject)) //skip anyone without snapshot data
            {
                continue;
            }
            foreach (KeyValuePair<Project, List<int>> kv in byProject) //rebuilds from snapshot
            {
                person.projects[kv.Key] = new List<int>(kv.Value);
            }
        }
        foreach (Project project in state.Projects) //restores each projects people set
        {
            project.people.Clear(); //clears old state
            if (!snapshot.ProjectPeople.TryGetValue(project, out HashSet<Person> people)) //skip any project not in snapshot
            {
                continue;
            }
            foreach (Person person in people) //adds back saved people to project
            {
                project.people.Add(person);
            }
        }

        state.RebuildGrid(); //rebuilds schedule state
    }
    private class ConflictTask //the cell in a grid/table that can be moved because conflicted
    {
        public int Week { get; set; }
        public Person OverloadedPerson { get; set; }
        public Project Project { get; set; }
    }

    private class WeekOptimizationResult //checks if it improved the fitness score or not
    {
        public bool Improved { get; set; }
        public double BestFitness { get; set; }
        public int CombinationsChecked { get; set; }
    }

    private class AssignmentSnapshot //copies of states to compare
    {
        public Dictionary<Person, Dictionary<Project, List<int>>> PersonAssignments { get; set; }
        public Dictionary<Project, HashSet<Person>> ProjectPeople { get; set; }
    }

    public class OptimizationResult //returned best result
    {
        public bool Improved { get; set; }
        public double FinalFitness { get; set; }
        public int WeeksImproved { get; set; }
        public int CombinationsChecked { get; set; }
    }
}