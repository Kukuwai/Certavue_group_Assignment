using System;
using System.Collections.Generic;
using System.Linq;

public class RoleOptimizer
{
<<<<<<< HEAD
<<<<<<< ours
=======
>>>>>>> 4e8a2bcd14597516a9c58936fc33663b0efee747
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
<<<<<<< HEAD
=======
    public OptimizationResult Optimize(ScheduleState state, int maxPasses = 5000)
    {
        var handler = new ScheduleHandler(state);
        double currentFitness = handler.CalculateFitnessScore(state);

        bool improvedAny = false;
        int movesApplied = 0;
        int combinationsChecked = 0;

        for (int pass = 0; pass < maxPasses; pass++)
        {
            MoveCandidate bestMove = FindBestMove(state, handler, currentFitness, out int checkedThisPass);
            combinationsChecked += checkedThisPass;

            if (bestMove == null || bestMove.FitnessDelta <= 0)
            {
                break;
            }

            bool applied = GreedyAlg.MoveHoursToReplacement(
                state,
                bestMove.Project,
                bestMove.OverloadedPerson,
                bestMove.ReplacementPerson,
                bestMove.RawWeek,
                bestMove.ShiftedWeek,
                bestMove.HoursToMove);

            if (!applied)
            {
                break;
            }

            improvedAny = true;
            movesApplied++;
            currentFitness = handler.CalculateFitnessScore(state);
        }

        return new OptimizationResult
>>>>>>> theirs
=======
>>>>>>> 4e8a2bcd14597516a9c58936fc33663b0efee747
        {
            BestState = state,
            Improved = improvedAny,
            FinalFitness = currentFitness,
<<<<<<< HEAD
<<<<<<< ours
=======
>>>>>>> 4e8a2bcd14597516a9c58936fc33663b0efee747
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
<<<<<<< HEAD
=======
            WeeksImproved = movesApplied,
            CombinationsChecked = combinationsChecked
        };
    }

    private MoveCandidate FindBestMove(ScheduleState state, ScheduleHandler handler, double baselineFitness, out int combinationsChecked)
    {
        combinationsChecked = 0;
        MoveCandidate best = null;

        List<OverloadCell> overloadCells = BuildOverloadCells(state);

        foreach (OverloadCell overload in overloadCells)
        {
            Person overloadedPerson = FindPersonById(state, overload.PersonId);
            if (overloadedPerson == null)
=======
>>>>>>> 4e8a2bcd14597516a9c58936fc33663b0efee747
            {
                continue;
            }

<<<<<<< HEAD
            List<SourceAssignment> sourceAssignments = BuildSourceAssignments(state, overloadedPerson, overload.Week);
            if (sourceAssignments.Count == 0)
            {
                continue;
            }

            List<Person> replacementCandidates = GetReplacementCandidates(state, overloadedPerson, overload.Week);
            if (replacementCandidates.Count == 0)
            {
                continue;
            }

            foreach (SourceAssignment source in sourceAssignments)
            {
                foreach (Person replacement in replacementCandidates)
                {
                    for (int hoursToMove = 10; hoursToMove <= source.SourceHours; hoursToMove += 5)
                    {
                        combinationsChecked++;
                        AssignmentSnapshot snapshot = CaptureSnapshot(state);

                        bool moved = GreedyAlg.MoveHoursToReplacement(
                            state,
                            source.Project,
                            overloadedPerson,
                            replacement,
                            source.RawWeek,
                            overload.Week,
                            hoursToMove);

                        if (moved)
                        {
                            double afterFitness = handler.CalculateFitnessScore(state);
                            double delta = afterFitness - baselineFitness;

                            if (delta > 0)
                            {
                                int overloadAfter = GetOverloadAmount(state, overload.PersonId, overload.Week);
                                int overloadReduction = overload.OverloadAmount - overloadAfter;

                                var candidate = new MoveCandidate
                                {
                                    Project = source.Project,
                                    OverloadedPerson = overloadedPerson,
                                    ReplacementPerson = replacement,
                                    RawWeek = source.RawWeek,
                                    ShiftedWeek = overload.Week,
                                    HoursToMove = hoursToMove,
                                    FitnessDelta = delta,
                                    OverloadReduction = overloadReduction
                                };

                                if (IsBetterCandidate(candidate, best))
                                {
                                    best = candidate;
                                }
                            }
                        }

                        RestoreSnapshot(state, snapshot);
                    }
                }
            }
        }

        return best;
    }

    private List<OverloadCell> BuildOverloadCells(ScheduleState state)
    {
        var cells = new List<OverloadCell>();

        foreach (KeyValuePair<ScheduleState.PersonWeekKey, int> kv in state.PersonWeekHours)
        {
            Person person = FindPersonById(state, kv.Key.PersonId);
            if (person == null)
            {
                continue;
            }

            int capacity = person.capacity > 0 ? person.capacity : 40;
            int overloadAmount = kv.Value - capacity;

            if (overloadAmount <= 0)
            {
                continue;
            }

            cells.Add(new OverloadCell
            {
                PersonId = kv.Key.PersonId,
                Week = kv.Key.Week,
                AssignedHours = kv.Value,
                Capacity = capacity,
                OverloadAmount = overloadAmount
            });
        }

        cells.Sort((left, right) => right.OverloadAmount.CompareTo(left.OverloadAmount));
        return cells;
    }

