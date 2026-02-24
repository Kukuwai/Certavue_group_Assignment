using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using Google.OrTools.ConstraintSolver;
using static ScheduleState;


//refactored logic
//List is sorted into person/weeks with most hours over worked and projects they work on
//For those projects do full block moves, split the project up, move the project fully, reassign roles hours
//still only improves fitness (pending Luca on this)


public class GreedyAlg
{
    public ScheduleState StartGreedy(List<Person> people, List<Project> projects)
    {
        //state keeps track of shifts
        var state = new ScheduleState(people, projects);
        BuildGreedySchedule(state);

        return state; //I added this for to work with finding conflicts. 
    }
    public void BuildGreedySchedule(ScheduleState state)
    {
        const int maxPasses = 100; //Can be whatever we want
        ScheduleHandler scheduleHandler = new ScheduleHandler(state);

        for (int pass = 1; pass <= maxPasses; pass++)
        {
            List<ScheduleState.OverloadCell> overloads = GetOverloadCellsOrdered(state); //list of over time cells

            if (overloads.Count == 0)
            {
                break; //Everything is good
            }

            bool appliedMove = false;

            foreach (ScheduleState.OverloadCell cell in overloads) //starts with worst and goes down
            {
                MoveChoice bestMove = GetBestMoveForWorstCell(state, scheduleHandler, cell);

                if (bestMove == null || bestMove.Delta <= 0)
                {
                    continue;
                }

                ApplyMoveChoice(state, bestMove); //applies first move that improves delta/fitness
                printStats("Greedy Pass " + pass, state);
                appliedMove = true;
                break;
            }

            if (!appliedMove)
            {
                break;
            }
        }
    }
    private List<ScheduleState.OverloadCell> GetOverloadCellsOrdered(ScheduleState state)
    {
        List<ScheduleState.OverloadCell> cells = new List<ScheduleState.OverloadCell>(); //Store all overloaded person/week cells 

        foreach (KeyValuePair<ScheduleState.PersonWeekKey, int> kv in state.PersonWeekHours) //Check every person/week total hour entry
        {
            Person person = null;
            foreach (Person p in state.People)
            {
                if (p.id == kv.Key.PersonId)
                {
                    person = p;
                    break;
                }
            }

            if (person == null)
            {
                continue;
            }

            int capacity = person.capacity; //This persons capacity
            if (capacity <= 0) //
            {
                capacity = 40; //40 if missing
            }

            if (kv.Value <= capacity)
            {
                continue;
            }

            ScheduleState.OverloadCell cell = new ScheduleState.OverloadCell(); //Greedy ranking
            cell.PersonId = kv.Key.PersonId;
            cell.Week = kv.Key.Week;
            cell.AssignedHours = kv.Value;
            cell.Capacity = capacity;
            cells.Add(cell);
        }

        for (int i = 0; i < cells.Count - 1; i++) //Current position to fill
        {
            for (int j = i + 1; j < cells.Count; j++) //Compares later cell to current one
            {
                if (cells[j].GetOverload() > cells[i].GetOverload()) //If later cell is more overworked move it forward
                {
                    ScheduleState.OverloadCell temp = cells[i];
                    cells[i] = cells[j];
                    cells[j] = temp;
                }
            }
        }

        return cells;
    }


    private List<Project> GetProjectsForPersonWeek(ScheduleState state, int personId, int week)
    {
        List<Project> result = new List<Project>();

        foreach (Project project in state.Projects)
        {
            int shift = state.GetShift(project); //Current proj shift
            List<ScheduleState.WeekKey> grid = state.GetGrid(project, shift); //Shifted cells

            bool assigned = false; //Is proj in person/week
            foreach (ScheduleState.WeekKey cell in grid)
            {
                if (cell.PersonId == personId && cell.Week == week) //Match overloaded person week
                {
                    assigned = true;
                    break;
                }
            }

            if (assigned) //
            {
                result.Add(project); //Add for review
            }
        }

        return result;
    }

