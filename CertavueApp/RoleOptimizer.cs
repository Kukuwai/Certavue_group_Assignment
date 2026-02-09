using System;
using System.Collections.Generic;
using System.Linq;

public class RoleOptimizer
{

    private List<ConflictTask> BuildConflictTasks(ScheduleState state, int week)
    {



        List<int> overloadedIds = state.PersonWeekGrid.Where(kv => kv.Key.Week == week && kv.Value > 1).Select(kv => kv.Key.PersonId).Distinct().ToList(); //list of person IDs that are over 40 hours
        var tasks = new List<ConflictTask>();
        foreach (int personId in overloadedIds)
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
                int rawWeek = week - state.GetShift(project);
                if (overloaded.projects.TryGetValue(project, out List<int> weeks) && weeks.Contains(rawWeek))
                {
                    tasks.Add(new ConflictTask
                    {
                        Week = week,
                        OverloadedPerson = overloaded,
                        Project = project
                    });
                }
            }
        }
        return tasks;
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

            if (!GreedyAlg.IsPersonFree(state, person, task.Week))
            {
                continue;
            }
            candidates.Add(person);
        }
        return candidates;
    }
    private bool TryMove(ScheduleState state, ConflictTask task, Person replacement)
    {
        int rawWeek = task.Week - state.GetShift(task.Project);
        if (!task.OverloadedPerson.projects.TryGetValue(task.Project, out List<int> weeks))
        {
            return false;
        }
        if (!weeks.Contains(rawWeek))
        {
            return false;
        }
        if (!GreedyAlg.IsPersonFree(state, replacement, task.Week))
        {
            return false;
        }
        GreedyAlg.MoveWeekToReplacement(state, task.Project, task.OverloadedPerson, replacement, rawWeek);
        return replacement.projects.TryGetValue(task.Project, out List<int> replacementWeeks) && replacementWeeks.Contains(rawWeek);
    }
    private AssignmentSnapshot CaptureSnapshot(ScheduleState state)
    {
        var personAssignments = new Dictionary<Person, Dictionary<Project, List<int>>>();
        foreach (Person person in state.People)
        {
            var byProject = new Dictionary<Project, List<int>>();
            foreach (KeyValuePair<Project, List<int>> kv in person.projects)
            {
                byProject[kv.Key] = new List<int>(kv.Value);
            }
            personAssignments[person] = byProject;
        }
        var projectPeople = new Dictionary<Project, HashSet<Person>>();
        foreach (Project project in state.Projects)
        {
            projectPeople[project] = new HashSet<Person>(project.people);
        }
        return new AssignmentSnapshot
        {
            PersonAssignments = personAssignments,
            ProjectPeople = projectPeople
        };
    }
    private void RestoreSnapshot(ScheduleState state, AssignmentSnapshot snapshot)
    {
        foreach (Person person in state.People)
        {
            person.projects.Clear();
            if (!snapshot.PersonAssignments.TryGetValue(person, out Dictionary<Project, List<int>> byProject))
            {
                continue;
            }
            foreach (KeyValuePair<Project, List<int>> kv in byProject)
            {
                person.projects[kv.Key] = new List<int>(kv.Value);
            }
        }
        foreach (Project project in state.Projects)
        {
            project.people.Clear();
            if (!snapshot.ProjectPeople.TryGetValue(project, out HashSet<Person> people))
            {
                continue;
            }

            foreach (Person person in people)
            {
                project.people.Add(person);
            }
        }

        state.RebuildGrid();
    }
    private class ConflictTask
    {
        public int Week { get; set; }
        public Person OverloadedPerson { get; set; }
        public Project Project { get; set; }
    }

    private class WeekOptimizationResult
    {
        public bool Improved { get; set; }
        public double BestFitness { get; set; }
        public int CombinationsChecked { get; set; }
    }

    private class AssignmentSnapshot
    {
        public Dictionary<Person, Dictionary<Project, List<int>>> PersonAssignments { get; set; }
        public Dictionary<Project, HashSet<Person>> ProjectPeople { get; set; }
    }

    public class OptimizationResult
    {
        public bool Improved { get; set; }
        public double FinalFitness { get; set; }
        public int WeeksImproved { get; set; }
        public int CombinationsChecked { get; set; }
    }


}