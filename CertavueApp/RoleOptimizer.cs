using System;
using System.Collections.Generic;
using System.Linq;

public class RoleOptimizer
{
    public OptimizationResult Optimize(ScheduleState state, int maxPasses = 99999999) //I set this ridicuously high to test speed first. I think this shouldn't have speed concerns but good to be safe?0
    {
        var handler = new ScheduleHandler(state); //will be used to measure fitness of schedules
        double currentFitness = handler.CalculateFitnessScore(state); //initial score and then best as of now score

        bool improvedAny = false;
        int weeksImproved = 0;
        int combinationsChecked = 0; //counter for speed checking

        for (int pass = 0; pass < maxPasses; pass++)
        {
            bool improvedThisPass = false; //resets each time to recheck
            List<int> conflictWeeks = state.PersonWeekGrid.Where(kv => kv.Value > 1).Select(kv => kv.Key.Week).Distinct().OrderBy(w => w).ToList(); //builds list of overbooked weeks
            foreach (int week in conflictWeeks) //goes through each week individually
            {
                WeekOptimizationResult weekResult = OptimizeWeek(state, week, currentFitness, handler); //optimizes week and returns stas
                combinationsChecked += weekResult.CombinationsChecked; //counts combos checked

                if (weekResult.Improved) //updates global best if it improved 
                {
                    improvedAny = true;
                    improvedThisPass = true;
                    weeksImproved++;
                    currentFitness = weekResult.BestFitness;
                }
            }

            if (!improvedThisPass) //performance piece to stop loop if no improvement found 
            {
                break;
            }
        }
        return new OptimizationResult   //can be used to print on output @Millar
        {
            BestState = state,
            Improved = improvedAny,
            FinalFitness = currentFitness,
            WeeksImproved = weeksImproved,
            CombinationsChecked = combinationsChecked
        };

    }

    
    private WeekOptimizationResult OptimizeWeek(ScheduleState state, int week, double baselineFitness, ScheduleHandler handler) //method checks one week at a time and optimizes it
    {
        List<ConflictTask> tasks = BuildConflictTasks(state, week); //each task is one overloaded person on one project in that week
        if (tasks.Count == 0)
        {
            return new WeekOptimizationResult
            {
                Improved = false,
                BestFitness = baselineFitness,
                CombinationsChecked = 0
            };
        }
        AssignmentSnapshot baseline = CaptureSnapshot(state);  //saves state before changes are made
        AssignmentSnapshot bestSnapshot = null; //will be stored with best state as changes happen
        double bestFitness = baselineFitness; //best fitness score
        int combinationsChecked = 0; //counter for how many checks have been done
        void Search(int taskIndex) //recursive search for each tasks and checks to keep current assignment or replace staff
        {
            if (taskIndex == tasks.Count)
            {
                combinationsChecked++;
                double score = handler.CalculateFitnessScore(state); //score this new state

                if (score > bestFitness)
                {
                    bestFitness = score;
                    bestSnapshot = CaptureSnapshot(state); //better and now "best" state
                }

                return;
            }
            Search(taskIndex + 1); //does not change task
            ConflictTask task = tasks[taskIndex]; //attempts to replace task
            List<Person> candidates = GetReplacementCandidates(state, task); //filters canidates by same role and availabiliy
            foreach (Person replacement in candidates) //tries each available dcanidate one by one
            {
                AssignmentSnapshot beforeMove = CaptureSnapshot(state); //saves state before a change is made
                if (TryMove(state, task, replacement)) //attempts to make the move
                {
                    Search(taskIndex + 1);
                }
                RestoreSnapshot(state, beforeMove); //restores so canidate is back at same point
            }
        }
        Search(0); //starts recursion from first task
        RestoreSnapshot(state, baseline); //restores original
        if (bestSnapshot != null) //better schedule is found so apply it
        {
            RestoreSnapshot(state, bestSnapshot);
            return new WeekOptimizationResult
            {
                Improved = true,
                BestFitness = bestFitness,
                CombinationsChecked = combinationsChecked
            };
        }
        return new WeekOptimizationResult //if not found then return what we had before
        {
            Improved = false,
            BestFitness = baselineFitness,
            CombinationsChecked = combinationsChecked
        };
    }
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
                if (overloaded.projects.TryGetValue(project, out Dictionary<int, int> weeks) && weeks.Keys.Contains(rawWeek)) //checks if project is being worked on that week
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
            if (person.id == task.OverloadedPerson.id) //avoids replacing self
            {
                continue;
            }

            if (!string.Equals(person.role, task.OverloadedPerson.role, StringComparison.OrdinalIgnoreCase)) //verifies same role
            {
                continue;
            }

            if (!GreedyAlg.IsPersonFree(state, person, task.Project, task.Week)) //makes sure they are free this week
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
        if (!task.OverloadedPerson.projects.TryGetValue(task.Project, out Dictionary<int, int> weeks)) //Makes sure the original person owns the project still, ie was moved away prior
        {
            return false;
        }
        if (!weeks.Keys.Contains(rawWeek)) //has to be the exact same week working
        {
            return false;
        }
        if (!GreedyAlg.IsPersonFree(state, replacement, task.Project, task.Week)) //replacement must still be free, ie not added another project
        {
            return false;
        }
        GreedyAlg.MoveWeekToReplacement(state, task.Project, task.OverloadedPerson, replacement, rawWeek); //does the actual moving
        return replacement.projects.TryGetValue(task.Project, out Dictionary<int, int> replacementWeeks) && replacementWeeks.Keys.Contains(rawWeek);
    }
    private AssignmentSnapshot CaptureSnapshot(ScheduleState state)  //keeps a snapshot of changes for comparison ak grid v grid
    {
        var personAssignments = new Dictionary<Person, Dictionary<Project, Dictionary<int, int>>>(); //Copy person/project/weeks map
        foreach (Person person in state.People)
        {
            var byProject = new Dictionary<Project, Dictionary<int, int>>(person.projects);
            foreach (KeyValuePair<Project, Dictionary<int,int>> kv in person.projects) //takes list of people on project
            {
                byProject[kv.Key] = new Dictionary<int, int>(kv.Value); //copies s future changes don't impact it
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
            if (!snapshot.PersonAssignments.TryGetValue(person, out Dictionary<Project, Dictionary<int, int>> byProject)) //skip anyone without snapshot data
            {
                continue;
            }
            foreach (KeyValuePair<Project, Dictionary<int, int>> kv in byProject) //rebuilds from snapshot
            {
                person.projects[kv.Key] = new Dictionary<int, int>(kv.Value);
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
        public Dictionary<Person, Dictionary<Project, Dictionary<int, int>>> PersonAssignments { get; set; }
        public Dictionary<Project, HashSet<Person>> ProjectPeople { get; set; }
    }

    public class OptimizationResult //returned best result
    {
        public ScheduleState BestState { get; set; }
        public bool Improved { get; set; }
        public double FinalFitness { get; set; }
        public int WeeksImproved { get; set; }
        public int CombinationsChecked { get; set; }
    }
}