    private void KeepBetter(ref MoveChoice best, MoveChoice candidate)
    {
        if (candidate == null)
        {
            return;
        }

        if (best == null || candidate.Delta > best.Delta) //replace best if better schedule is here
        {
            best = candidate;
        }
    }

    private MoveChoice GetBestMoveForWorstCell(ScheduleState state, ScheduleHandler scheduleHandler, ScheduleState.OverloadCell worst)
    {
        MoveChoice best = null;

        List<Project> ordered = GetProjectsForPersonWeek(state, worst.PersonId, worst.Week); //only projects a part of the overloaded cell

        foreach (Project project in ordered)
        {
            MoveChoice weekChoice = GetBestProjectWeekMoveOrSplit(state, scheduleHandler, project, worst.Week); //try to move/split projects overloaded weeks
            KeepBetter(ref best, weekChoice);

            MoveChoice shiftChoice = GetBestProjectShiftMove(state, scheduleHandler, project); //move whole project shift
            KeepBetter(ref best, shiftChoice);
        }

        MoveChoice roleChoice = GetBestRoleReassignMove(state, scheduleHandler, worst, ordered); //move hours to another person in same role
        KeepBetter(ref best, roleChoice);

        return best;
    }

    private MoveChoice GetBestProjectShiftMove(ScheduleState state, ScheduleHandler scheduleHandler, Project project)
    {
        int currentShift = state.GetShift(project);
        MoveChoice best = null;

        foreach (int candidateShift in state.GetValidShifts(project))
        {
            double delta = scheduleHandler.EvaluateMoveDelta(project, candidateShift); //score impact of this shift

            if (delta <= 0) //didn't improve
            {
                continue;
            }

            MoveChoice choice = new MoveChoice();
            choice.Type = MoveType.ProjectShift;
            choice.Project = project;
            choice.CandidateShift = candidateShift;
            choice.Delta = delta;

            KeepBetter(ref best, choice); //best shift 
        }

        return best;
    }

    private List<int> GetCandidateTargetRawWeeks(ScheduleState state, Project project, int sourceRawWeek)
    {
        List<int> targets = new List<int>();
        int currentShift = state.GetShift(project);

        foreach (int candidateShift in state.GetValidShifts(project))
        {
            int delta = candidateShift - currentShift; //how far is the shift
            int targetRawWeek = sourceRawWeek + delta;
            if (targetRawWeek == sourceRawWeek)
            {
                continue;
            }

            if (!targets.Contains(targetRawWeek))
            {
                targets.Add(targetRawWeek); //prevents dups
            }
        }

        return targets;
    }

