using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using Google.OrTools.ConstraintSolver;
using static ScheduleState;

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
        const int maxPasses = 10; //Can be whatever we want
        ScheduleHandler scheduleHandler = new ScheduleHandler(state);

        for (int pass = 1; pass <= maxPasses; pass++)
        {
            ScheduleState.OverloadCell worst;
            bool hasWorst = state.TryGetWorstOverloadCell(out worst); //Pick worst overloaded person/week
            if (!hasWorst)
            {
                break; //Stop when nobody is overtime
            }

            MoveChoice bestMove = GetBestMoveForWorstCell(state, scheduleHandler, worst); //Finds best move
            if (bestMove == null || bestMove.Delta <= 0)
            {
                break; //No improvement
            }

            ApplyMoveChoice(state, bestMove); //Apply one move
            printStats("Greedy Pass " + pass, state);
        }
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

            if (targetRawWeek < 1 || targetRawWeek > 52) //stays in calendar
            {
                continue;
            }

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
        int projectShift = state.GetShift(project);
        int sourceRawWeek = overloadedShiftedWeek - projectShift;

        if (sourceRawWeek < 1 || sourceRawWeek > 52)
        {
            return null;
        }

        if (!ProjectHasRawWeek(project, sourceRawWeek)) //make sure week exists
        {
            return null;
        }

        List<int> targets = GetCandidateTargetRawWeeks(state, project, sourceRawWeek);
        MoveChoice best = null;

        foreach (int targetRawWeek in targets)
        {
            AssignmentSnapshot snapMove = CaptureSnapshot(state); //try to move entire week block
            double beforeMove = scheduleHandler.CalculateFitnessScore(state);
            bool moved = TryMoveProjectWeekBlock(state, project, sourceRawWeek, targetRawWeek);
            if (moved)
            {
                double afterMove = scheduleHandler.CalculateFitnessScore(state);
                double deltaMove = afterMove - beforeMove;

                if (deltaMove > 0)
                {
                    MoveChoice choice = new MoveChoice();
                    choice.Type = MoveType.WeekMove;
                    choice.Project = project;
                    choice.SourceRawWeek = sourceRawWeek;
                    choice.TargetRawWeek = targetRawWeek;
                    choice.Delta = deltaMove;
                    KeepBetter(ref best, choice);
                }
            }
            RestoreSnapshot(state, snapMove);

            int[] splitPercents = new int[] { 25, 50, 75 };             //Split candidates (same project-week block, split across two weeks) if they are too large
            foreach (int splitPercent in splitPercents)
            {
                AssignmentSnapshot snapSplit = CaptureSnapshot(state);
                double beforeSplit = scheduleHandler.CalculateFitnessScore(state);
                bool splitMoved = TrySplitProjectWeekBlock(state, project, sourceRawWeek, targetRawWeek, splitPercent); //partial hours movement

                if (splitMoved)
                {
                    double afterSplit = scheduleHandler.CalculateFitnessScore(state);
                    double deltaSplit = afterSplit - beforeSplit;

                    if (deltaSplit > 0)
                    {
                        MoveChoice splitChoice = new MoveChoice();
                        splitChoice.Type = MoveType.WeekSplit;
                        splitChoice.Project = project;
                        splitChoice.SourceRawWeek = sourceRawWeek;
                        splitChoice.TargetRawWeek = targetRawWeek;
                        splitChoice.SplitPercent = splitPercent;
                        splitChoice.Delta = deltaSplit;
                        KeepBetter(ref best, splitChoice);
                    }
                }

                RestoreSnapshot(state, snapSplit);
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

        if (overloadedPerson == null)
        {
            return null;
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

        if (choice.Type == MoveType.WeekSplit) //splits week 
        {
            TrySplitProjectWeekBlock(state, choice.Project, choice.SourceRawWeek, choice.TargetRawWeek, choice.SplitPercent);
            return;
        }

        if (choice.Type == MoveType.RoleReassign) //work assigned to another person in role
        {
            MoveHoursToReplacement(
                state,
                choice.Project,
                choice.FromPerson,
                choice.ToPerson,
                choice.RawWeek,
                choice.ShiftedWeek,
                choice.MoveHours);
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
        public int SplitPercent;
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


    private bool ProjectHasRawWeek(Project project, int rawWeek) //check if proj has any work assigned on unshifted week
    {
        foreach (Person person in project.people)
        {
            Dictionary<int, int> weekHours;
            if (!person.projects.TryGetValue(project, out weekHours))
            {
                continue;
            }

            if (weekHours.ContainsKey(rawWeek))
            {
                return true;
            }
        }

        return false;
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

            if (sourceHours < 10 || sourceHours % 5 != 0)
            {
                return false;
            }

            int combined = targetHours + sourceHours; //after move hours
            if (combined < 10 || combined % 5 != 0)
            {
                return false;
            }

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


    private bool TryBestProjectWeekMove(ScheduleState state, ScheduleHandler scheduleHandler, Project project, int overloadedShiftedWeek) //Look for small moves and apply best move/delta
    {
        int projectShift = state.GetShift(project);

        int sourceRawWeek = overloadedShiftedWeek - projectShift;         //Convert shifted week back to original storage week for this project

        if (sourceRawWeek < 1 || sourceRawWeek > 52)
        {
            return false;
        }

        if (!ProjectHasRawWeek(project, sourceRawWeek))
        {
            return false;
        }

        List<int> candidateTargets = new List<int>(); //possible shifts
        candidateTargets.Add(sourceRawWeek - 1);
        candidateTargets.Add(sourceRawWeek + 1);

        double scoreBefore = scheduleHandler.CalculateFitnessScore(state);
        bool foundBetter = false;
        double bestDelta = 0.0;
        int bestTarget = sourceRawWeek;

        foreach (int targetRawWeek in candidateTargets)
        {
            int shiftedTargetWeek = targetRawWeek + projectShift;       //shifted calendar for boundary/window checks.

            if (shiftedTargetWeek < 1 || shiftedTargetWeek > 52)
            {
                continue;
            }
            if (shiftedTargetWeek <= project.startDate || shiftedTargetWeek >= project.endDate) //stays in window
            {
                continue;
            }

            bool moved = TryMoveProjectWeekBlock(state, project, sourceRawWeek, targetRawWeek); //possible move
            if (!moved)
            {
                continue;
            }

            double scoreAfter = scheduleHandler.CalculateFitnessScore(state);
            double delta = scoreAfter - scoreBefore;

            TryMoveProjectWeekBlock(state, project, targetRawWeek, sourceRawWeek);  //undoes move

            if (delta > bestDelta) //keeps best
            {
                bestDelta = delta;
                bestTarget = targetRawWeek;
                foundBetter = true;
            }
        }

        if (!foundBetter)
        {
            return false;
        }
        return TryMoveProjectWeekBlock(state, project, sourceRawWeek, bestTarget);
    }


    //something to think about on this method is it replaces it with the first available person. Maybe that is fine, maybe not but good to discuss
    public bool MoveBetweenRoles(ScheduleState state)
    {
        List<ScheduleState.PersonWeekKey> conflictedPersons = new List<ScheduleState.PersonWeekKey>(); //person weeks over 40 hours
        bool changed = false;

        foreach (KeyValuePair<ScheduleState.PersonWeekKey, int> entry in state.PersonWeekHours)
        {
            Person overloadedPerson = null;
            foreach (Person person in state.People) //assign the over 40 person
            {
                if (person.id == entry.Key.PersonId)
                {
                    overloadedPerson = person;
                    break;
                }
            }
            if (overloadedPerson == null)
            {
                continue;
            }

            int capacity = overloadedPerson.capacity;
            if (capacity <= 0)
            {
                capacity = 40;
            }

            if (entry.Value > capacity) //adds if over 40 hours or cap
            {
                conflictedPersons.Add(entry.Key);
            }
        }

        foreach (ScheduleState.PersonWeekKey key in conflictedPersons) //iterates over staff assignments for each over worked person
        {
            Person overloadedPerson = null;

            foreach (Person person in state.People)
            {
                if (person.id == key.PersonId)
                {
                    overloadedPerson = person;
                    break;
                }
            }

            if (overloadedPerson == null)
            {
                continue;
            }

            int shiftedWeek = key.Week; //Week in current shifted schedule
            List<Project> weeksProjects = new List<Project>(); //Projects contributing to this overloaded week

            foreach (Project project in state.Projects) //projects person is on in shifted week
            {
                if (!project.people.Contains(overloadedPerson))
                {
                    continue;
                }

                int projectShift = state.GetShift(project);

                int rawWeek = shiftedWeek - projectShift;
                if (rawWeek < 1 || rawWeek > 52)
                {
                    continue;
                }
                Dictionary<int, int> weekHours;
                if (!overloadedPerson.projects.TryGetValue(project, out weekHours))
                {
                    continue;
                }

                if (!weekHours.ContainsKey(rawWeek))
                {
                    continue;
                }
                weeksProjects.Add(project);
            }
            foreach (Project project in weeksProjects) //for each project try to move the by 5 chunks to someone with same role
            {
                int projectShift = state.GetShift(project);
                int rawWeek = shiftedWeek - projectShift;

                int rawHours = overloadedPerson.getHoursForProjecForWeek(project, rawWeek);
                int moveHours = NormalizeHoursByRule(rawHours);

                if (moveHours < 10)
                {
                    continue;
                }

                Person replacementPerson = null;

                foreach (Person person in state.People) //first valid person who can take hours
                {
                    if (person.id == overloadedPerson.id)
                    {
                        continue;
                    }

                    if (person.role != overloadedPerson.role)
                    {
                        continue;
                    }

                    // Capacity check on shifted week total.
                    if (!CanTakeHours(state, person, shiftedWeek, moveHours))
                    {
                        continue;
                    }

                    replacementPerson = person;
                    break;
                }

                if (replacementPerson == null)
                {
                    continue; //No one cn take work
                }

                bool moved = MoveHoursToReplacement(state, project, overloadedPerson, replacementPerson, rawWeek, moveHours);
                if (moved)
                {
                    changed = true;
                }
            }
        }

        return changed;
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
    public static bool MoveHoursToReplacement(ScheduleState state, Project project, Person from, Person to, int week, int hoursToMove) //this is about to be so much code 
    {
        if (hoursToMove < 10) //Puts someone under 10
        {
            return false;
        }
        if (hoursToMove % 5 != 0) //Moves in 5 hours increments
        {
            return false;
        }
        if (from.role != to.role) //Has to be same role to trade
        {
            return false;
        }
        if (!from.projects.ContainsKey(project)) //Has to have that project (not removed earlier)
        {
            return false;
        }
        if (!from.projects[project].ContainsKey(week)) //Must have this week 
        {
            return false;
        }
        if (!CanTakeHours(state, to, week, hoursToMove)) //To person cannot be over worked already
        {
            return false;
        }
        int fromHours = from.projects[project][week]; //Current from hours
        if (fromHours < hoursToMove) //Cannot go negative
        {
            return false;
        }
        int toHours = 0; //Initial target hours
        if (to.projects.ContainsKey(project) && to.projects[project].ContainsKey(week)) //To person has this week 
        {
            toHours = to.projects[project][week]; //Target hours now
        }
        int fromAfter = fromHours - hoursToMove; //Hours for to person
        int toAfter = toHours + hoursToMove; //Updated hours
        if (fromAfter > 0 && fromAfter < 10) //Hours has to be at least 10
        {
            return false;
        }
        if (toAfter > 0 && toAfter < 10) // Rule:to person aso needs to be at least 10
        {
            return false;
        }
        if (fromAfter == 0) //From person has 0 hours left remove them
        {
            from.projects[project].Remove(week);
        }
        else
        {
            from.projects[project][week] = fromAfter; //Saves remaining hours
        }
        if (!to.projects.ContainsKey(project)) //Dictionary exists or not
        {
            to.projects[project] = new Dictionary<int, int>();
        }
        to.projects[project][week] = toAfter; //Updated to person hours
        if (from.projects[project].Count == 0) //If from isn't on proj anymore remove them
        {
            project.people.Remove(from);
        }
        project.people.Add(to); //To is added to proj people
        state.RebuildGrid();
        return true; //Move made
    }

    //Checking person's schedule for open week
    public static bool IsPersonFree(ScheduleState state, Person person, Project project, int week) //boolean that returns true/false if person is free or not
    {
        var key = new ScheduleState.WeekKey(person.id, project.id, week); //week key for review
        return !state.PersonWeekGrid.TryGetValue(key, out var count); //Checks if this person has a project for the week, if they do it returns false which means they cannot be swapped
    }

    //Holds the scoring results for a candidate shift
    public class ShiftScore
    {
        public int DeltaDoubleBooked { get; set; } //double booked change
        public int OverlapAfter { get; set; } //remaining double booked after a move
        public int ShiftDistance { get; set; } //shift size aka smaller may = better
    }

    public ShiftScore EvaluateShift(ScheduleState state, Project project, int candidateShift)
    {
        int currentShift = state.GetShift(project);

        List<WeekKey> current = new List<WeekKey>(state.GetGrid(project, currentShift)); //weeks at current shift
        List<WeekKey> candidate = new List<WeekKey>(state.GetGrid(project, candidateShift)); //weeks at new shift


        List<WeekKey> touched = new List<WeekKey>(current); //any cells impacted by change
        foreach (WeekKey k in candidate)
        {
            if (!touched.Contains(k))
                touched.Add(k);
        }

        int delta = 0; //change in 2x bookings
        int overlapAfter = 0; //how many double bookings after move

        foreach (WeekKey key in touched)
        {
            int baseCount = 0;
            state.PersonWeekGrid.TryGetValue(key, out baseCount); //current bookings from all projects
            if (current.Contains(key)) baseCount -= 1; //removes current projects placement

            //adds the canidate project p;cement
            int newCount;
            if (candidate.Contains(key))
            {
                newCount = baseCount + 1;
            }
            else
            {
                newCount = baseCount + 0;
            }
            if (baseCount >= 2) delta -= 1; //double booked pre move
            if (newCount >= 2) //double booked after move
            {
                delta += 1;
                overlapAfter++;
            }
            int conflictPenalty = 0;
            foreach (var kv in state.PersonWeekGrid)
            {
                if (kv.Value > 1)
                    conflictPenalty += (kv.Value - 1);
            }
        }


        return new ShiftScore  //result of shift
        {
            DeltaDoubleBooked = delta,
            OverlapAfter = overlapAfter,
            ShiftDistance = Math.Abs(candidateShift)
        };
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