    private List<SourceAssignment> BuildSourceAssignments(ScheduleState state, Person overloadedPerson, int shiftedWeek)
    {
        var sources = new List<SourceAssignment>();

        foreach (KeyValuePair<Project, Dictionary<int, int>> assignment in overloadedPerson.projects)
        {
            Project project = assignment.Key;
            int rawWeek = shiftedWeek - state.GetShift(project);

            if (!assignment.Value.TryGetValue(rawWeek, out int sourceHours))
            {
                continue;
            }

            if (sourceHours < 10)
            {
                continue;
            }

            sources.Add(new SourceAssignment
            {
                Project = project,
                RawWeek = rawWeek,
                SourceHours = sourceHours
            });
        }

        return sources;
    }

    private List<Person> GetReplacementCandidates(ScheduleState state, Person overloadedPerson, int shiftedWeek)
    {
        var candidates = new List<Person>();

        foreach (Person person in state.People)
        {
            if (person.id == overloadedPerson.id)
>>>>>>> theirs
            {
                continue;
            }

<<<<<<< ours
            if (!string.Equals(person.role, task.OverloadedPerson.role, StringComparison.OrdinalIgnoreCase)) //verifies same role
=======
            if (!string.Equals(person.role, overloadedPerson.role, StringComparison.Ordinal))
>>>>>>> theirs
=======
            if (!string.Equals(person.role, task.OverloadedPerson.role, StringComparison.OrdinalIgnoreCase)) //verifies same role
>>>>>>> 4e8a2bcd14597516a9c58936fc33663b0efee747
            {
                continue;
            }

<<<<<<< HEAD
<<<<<<< ours
=======
>>>>>>> 4e8a2bcd14597516a9c58936fc33663b0efee747
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
<<<<<<< HEAD
=======
            int availableCapacity = GetRemainingCapacity(state, person, shiftedWeek);
            if (availableCapacity < 10)
            {
                continue;
            }

            candidates.Add(person);
        }

        return candidates;
    }

    private static bool IsBetterCandidate(MoveCandidate candidate, MoveCandidate best)
    {
        if (best == null)
        {
            return true;
        }

        if (candidate.OverloadReduction > best.OverloadReduction)
        {
            return true;
        }

        if (candidate.OverloadReduction < best.OverloadReduction)
        {
            return false;
        }

        if (candidate.FitnessDelta > best.FitnessDelta)
        {
            return true;
        }

        if (candidate.FitnessDelta < best.FitnessDelta)
        {
            return false;
        }

        return candidate.HoursToMove > best.HoursToMove;
    }

    private int GetRemainingCapacity(ScheduleState state, Person person, int week)
    {
        int currentHours = 0;
        state.PersonWeekHours.TryGetValue(new ScheduleState.PersonWeekKey(person.id, week), out currentHours);

        int capacity = person.capacity > 0 ? person.capacity : 40;
        return capacity - currentHours;
    }

    private int GetOverloadAmount(ScheduleState state, int personId, int week)
    {
        Person person = FindPersonById(state, personId);
        if (person == null)
        {
            return 0;
        }

        int assignedHours = 0;
        state.PersonWeekHours.TryGetValue(new ScheduleState.PersonWeekKey(personId, week), out assignedHours);

        int capacity = person.capacity > 0 ? person.capacity : 40;
        return Math.Max(0, assignedHours - capacity);
    }

    private Person FindPersonById(ScheduleState state, int personId)
    {
        foreach (Person person in state.People)
        {
            if (person.id == personId)
            {
                return person;
            }
        }

        return null;
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
    private class OverloadCell
    {
        public int PersonId { get; set; }
        public int Week { get; set; }
        public int AssignedHours { get; set; }
        public int Capacity { get; set; }
        public int OverloadAmount { get; set; }
    }

    private class SourceAssignment
    {
        public Project Project { get; set; }
        public int RawWeek { get; set; }
        public int SourceHours { get; set; }
    }

    private class MoveCandidate
    {
        public Project Project { get; set; }
        public Person OverloadedPerson { get; set; }
        public Person ReplacementPerson { get; set; }
        public int RawWeek { get; set; }
        public int ShiftedWeek { get; set; }
        public int HoursToMove { get; set; }
        public double FitnessDelta { get; set; }
        public int OverloadReduction { get; set; }
>>>>>>> theirs
=======
>>>>>>> 4e8a2bcd14597516a9c58936fc33663b0efee747
    }

    private class AssignmentSnapshot //copies of states to compare
    {
        public Dictionary<Person, Dictionary<Project, Dictionary<int, int>>> PersonAssignments { get; set; }
        public Dictionary<Project, HashSet<Person>> ProjectPeople { get; set; }
<<<<<<< HEAD
<<<<<<< ours
=======
>>>>>>> 4e8a2bcd14597516a9c58936fc33663b0efee747
    }

    public class OptimizationResult //returned best result
    {
        public ScheduleState BestState { get; set; }
        public bool Improved { get; set; }
        public double FinalFitness { get; set; }
        public int WeeksImproved { get; set; }
        public int CombinationsChecked { get; set; }
    }
<<<<<<< HEAD
}
=======
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
>>>>>>> theirs
=======
}
>>>>>>> 4e8a2bcd14597516a9c58936fc33663b0efee747