    private MoveChoice GetBestProjectWeekMoveOrSplit(ScheduleState state, ScheduleHandler scheduleHandler, Project project, int overloadedShiftedWeek)
    {
        int projectShift = state.GetShift(project); //Current project shift
        int sourceRawWeek = overloadedShiftedWeek - projectShift;
        List<int> targets = GetCandidateTargetRawWeeks(state, project, sourceRawWeek); //Candidate target raw weeks
        MoveChoice best = null;

        foreach (int targetRawWeek in targets) //Try each destination week
        {
            AssignmentSnapshot snapMove = CaptureSnapshot(state); //Save state
            double beforeMove = scheduleHandler.CalculateFitnessScore(state); //Baseline score

            bool moved = TryMoveProjectWeekBlock(state, project, sourceRawWeek, targetRawWeek); //Try moving full block
            if (moved)
            {
                double afterMove = scheduleHandler.CalculateFitnessScore(state); //Score after trial
                double deltaMove = afterMove - beforeMove;

                if (deltaMove > 0) //Only improvements
                {
                    MoveChoice weekChoice = new MoveChoice();
                    weekChoice.Type = MoveType.WeekMove;
                    weekChoice.Project = project;
                    weekChoice.SourceRawWeek = sourceRawWeek;
                    weekChoice.TargetRawWeek = targetRawWeek;
                    weekChoice.Delta = deltaMove;
                    KeepBetter(ref best, weekChoice); //Compare against current best
                }
            }

            RestoreSnapshot(state, snapMove); //Undo trial

            for (int splitHours = 10; splitHours <= 40; splitHours += 5) //Try every allowed split size
            {
                AssignmentSnapshot snapSplit = CaptureSnapshot(state); //Save state
                double beforeSplit = scheduleHandler.CalculateFitnessScore(state); //Baseline score

                bool splitMoved = TrySplitProjectWeekBlock(state, project, sourceRawWeek, targetRawWeek, splitHours); //Try split
                if (splitMoved)
                {
                    double afterSplit = scheduleHandler.CalculateFitnessScore(state); //Score after split trial
                    double deltaSplit = afterSplit - beforeSplit;

                    if (deltaSplit > 0) //Keep only improvements
                    {
                        MoveChoice splitChoice = new MoveChoice();
                        splitChoice.Type = MoveType.WeekSplit;
                        splitChoice.Project = project;
                        splitChoice.SourceRawWeek = sourceRawWeek;
                        splitChoice.TargetRawWeek = targetRawWeek;
                        splitChoice.MoveHours = splitHours; //Save split size
                        splitChoice.Delta = deltaSplit;
                        KeepBetter(ref best, splitChoice); //Compare against current best
                    }
                }

                RestoreSnapshot(state, snapSplit); //Undo changes
            }
        }

        return best;
    }


    private MoveChoice GetBestRoleReassignMove(ScheduleState state, ScheduleHandler scheduleHandler, ScheduleState.OverloadCell worst, List<Project> orderedProjects)
    {
        MoveChoice best = null;

        Person overloadedPerson = null;
        foreach (Person person in state.People)
        {
            if (person.id == worst.PersonId)
            {
                overloadedPerson = person;
                break;
            }
        }

        foreach (Project project in orderedProjects)
        {
            int projectShift = state.GetShift(project);
            int rawWeek = worst.Week - projectShift;

            if (rawWeek < 1 || rawWeek > 52)
            {
                continue;
            }

            int rawHours = overloadedPerson.getHoursForProjecForWeek(project, rawWeek); //possible reassignments
            int moveHours = NormalizeHoursByRule(rawHours); //keeps the 5 and 10 rules from earlier
            if (moveHours < 10)
            {
                continue;
            }

            foreach (Person candidate in state.People)
            {
                if (candidate.id == overloadedPerson.id) //cannot be same person
                {
                    continue;
                }

                if (candidate.role != overloadedPerson.role) //has to be same role
                {
                    continue;
                }

                if (!CanTakeHours(state, candidate, worst.Week, moveHours)) //has to have capacity
                {
                    continue;
                }

                AssignmentSnapshot snap = CaptureSnapshot(state); //checks move
                double before = scheduleHandler.CalculateFitnessScore(state);

                bool moved = MoveHoursToReplacement(state, project, overloadedPerson, candidate, rawWeek, worst.Week, moveHours);

                if (moved)
                {
                    double after = scheduleHandler.CalculateFitnessScore(state);
                    double delta = after - before;

                    if (delta > 0) //only keep improvements
                    {
                        MoveChoice choice = new MoveChoice();
                        choice.Type = MoveType.RoleReassign;
                        choice.Project = project;
                        choice.FromPerson = overloadedPerson;
                        choice.ToPerson = candidate;
                        choice.RawWeek = rawWeek;
                        choice.ShiftedWeek = worst.Week;
                        choice.MoveHours = moveHours;
                        choice.Delta = delta;
                        KeepBetter(ref best, choice);
                    }
                }

                RestoreSnapshot(state, snap);
            }
        }

        return best;
    }

