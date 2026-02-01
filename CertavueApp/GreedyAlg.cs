using System;
using System.Collections.Generic;
using System.Linq;

public class GreedyAlg
{
    public struct WeekKey //handles a person and week cell
    {
        public int PersonId;
        public int Week;

        public WeekKey(int personId, int week)
        {
            PersonId = personId;
            Week = week;
        }
    }
    public ScheduleState StartGreedy(List<Person> people, List<Project> projects)
    {
        //state keeps track of shifts
        var state = new ScheduleState(people, projects);
        BuildGreedySchedule(state);

        return state; // I added this for to work with finding conflicts. 
    }
    public void BuildGreedySchedule(ScheduleState state)
    {
        const int maxPasses = 6; //seeing if this improves perfornmancesince greedy is cheap.it can be any number really
        //for file 75 large should start with 1036 and 36.39% double booked
        int startTotal = state.PersonWeekGrid.Values.Sum();
        int startNonConflict = state.PersonWeekGrid.Where(kv => kv.Value == 1).Sum(kv => kv.Value);
        int startDouble = state.PersonWeekGrid.Count(kv => kv.Value >= 2);
        double startPct;
        if (startTotal == 0)
        {
            startPct = 100.0;
        }
        else
        {
            startPct = (double)startNonConflict / startTotal * 100.0;
        }
        Console.WriteLine("Greedy algorithm running: ");

        Console.WriteLine("Start total: " + startTotal + ", double-booked=" + startDouble + ", % not double-booked=" + startPct.ToString("0.##"));


        for (int pass = 1; pass <= maxPasses; pass++)
        {
            var ordered = state.Projects
            .OrderByDescending(p => state.GetDuration(p))   //longest projs first
            .ThenByDescending(p => p.people.Count)          //breaks tie by most people on proj
            .ToList();

            bool anyShifted = false; //track if any schedules are moved 

            foreach (var project in ordered)  //tracks projects in order
            {
                int currentShift = state.GetShift(project);
                int bestShift = currentShift;
                ShiftScore best = EvaluateShift(state, project, currentShift); // baseline

                foreach (int candidate in state.GetValidShifts(project)) //all allowed shifts ie within dates
                {
                    ShiftScore test = EvaluateShift(state, project, candidate);

                    //goes in order fewer double booked, overlap and then shortest move
                    bool better =
                        test.DeltaDoubleBooked < best.DeltaDoubleBooked ||
                        (test.DeltaDoubleBooked == best.DeltaDoubleBooked && test.OverlapAfter < best.OverlapAfter) ||
                        (test.DeltaDoubleBooked == best.DeltaDoubleBooked && test.OverlapAfter == best.OverlapAfter && test.ShiftDistance < best.ShiftDistance);

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
            int total = state.PersonWeekGrid.Values.Sum();  //all time slots
            int nonConflict = state.PersonWeekGrid.Where(kv => kv.Value == 1).Sum(kv => kv.Value);  //clean slots aka not double booked
            int doubleBooked = state.PersonWeekGrid.Count(kv => kv.Value >= 2); //double booked
            double pct;  //% not double booked
            if (total == 0)
            {
                pct = 100;
            }

            else
            {
                pct = (double)nonConflict / total * 100;

            }

            Console.WriteLine("After pass " + pass + ", total: " + total + ", double-booked=" + doubleBooked + ", % not double-booked=" + pct.ToString("0.##"));

            if (!anyShifted) break; //ends if nothing moves so we really could have the passes be pretty high for safety
        }
    }

    // Holds the scoring results for a candidate shift
    public class ShiftScore
    {
        public int DeltaDoubleBooked { get; set; } //double booked change
        public int OverlapAfter { get; set; } //remaining double booked after a move
        public int ShiftDistance { get; set; } //shift size aka smaller may = better
    }

    // Returns a ShiftScore
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
        }


        return new ShiftScore  //result of shift
        {
            DeltaDoubleBooked = delta,
            OverlapAfter = overlapAfter,
            ShiftDistance = Math.Abs(candidateShift)
        };
    }
    public class Window    //handles the start and end weeks of project
    {
        public int Start; //as far left 
        public int End; //right
        public Window(int start, int end)
        {
            Start = start;
            End = end;
        }
    }

    //Since we designed it without a schedule class in the loader we keep track here making the "grid" in the CSV form
    public class ScheduleState
    {
        private const int Weeks = 52;
        public List<Person> People { get; } //list of all people in that schedule
        public List<Project> Projects { get; } //list of projects

