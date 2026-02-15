using System;
using System.Collections.Generic;
using System.Linq;

public class RoleOptimizer
{
    public OptimizationResult Optimize(ScheduleState state, int maxPasses = int.MaxValue) //If speed is a concern we can hard code it again
    {
        var handler = new ScheduleHandler(state); //Scoring check
        double initialFitness = handler.CalculateFitnessScore(state); //Baseline fitness
        double bestFitness = initialFitness; //Tracks highest
        int bestPathLength = 0; //Counts moves
        int combinationsChecked = 0;
        int statesExplored = 1;
        AssignmentSnapshot bestSnapshot = CaptureSnapshot(state); //First schedule is the initial best
        var exploredStates = new HashSet<string>(); //Every visited state
        string startHash = BuildStateHash(state);
        exploredStates.Add(startHash); //Marks state as visited
        var currentPath = new List<MoveCandidate>();

        ExploreAllSequences(state, handler, exploredStates, currentPath, ref bestFitness, ref bestSnapshot, ref bestPathLength, ref combinationsChecked, ref statesExplored, maxPasses); //Exhaustive search moving overloaded to underloaded

        RestoreSnapshot(state, bestSnapshot); //Best found state

        return new OptimizationResult //Summary of "best"
        {
            BestState = state,
            Improved = bestFitness > initialFitness,
            FinalFitness = bestFitness,
            WeeksImproved = bestPathLength,
            CombinationsChecked = combinationsChecked
        };
    }

    private void ExploreAllSequences(ScheduleState state, ScheduleHandler handler, HashSet<string> exploredStates, List<MoveCandidate> currentPath, ref double bestFitness, ref AssignmentSnapshot bestSnapshot, ref int bestPathLength, ref int combinationsChecked, ref int statesExplored, int maxStatesToExplore) //Exhasutive search method
    {
        if (statesExplored >= maxStatesToExplore) //Limiter
        {
            return;
        }

        List<MoveCandidate> legalMoves = EnumerateLegalMoves(state); //Generates every legal single move from the current state

        foreach (MoveCandidate move in legalMoves) //Tries every legal next move to build full sequence combinations
        {
            combinationsChecked++; //Counts this attempted move branch as one evaluated combination
            AssignmentSnapshot beforeMove = CaptureSnapshot(state); //Saves current state so can be reset

            bool applied = GreedyAlg.MoveHoursToReplacement(state, move.Project, move.OverloadedPerson, move.ReplacementPerson, move.RawWeek, move.ShiftedWeek, move.HoursToMove);

            if (!applied)
            {
                RestoreSnapshot(state, beforeMove);
                continue;
            }

            string nextHash = BuildStateHash(state); //newly reached state after applying this move
            if (exploredStates.Contains(nextHash)) //See if this state has been reviewed before
            {
                RestoreSnapshot(state, beforeMove); //Dup state
                continue;
            }

            exploredStates.Add(nextHash); //State is visited
            statesExplored++;
            currentPath.Add(move);

            double currentFitness = handler.CalculateFitnessScore(state); //Fitness score
            if (currentFitness > bestFitness) //Updates when fitness improves
            {
                bestFitness = currentFitness;
                bestSnapshot = CaptureSnapshot(state);
                bestPathLength = currentPath.Count;
            }

            if (statesExplored < maxStatesToExplore)
            {
                ExploreAllSequences(state, handler, exploredStates, currentPath, ref bestFitness, ref bestSnapshot, ref bestPathLength, ref combinationsChecked, ref statesExplored, maxStatesToExplore);
            }

            currentPath.RemoveAt(currentPath.Count - 1);
            RestoreSnapshot(state, beforeMove);
        }
    }
    private List<MoveCandidate> EnumerateLegalMoves(ScheduleState state) //Every possible move
    {
        var moves = new List<MoveCandidate>(); //All possible moves for state
        List<OverloadCell> overloadCells = BuildOverloadCells(state); //Over 40 person/weeks

        foreach (OverloadCell overload in overloadCells)
        {
            Person overloadedPerson = FindPersonById(state, overload.PersonId);
            if (overloadedPerson == null)
            {
                continue;
            }

            List<SourceAssignment> sources = BuildSourceAssignments(state, overloadedPerson, overload.Week); //finds all projects for person on the over hour week
            if (sources.Count == 0)
            {
                continue;
            }

            List<Person> targets = GetReplacementCandidates(state, overloadedPerson, overload.Week); //Finds people with same role
            if (targets.Count == 0)
            {
                continue;
            }

            foreach (SourceAssignment source in sources) //Project/week from person
            {
                foreach (Person target in targets) //To person
                {
                    int targetRemaining = GetRemainingCapacity(state, target, overload.Week); //To persons available hours
                    int maxHoursToMove = Math.Min(source.SourceHours, RoundDownToNearestFive(targetRemaining)); //Makes sure it goes by 5
                    if (maxHoursToMove < 10) //Cannot be under 10
                    {
                        continue;
                    }

                    for (int hoursToMove = 10; hoursToMove <= maxHoursToMove; hoursToMove += 5) //Every move by 5
                    {
                        moves.Add(new MoveCandidate
                        {
                            Project = source.Project,
                            OverloadedPerson = overloadedPerson,
                            ReplacementPerson = target,
                            RawWeek = source.RawWeek,
                            ShiftedWeek = overload.Week,
                            HoursToMove = hoursToMove
                        });
                    }
                }
            }
        }

        return moves;
    }