    private void ApplyMoveChoice(ScheduleState state, MoveChoice choice)
    {
        if (choice.Type == MoveType.ProjectShift)
        {
            state.ApplyShift(choice.Project, choice.CandidateShift); //apply move since it improves fitness
            return;
        }

        if (choice.Type == MoveType.WeekMove) //moves entire week block for project
        {
            TryMoveProjectWeekBlock(state, choice.Project, choice.SourceRawWeek, choice.TargetRawWeek);
            return;
        }

        if (choice.Type == MoveType.WeekSplit) //splitting the week
        {
            TrySplitProjectWeekBlock(state, choice.Project, choice.SourceRawWeek, choice.TargetRawWeek, choice.MoveHours);
            return;
        }


        if (choice.Type == MoveType.RoleReassign) //work assigned to another person in role
        {
            MoveHoursToReplacement(state, choice.Project, choice.FromPerson, choice.ToPerson, choice.RawWeek, choice.ShiftedWeek, choice.MoveHours);
        }
    }


    private class WeekMoveEntry //one person impacted by a project-week block move.
    {
        public Person Person;
        public int SourceHours;
        public int TargetHours;
    }

    private class WeekSplitEntry //Per person values to apply
    {
        public Person Person;
        public int SourceAfter;
        public int TargetAfter;
    }

    private enum MoveType //Checks moves 
    {
        WeekMove,
        WeekSplit,
        ProjectShift,
        RoleReassign
    }

    private class MoveChoice //One potential canidate to apply move to
    {
        public MoveType Type;
        public Project Project;
        public int SourceRawWeek;
        public int TargetRawWeek;
        //public int SplitPercent;
        public int CandidateShift;
        public Person FromPerson;
        public Person ToPerson;
        public int RawWeek;
        public int ShiftedWeek;
        public int MoveHours;
        public double Delta;
    }

    private class AssignmentSnapshot //Snapshot of move
    {
        public Dictionary<Person, Dictionary<Project, Dictionary<int, int>>> PersonAssignments;
        public Dictionary<Project, HashSet<Person>> ProjectPeople;
    }
    private bool TryMoveProjectWeekBlock(ScheduleState state, Project project, int sourceRawWeek, int targetRawWeek)
    {
        if (sourceRawWeek == targetRawWeek) //cannot equal same week
        {
            return false;
        }

        bool okOrder = state.PreservesProjectWeekOrder(project, sourceRawWeek, targetRawWeek); //keeps work in order
        if (!okOrder)
        {
            return false;
        }

        int projectShift = state.GetShift(project); //current shift
        int shiftedTargetWeek = targetRawWeek + projectShift;

        List<WeekMoveEntry> entries = new List<WeekMoveEntry>(); //all people and hours impacted by this block move

        foreach (Person person in project.people)
        {
            Dictionary<int, int> weekHours;
            if (!person.projects.TryGetValue(project, out weekHours)) //person has no hours on this project
            {
                continue;
            }

            int sourceHours;
            if (!weekHours.TryGetValue(sourceRawWeek, out sourceHours)) //no hours in source week
            {
                continue;
            }

            int targetHours = 0; //no existing hours ie blank
            weekHours.TryGetValue(targetRawWeek, out targetHours); //existing target hours

            if (!CanTakeHours(state, person, shiftedTargetWeek, sourceHours)) //is person under 40 hours
            {
                return false;
            }

            WeekMoveEntry entry = new WeekMoveEntry(); //all updates done together
            entry.Person = person;
            entry.SourceHours = sourceHours;
            entry.TargetHours = targetHours;
            entries.Add(entry);
        }

        if (entries.Count == 0) //no change
        {
            return false;
        }

        foreach (WeekMoveEntry entry in entries) //actually makes the move
        {
            Dictionary<int, int> weekHours = entry.Person.projects[project];
            weekHours.Remove(sourceRawWeek);
            weekHours[targetRawWeek] = entry.TargetHours + entry.SourceHours;
        }

        state.RebuildGrid();
        return true;
    }

