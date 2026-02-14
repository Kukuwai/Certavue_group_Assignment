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
        const int maxPasses = 10; //seeing if this improves perfornmancesince greedy is cheap.it can be any number really
        int startTotal = state.PersonWeekGrid.Values.Sum();//only occupied person/weeks
        int startNotDoubleBookedCells = state.PersonWeekGrid.Where(kv => kv.Value == 1).Sum(kv => kv.Value);

        double startPct;

        if (startTotal == 0)
        {
            startPct = 100.0;
        }
        else
        {
            startPct = (double)startNotDoubleBookedCells / startTotal * 100.0;
        }

        var scheduleHandler = new ScheduleHandler(state);

        int GetConflictScore(Project p)
        {
            int shift = state.GetShift(p);
            var grid = state.GetGrid(p, shift);

            int score = 0;
            foreach (var cell in grid)
            {
                if (state.PersonWeekGrid.TryGetValue(cell, out var count))
                    score += Math.Max(0, count - 1);
            }
            return score;
        }


        for (int pass = 1; pass <= maxPasses; pass++)
        {
            ScheduleState.OverloadCell worst; //Holds worst person/week.
            bool hasWorst = state.TryGetWorstOverloadCell(out worst); //Returns the worst overload

            if (!hasWorst) //Nothing over worked
            {
                break;
            }

            List<Project> ordered = GetProjectsForPersonWeek(state, worst.PersonId, worst.Week); //Only projects causing this overload
            if (ordered.Count == 0) //If missing
            {
                break;
            }

            bool anyShifted = false; //Changes made

            foreach (var project in ordered)  //tracks projects in order
            {
                int currentShift = state.GetShift(project);
                int bestShift = currentShift;
                var best = scheduleHandler.EvaluateMoveDelta(project, currentShift); //baseline

                foreach (int candidate in state.GetValidShifts(project)) //all allowed shifts ie within dates
                {
                    var test = scheduleHandler.EvaluateMoveDelta(project, candidate);

                    bool better = false;
                    if (test > best)
                    {
                        better = true;
                    }

                    if (better)
                    {
                        bestShift = candidate; //tracks best shift
                        best = test; //keeps the best
                    }
                }

                if (bestShift != currentShift)
                {
                    state.ApplyShift(project, bestShift); //implements the move
                    anyShifted = true;  //used to make sure changes occurred
                }
            }
            if (MoveBetweenRoles(state))
            {
                anyShifted = true;
            }

            int total = state.PersonWeekGrid.Values.Sum();//only occupied person/weeks
            int notDoubleBookedCells = state.PersonWeekGrid.Where(kv => kv.Value == 1).Sum(kv => kv.Value);

            double pct;

            if (total == 0)
            {
                pct = 100.0;
            }
            else
            {
                pct = (double)notDoubleBookedCells / total * 100.0;
            }

            printStats($"Greedy Pass {pass}", state);

            if (!anyShifted) break; //ends if nothing moves so we really could have the passes be pretty high for safety
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


    //something to think about on this method is it replaces it with the first available person. Maybe that is fine, maybe not but good to discuss
    public bool MoveBetweenRoles(ScheduleState state)
    {
        var conflictedPersons = new List<ScheduleState.WeekKey>(); //list of over booked persons
        bool changed = false; //used as the return


        foreach (var entry in state.PersonWeekGrid) //if someone has more than 1 proj for the week they are added
        {
            if (entry.Value > 1)
            {
                conflictedPersons.Add(entry.Key);
            }
        }

        foreach (var key in conflictedPersons) //will manage each conflict in the list
        {
            Person overloadedPerson = null;
            foreach (var person in state.People)
            {
                if (person.id == key.PersonId) //finds the person object attached to the conflicted person and assigns them
                {
                    overloadedPerson = person;
                    break;
                }
            }

            if (overloadedPerson == null)
            {
                continue; //nobody found go to next person
            }

            int week = key.Week;  //the week with conflict

            var weeksProjects = new List<Project>(); //list of persons project for that week

            foreach (var project in state.Projects) //checks projects to see which ones are conflicted
            {
                if (!project.people.Contains(overloadedPerson)) //ignores any project a person isn't assigned to
                {
                    continue;
                }
                int shift = state.GetShift(project);        //decides what shift and grid apply to this project
                var grid = state.GetGrid(project, shift);

                bool assigned = false;  //used to see if project will be assigned

                foreach (var cell in grid)      //sees if the project is on the actual week of issue
                {
                    if (cell.PersonId == overloadedPerson.id && cell.Week == week)
                    {
                        assigned = true;
                        break;
                    }
                }

                if (assigned)   //if it is on the weeks issue this it is added to the list
                {
                    weeksProjects.Add(project);
                }
            }

            foreach (var project in weeksProjects)  //needs to find an open replacement based on role still
            {
                Person replacementPerson = null;

                foreach (var person in state.People) //searching for a valid replacement 
                {
                    if (person.id == overloadedPerson.id)
                    {
                        continue; //can't replace themselves
                    }

                    if (person.role == overloadedPerson.role)    //roles are equal
                    {
                        if (IsPersonFree(state, person, project, week))   //calls is free method to verify opening
                        {
                            replacementPerson = person; //found the person to replace
                            break;
                        }


                    }

                }

                if (replacementPerson != null)
                {
                    int rawHours = overloadedPerson.getHoursForProjecForWeek(project, week); //current hours for the project on weekly basis
                    int moveHours = NormalizeHoursByRule(rawHours); //move hours by 5

                    if (moveHours >= 10) //hours has to be over 10
                    {
                        bool moved = MoveHoursToReplacement(state, project, overloadedPerson, replacementPerson, week, moveHours); //Partial movement logic
                        if (moved) //Move happened
                        {
                            changed = true;
                        }
                    }
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