    private static int RoundDownToNearestFive(int value) //Normalizes capacity to 5 hour increments. If this is too slow might need to make it 10 to cut moves in half 
    {
        if (value <= 0) //no negatives
        {
            return 0;
        }

        return (value / 5) * 5;
    }



    private List<OverloadCell> BuildOverloadCells(ScheduleState state)
    {
        var cells = new List<OverloadCell>(); //Over capacity person/weeks list

        foreach (KeyValuePair<ScheduleState.PersonWeekKey, int> kv in state.PersonWeekHours)
        {
            Person person = FindPersonById(state, kv.Key.PersonId);
            if (person == null)
            {
                continue;
            }

            int capacity;
            if (person.capacity > 0)
            {
                capacity = person.capacity;
            }
            else
            {
                capacity = 40;
            }

            int overloadAmount = kv.Value - capacity; //How over?

            if (overloadAmount <= 0)
            {
                continue;
            }

            cells.Add(new OverloadCell //Save details for overloaded info
            {
                PersonId = kv.Key.PersonId,
                Week = kv.Key.Week,
                AssignedHours = kv.Value,
                Capacity = capacity,
                OverloadAmount = overloadAmount
            });
        }

        cells.Sort((a, b) => b.OverloadAmount.CompareTo(a.OverloadAmount)); //Larger overloads first
        return cells;
    }

    private List<SourceAssignment> BuildSourceAssignments(ScheduleState state, Person overloadedPerson, int shiftedWeek) //Projects that overloaded person is on that week
    {
        var sources = new List<SourceAssignment>();

        foreach (KeyValuePair<Project, Dictionary<int, int>> assignment in overloadedPerson.projects)
        {
            Project project = assignment.Key;
            int rawWeek = shiftedWeek - state.GetShift(project);

            if (!assignment.Value.TryGetValue(rawWeek, out int sourceHours)) //Checks whether overloaded person has hours for this project/rawWeek
            {
                continue;
            }

            if (sourceHours < 10) //Cannot have 1-9 houirs
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

    private List<Person> GetReplacementCandidates(ScheduleState state, Person overloadedPerson, int shiftedWeek) //People with capacity
    {
        var candidates = new List<Person>();

        foreach (Person person in state.People)
        {
            if (person.id == overloadedPerson.id) //No self transfer
            {
                continue;
            }

            if (!string.Equals(person.role, overloadedPerson.role, StringComparison.OrdinalIgnoreCase)) //Same role required
            {
                continue;
            }

            int availableCapacity = GetRemainingCapacity(state, person, shiftedWeek); //Updated capacity
            if (availableCapacity < 10)
            {
                continue;
            }

            candidates.Add(person);
        }

        return candidates;
    }
    private int GetRemainingCapacity(ScheduleState state, Person person, int week)
    {
        int currentHours = 0;
        state.PersonWeekHours.TryGetValue(new ScheduleState.PersonWeekKey(person.id, week), out currentHours);
        int capacity;
        if (person.capacity > 0)
        {
            capacity = person.capacity;
        }
        else
        {
            capacity = 40;
        }
        return capacity - currentHours;
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

    private string BuildStateHash(ScheduleState state) //builds logic so the same state is only explored once
    {
        var parts = new List<string>(); //Normalized state 

        foreach (Project project in state.Projects.OrderBy(p => p.id)) //Orders projects
        {
            parts.Add("SHIFT:" + project.id + ":" + state.GetShift(project)); 
        }

        foreach (Person person in state.People.OrderBy(p => p.id)) //Order people
        {
            foreach (KeyValuePair<Project, Dictionary<int, int>> projectEntry in person.projects.OrderBy(kv => kv.Key.id)) //Projects per person
            {
                foreach (KeyValuePair<int, int> weekEntry in projectEntry.Value.OrderBy(kv => kv.Key)) //Weeks/hours per project
                {
                    parts.Add("ASSIGN:" + person.id + ":" + projectEntry.Key.id + ":" + weekEntry.Key + ":" + weekEntry.Value); //Person/project/weeks/hours assignment
                }
            }
        }

        return string.Join("|", parts); //Returns one string that is unique to this state
    }

    private AssignmentSnapshot CaptureSnapshot(ScheduleState state)  //keeps a snapshot of changes for comparison ak grid v grid
    {
        var personAssignments = new Dictionary<Person, Dictionary<Project, Dictionary<int, int>>>(); //Copy person/project/weeks map
        foreach (Person person in state.People)
        {
            var byProject = new Dictionary<Project, Dictionary<int, int>>(person.projects);
            foreach (KeyValuePair<Project, Dictionary<int, int>> kv in person.projects) //takes list of people on project
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
        return new AssignmentSnapshot  //Return one snapshot object containing both views of the same schedule, person/project/weeks and project/people so we can build a full state
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
    }


}