    private bool TrySplitProjectWeekBlock(ScheduleState state, Project project, int sourceRawWeek, int targetRawWeek, int splitHours)
    {
        bool okOrder = state.PreservesProjectWeekOrder(project, sourceRawWeek, targetRawWeek); //keeps project order
        if (!okOrder)
        {
            return false;
        }

        int projectShift = state.GetShift(project);
        int shiftedTargetWeek = targetRawWeek + projectShift;

        List<WeekSplitEntry> entries = new List<WeekSplitEntry>(); //per person updates 

        foreach (Person person in project.people)
        {
            Dictionary<int, int> weekHours;
            if (!person.projects.TryGetValue(project, out weekHours)) //person isn't on project
            {
                continue;
            }

            int sourceHours;
            if (!weekHours.TryGetValue(sourceRawWeek, out sourceHours)) //person has no hours
            {
                continue;
            }

            int targetHours = 0;
            weekHours.TryGetValue(targetRawWeek, out targetHours); //existing target hours if any

            int moveHours = splitHours;

            if (sourceHours < moveHours)
            {
                return false;
            }

            int sourceAfter = sourceHours - moveHours;
            int targetAfter = targetHours + moveHours;

            if (sourceAfter > 0 && sourceAfter < 10)
            {
                return false;
            }
            if (!CanTakeHours(state, person, shiftedTargetWeek, moveHours))
            {
                return false;
            }

            WeekSplitEntry entry = new WeekSplitEntry();
            entry.Person = person;
            entry.SourceAfter = sourceAfter;
            entry.TargetAfter = targetAfter;
            entries.Add(entry);
        }

        if (entries.Count == 0)
        {
            return false;
        }

        foreach (WeekSplitEntry entry in entries)
        {
            Dictionary<int, int> weekHours = entry.Person.projects[project];

            if (entry.SourceAfter == 0)
            {
                weekHours.Remove(sourceRawWeek);
            }
            else
            {
                weekHours[sourceRawWeek] = entry.SourceAfter;
            }

            weekHours[targetRawWeek] = entry.TargetAfter;
        }

        state.RebuildGrid();
        return true;
    }

    private AssignmentSnapshot CaptureSnapshot(ScheduleState state)
    {
        AssignmentSnapshot snapshot = new AssignmentSnapshot(); //Copy of current schedule state
        snapshot.PersonAssignments = new Dictionary<Person, Dictionary<Project, Dictionary<int, int>>>(); //Person/project/week/hours storage
        snapshot.ProjectPeople = new Dictionary<Project, HashSet<Person>>(); //Project/people copy

        foreach (Person person in state.People) //Every person's current assignments
        {
            Dictionary<Project, Dictionary<int, int>> byProject = new Dictionary<Project, Dictionary<int, int>>(); //Person/project 

            foreach (KeyValuePair<Project, Dictionary<int, int>> kv in person.projects) //Each project person has
            {
                byProject[kv.Key] = new Dictionary<int, int>(kv.Value); //Week/hours copy
            }

            snapshot.PersonAssignments[person] = byProject;
        }

        foreach (Project project in state.Projects) //Each projects people's list
        {
            snapshot.ProjectPeople[project] = new HashSet<Person>(project.people); //Keep a copy to switch back toi
        }

        return snapshot;
    }

    private void RestoreSnapshot(ScheduleState state, AssignmentSnapshot snapshot)
    {
        foreach (Person person in state.People) //Change back everyones info
        {
            person.projects.Clear(); //Remove current info 

            Dictionary<Project, Dictionary<int, int>> byProject;
            if (!snapshot.PersonAssignments.TryGetValue(person, out byProject))
            {
                continue; //If nothing then leave nothing
            }

            foreach (KeyValuePair<Project, Dictionary<int, int>> kv in byProject) //Person/project/weeks
            {
                person.projects[kv.Key] = new Dictionary<int, int>(kv.Value); //Copy back into schedule state
            }
        }

        foreach (Project project in state.Projects) //Every person's project list
        {
            project.people.Clear(); //Clear people on project list

            HashSet<Person> people;
            if (!snapshot.ProjectPeople.TryGetValue(project, out people))
            {
                continue;
            }

            foreach (Person person in people) //Saved for this project
            {
                project.people.Add(person);
            }
        }

        state.RebuildGrid();
    }


