using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
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
            var ordered = state.Projects
     .OrderByDescending(p => GetConflictScore(p))
     .ThenByDescending(p => state.GetDuration(p))
     .ThenByDescending(p => p.people.Count)
     .ToList();


            bool anyShifted = false; //track if any schedules are moved 

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
                    MoveWeekToReplacement(state, project, overloadedPerson, replacementPerson, week);
                    changed = true; //successfully fixed
                }
            }

        }

        return changed;
    }


    //we can probably move this method out somewhere else later but just the initial handoff shift logic
    public static void MoveWeekToReplacement(ScheduleState state, Project project, Person from, Person to, int week)
{
    // 1. 先把小时数存起来，因为 Remove 之后就拿不到了
    int hours = from.getHoursForProjecForWeek(project, week);

    // 2. 尝试从原负责人手中移除
    if (!from.projects.ContainsKey(project) || !from.projects[project].Remove(week))
    {
        return;
    }

    // 3. 准备给接收者（to）
    if (!to.projects.ContainsKey(project))
    {
        to.projects[project] = new Dictionary<int, int>();
    }

    // --- 修复点：使用索引器 [week] 而不是 .Add() ---
    // 这样如果 key 已存在，它会覆盖旧值而不是报错崩溃
    to.projects[project][week] = hours; 

    // 4. 更新项目的参与人员名单
    project.people.Add(to); // HashSet 会自动处理重复，所以 Add 是安全的

    if (from.projects[project].Count == 0)
    {
        project.people.Remove(from);
    }

    // 5. 重构网格以反映更改
    state.RebuildGrid();
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