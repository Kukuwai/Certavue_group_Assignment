using System;
using System.Collections.Generic;
using System.Linq;

public class RoleOptimizer
{
    public OptimizationResult Optimize(ScheduleState state, int maxPasses = 999999)
    {
        var handler = new ScheduleHandler(state); //Scoring variable for improvements
        double currentFitness = handler.CalculateFitnessScore(state); //Baseline fitness
        bool improvedAny = false;
        int movesApplied = 0;
        int combinationsChecked = 0;

        for (int pass = 0; pass < maxPasses; pass++)
        {
            MoveCandidate bestMove = FindBestMove(state, handler, currentFitness, out int checkedThisPass); //Searches all valid 5 hour searches and keeps the best fitness
            combinationsChecked += checkedThisPass;

            if (bestMove == null || bestMove.FitnessDelta <= 0)
            {
                break;
            }

            bool applied = GreedyAlg.MoveHoursToReplacement(state, bestMove.Project, bestMove.OverloadedPerson, bestMove.ReplacementPerson, bestMove.RawWeek, bestMove.ShiftedWeek, bestMove.HoursToMove); //Checks with current state
            if (!applied)
            {
                break;
            }

            improvedAny = true;
            movesApplied++;
            currentFitness = handler.CalculateFitnessScore(state);
        }

        return new OptimizationResult //Returns best state.
        {
            BestState = state,
            Improved = improvedAny,
            FinalFitness = currentFitness,
            WeeksImproved = movesApplied,
            CombinationsChecked = combinationsChecked
        };
    }

    private MoveCandidate FindBestMove(ScheduleState state, ScheduleHandler handler, double baselineFitness, out int combinationsChecked) //Checks all possible 5 hour moves
    {
        combinationsChecked = 0; 
        MoveCandidate best = null; 

        List<OverloadCell> overloadCells = BuildOverloadCells(state); //Builds all currently overloaded person/weeks

        foreach (OverloadCell overload in overloadCells) 
        {
            Person overloadedPerson = FindPersonById(state, overload.PersonId); 
            if (overloadedPerson == null) 
            {
                continue; 
            }

            List<SourceAssignment> sourceAssignments = BuildSourceAssignments(state, overloadedPerson, overload.Week); //All projs/weeks part of overloaded person
            if (sourceAssignments.Count == 0) 
            {
                continue; 
            }

            List<Person> replacementCandidates = GetReplacementCandidates(state, overloadedPerson, overload.Week); //Under 40 hours in same role/week
            if (replacementCandidates.Count == 0) 
            {
                continue; 
            }

            foreach (SourceAssignment source in sourceAssignments) 
            {
                foreach (Person replacement in replacementCandidates) 
                {
                    for (int hoursToMove = 10; hoursToMove <= source.SourceHours; hoursToMove += 5) //Try every legal 5 hour move
                    {
                        combinationsChecked++; 
                        AssignmentSnapshot snapshot = CaptureSnapshot(state); //State saved

                        bool moved = GreedyAlg.MoveHoursToReplacement(state, source.Project, overloadedPerson, replacement, source.RawWeek, overload.Week, hoursToMove); 

                        if (moved) 
                        {
                            double afterFitness = handler.CalculateFitnessScore(state); //After move score
                            double delta = afterFitness - baselineFitness; 

                            if (delta > 0) //Only improvements
                            {
                                int overloadAfter = GetOverloadAmount(state, overload.PersonId, overload.Week); 
                                int overloadReduction = overload.OverloadAmount - overloadAfter; 

                                var candidate = new MoveCandidate //Saved to be compared
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

                                if (IsBetterCandidate(candidate, best)) //New best when higher fitness
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