        public Dictionary<WeekKey, int> PersonWeekGrid { get; } = new(); //dictionary to track projects a person has each week
        private readonly Dictionary<Project, Window> _window; //considers start and end dates to know what valid moves are 
        private readonly Dictionary<Project, int> _shift; //current shift of project
        public ScheduleState(List<Person> people, List<Project> projects)
        {
            People = people;
            Projects = projects;
            _window = new Dictionary<Project, Window>();
            foreach (var proj in projects)
            {
                _window[proj] = new Window(proj.startDate, proj.endDate);
            }

            _shift = new Dictionary<Project, int>();
            foreach (var proj in projects)
            {
                _shift[proj] = 0;   // all projects start with shift 0
            }

            RebuildGrid();
        }
        //duration should be from start date week +1 to end date week -1 but need to check my maths on this one on paper
        public int GetDuration(Project p)
        {
            var weeks = p.people
                .SelectMany(person => person.projects[p]) //weeks on proj for this person
                .Distinct() //remove duplicate weeks ie 2 people working week 10
                .ToList();

            int leftmost = weeks.Min();
            int rightmost = weeks.Max();
            int duration = rightmost - leftmost + 1;

            return duration;
        }
        public int GetShift(Project p)
        {
            return _shift[p];
        }
        public void SetShift(Project p, int shift)
        {
            _shift[p] = shift;
        }

        //finds all valid shifts for each project 
        public List<int> GetValidShifts(Project p)
        {
            var weeks = p.people //all work weeks on project
                .SelectMany(person => person.projects[p])  //returns weeks a person works
                .Distinct()  //removes duplicates aka 2 people workinf
                .ToList();

            if (weeks.Count == 0)
            {
                return new List<int> { 0 }; //breaks without this do not remove
            }

            int baselineStart = weeks.Min();   //earliest x
            int baselineEnd = weeks.Max();     //latest x

            int duration = baselineEnd - baselineStart + 1; //how wide the project is or long

            int earliestStart = _window[p].Start + 1;  //valid moves left
            int latestEnd = _window[p].End - 1;        //valid moves right


            int minShift = Math.Max(-baselineStart + 1, earliestStart - baselineStart); //how far left can it go
            int maxShift = Math.Min(52 - baselineEnd, latestEnd - baselineEnd); // how far right

            if (maxShift < minShift)
            {
                return new List<int>();  //gave an error on some if constraints didn't allow movement
            }

            return Enumerable.Range(minShift, maxShift - minShift + 1).ToList(); //list of valid shfts
        }
        //This is what actually moves the weeks. It takes a project and shift and moves ever person's weeks by the shift #
        public List<WeekKey> GetGrid(Project p, int shift)  //basically the view for the project and weeks like our excel sheets
        {
            List<WeekKey> cells = new List<WeekKey>();   //holds occupied cells
            foreach (var person in p.people) //each person on a proj
            {
                if (person.projects.ContainsKey(p))
                {
                    foreach (var originalWeek in person.projects[p])   //takes each week someone is on a project
                    {
                        int shiftedWeek = originalWeek + shift;  //makes the shift change
                        if (shiftedWeek >= 1 && shiftedWeek <= 52)
                        {
                            cells.Add(new WeekKey(person.id, shiftedWeek)); //adds the new changed cell to list
                        }
                    }
                }
            }
            return cells;
        }

        //used at beginning to build the grid and add projects to it
        public void RebuildGrid()
        {
            PersonWeekGrid.Clear();  //clears the grid viee
            foreach (var p in Projects)
            {
                AddProjectToGrid(p);

            }
        }
        //updates the grid with shifts
        public void ApplyShift(Project p, int shift)
        {
            RemoveProjectFromGrid(p);  //removes old spot
            SetShift(p, shift);         //takes the new one post shift
            AddProjectToGrid(p);    //adds the proj back
        }
        //removes a project from the grid when it is being shifted
        private void RemoveProjectFromGrid(Project p)
        {
            int shift = GetShift(p);   //current cell shift
            foreach (var key in GetGrid(p, shift))
            {
                int week = key.Week;
                if (week < 1 || week > Weeks) continue;
                if (!PersonWeekGrid.TryGetValue(key, out var count)) continue;
                if (--count == 0) PersonWeekGrid.Remove(key);
                else PersonWeekGrid[key] = count;
            }
        }
        //current shift added 
        private void AddProjectToGrid(Project p)
        {
            int shift = GetShift(p);
            foreach (var key in GetGrid(p, shift))  //loops over occupied cells
            {
                int week = key.Week;
                if (week < 1 || week > Weeks) continue;
                PersonWeekGrid[key] = PersonWeekGrid.GetValueOrDefault(key) + 1;
            }
        }
    }
}