    private static int NormalizeHoursByRule(int hours)
    {
        int rounded = (hours / 5) * 5; //Round down to nearest multiple of 5
        if (rounded < 10) //Minimum legal nonzero move is 10
        {
            return 0; //No moves
        }

        return rounded;
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
    
    public static bool MoveHoursToReplacement(ScheduleState state, Project project, Person from, Person to, int rawWeek, int shiftedWeek, int hoursToMove)
    {
        if (hoursToMove < 10) //Must be 10 hours
        {
            return false;
        }

        if (hoursToMove % 5 != 0) //Multiples of 5
        {
            return false;
        }

        if (from.role != to.role) //Role must match
        {
            return false;
        }

        if (!from.projects.ContainsKey(project)) //Source person must actually be on this project
        {
            return false;
        }

        if (!from.projects[project].ContainsKey(rawWeek)) //Source person must have hours in this week (prior moves)
        {
            return false;
        }

        if (!CanTakeHours(state, to, shiftedWeek, hoursToMove)) //Make sure receiving person can take these hours in calendar week view
        {
            return false;
        }

        int fromHours = from.projects[project][rawWeek]; //Current source person hours on this project/week
        if (fromHours < hoursToMove) //Cannot move more than source currently has
        {
            return false;
        }

        int toHours = 0;
        if (to.projects.ContainsKey(project) && to.projects[project].ContainsKey(rawWeek)) //Check if to person has hours on same week
        {
            toHours = to.projects[project][rawWeek];
        }

        int fromAfter = fromHours - hoursToMove; //From hours after move
        int toAfter = toHours + hoursToMove; //To hours after move

        if (fromAfter > 0 && fromAfter < 10) //source block must be valid 
        {
            return false;
        }

        if (toAfter > 0 && toAfter < 10) //To mst be valid
        {
            return false;
        }

        if (fromAfter > 0 && fromAfter % 5 != 0) //5 hour increments
        {
            return false;
        }

        if (toAfter > 0 && toAfter % 5 != 0) //5 hour increments
        {
            return false;
        }


        if (fromAfter == 0)
        {
            from.projects[project].Remove(rawWeek); //remove that week entry if empty now
        }
        else
        {
            from.projects[project][rawWeek] = fromAfter; //Change to lower hours
        }

        if (!to.projects.ContainsKey(project)) //Create project/week
        {
            to.projects[project] = new Dictionary<int, int>();
        }

        to.projects[project][rawWeek] = toAfter;

        if (from.projects[project].Count == 0) //If source has no remaining weeks on this project then delete them
        {
            project.people.Remove(from);
        }

        project.people.Add(to);
        state.RebuildGrid();
        return true;
    }

    //Holds the scoring results for a candidate shift
    public class ShiftScore
    {
        public int DeltaDoubleBooked { get; set; } //double booked change
        public int OverlapAfter { get; set; } //remaining double booked after a move
        public int ShiftDistance { get; set; } //shift size aka smaller may = better
    }

    public void printStats(string dataName, ScheduleState state)
    {
        ScheduleHandler handler = new ScheduleHandler(state);
        var conflictScore = handler.GetConflictScore(state);
        var movementScore = handler.GetMovementScore(state);
        var focusScore = handler.GetFocusScore(state);
        var continuityScore = handler.GetContinuityScore(state);
        var durationScore = handler.GetDurationScore(state);
        var fitnessScore = handler.CalculateFitnessScore(state);
        Console.WriteLine($"|----- {dataName} -----|");
        Console.WriteLine($"Finess Score - {fitnessScore.ToString("F2")}\nBreakdown - Conflict Score: {conflictScore.ToString("F2")} || Movement Score: {movementScore.ToString("F2")} || Focus Score: {focusScore.ToString("F2")} || Continuity Score: {continuityScore.ToString("F2")} || Duration Score: {durationScore.ToString("F2")}\n");
    }

}