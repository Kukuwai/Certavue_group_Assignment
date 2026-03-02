using System.Linq;
using System.Collections.Generic;
using MoreLinq; // Requires the MoreLINQ package

//Since we designed it without a schedule class in the loader we keep track here making the "grid" in the CSV form
public class ScheduleState
{
    // Identifies a single grid cell by person, project, and week.
    // Used in PersonWeekGrid to track hours per person/project/week.
    public struct WeekKey //handles a person and week cell
    {
        public int PersonId;
        public int ProjectId;
        public int Week;

        public WeekKey(int personId, int projectId, int week)
        {
            PersonId = personId;
            ProjectId = projectId;
            Week = week;
        }
    }
    // Identifies a person and week without a specific project.
    // Used in PersonWeekHours to track total hours per person per week for overload detection.
    public struct PersonWeekKey
    {
        public int PersonId;
        public int Week;

        public PersonWeekKey(int personId, int week)
        {
            PersonId = personId;
            Week = week;
        }
    }

    // Defines the allowed time boundary for a project.
    // Work can only be scheduled between Start + 1 and End - 1.
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

    private const int Weeks = 52;
    public List<Person> People { get; } //list of all people in that schedule
    public List<Project> Projects { get; } //list of projects

    public Dictionary<WeekKey, int> PersonWeekGrid { get; } = new();   //dictionary to track projects a person has each week and weekly hours on them
    public Dictionary<PersonWeekKey, int> PersonWeekHours { get; } = new(); //Will be used to store sum of hours per person per week

    private readonly Dictionary<Project, Window> window; //considers start and end dates to know what valid moves are 
    private readonly Dictionary<Project, int> shift; //current shift of project
    private readonly Dictionary<Project, List<int>> orderedProjectWeeks = new Dictionary<Project, List<int>>(); //using to cache the order of projects so week 3 doesn't go before week 2 for example

    // Input: A list of all people and projects to schedule.
    //
    // Logic: Initialises time windows and shifts for every project,
    //        caches their week order, then builds the grid from scratch.
    //
    // Purpose: The single entry point to create a working schedule state.
    //          Everything else in the class depends on this being run first.
    //
    // Output: A fully initialised ScheduleState ready for shifts and optimisation.
    public ScheduleState(List<Person> people, List<Project> projects)
    {
        People = people;
        Projects = projects;
        window = new Dictionary<Project, Window>();//valid moves for each project
        foreach (var proj in projects)
        {
            window[proj] = new Window(proj.startDate, proj.endDate); //saves the start and end date to verify valid moves
        }

        shift = new Dictionary<Project, int>(); //shift tracking per project
        foreach (var proj in projects)
        {
            shift[proj] = 0;   // all projects start with shift 0
            CacheOrderedProjectWeeks(proj); //makes sure projects stay in order
        }

        RebuildGrid();
    }
    //duration should be from start date week +1 to end date week -1 but need to check my maths on this one on paper
    public int GetDuration(Project p)
    {
        return p.durationProjectFinder();
    }
    public int GetShift(Project p)
    {
        return shift[p];
    }
    public void SetShift(Project p, int shiftToDo)
    {
        shift[p] = shiftToDo; //saves shift value so when it is rebuilt that project goes to the right cell
    }

    public void AddProject(Project p)  //added this to fix it @Luca your project wasn't stored anywhere 
    {
        if (Projects.Contains(p)) return;

        Projects.Add(p);
        window[p] = new Window(p.startDate, p.endDate);
        shift[p] = 0;
        CacheOrderedProjectWeeks(p);
        AddProjectToGrid(p);
    }


    // Input: A project to evaluate.
    //
    // Logic: Collects all weeks where any person on the project has assigned hours.
    //        Finds the first and last week, then calculates how far left or right
    //        the project can slide while staying inside both the schedule (weeks 1-52)
    //        and the project's allowed time window (Start + 1 to End - 1).
    //
    // Purpose: Tells the greedy algorithm and handler every legal position
    //          a project can be shifted to before attempting any move.
    //
    // Output: List of valid integer shifts. Returns { 0 } if no work is assigned yet,
    //         empty list if no moves are possible.
    public List<int> GetValidShifts(Project p)
    {
        List<int> assignedWeeks = new List<int>(); //every week work is assigned

        foreach (Person person in p.people)
        {
            if (!person.projects.ContainsKey(p))
            {
                continue; //Missing a person/week
            }

            foreach (int week in person.projects[p].Keys)
            {
                if (!assignedWeeks.Contains(week))
                {
                    assignedWeeks.Add(week); //Only unique weeks 
                }
            }
        }

        if (assignedWeeks.Count == 0)
        {
            return new List<int> { 0 }; //No assignment yet so stay
        }

        assignedWeeks.Sort();
        int firstWeek = assignedWeeks[0];
        int lastWeek = assignedWeeks[assignedWeeks.Count - 1];

        int earliestAllowedWeek = window[p].Start + 1; //allowed moves left and right
        int latestAllowedWeek = window[p].End - 1;

        int minShift = Math.Max(1 - firstWeek, earliestAllowedWeek - firstWeek); //smallest and largest allowed shifts
        int maxShift = Math.Min(Weeks - lastWeek, latestAllowedWeek - lastWeek);

        if (minShift > maxShift)
        {
            return new List<int>(); //No moves possible
        }

        return Enumerable.Range(minShift, maxShift - minShift + 1).ToList();
    }

    // Input: A project and a shift value to apply for the preview.
    //
    // Logic: Loops through every person on the project and every week they are assigned,
    //        adds the shift to each week number, and collects the resulting cells.
    //        Skips any cells that fall outside weeks 1-52.
    //
    // Purpose: Read-only preview of where a project's cells would land at a given shift.
    //          Used by the greedy to find which projects occupy an overloaded cell,
    //          and by the handler to calculate role saturation.
    //
    // Output: List of WeekKey (person + project + week). Does not include hours,
    //         does not change any data.
    public List<WeekKey> GetGrid(Project p, int shift)  //basically the view for the project and weeks like our excel sheets
    {
        List<WeekKey> cells = new List<WeekKey>();   //holds occupied cells
        foreach (var person in p.people) //each person on a proj
        {
            if (person.projects.ContainsKey(p))
            {
                foreach (var originalWeek in person.projects[p])   //takes each week someone is on a project
                {
                    int shiftedWeek = originalWeek.Key + shift;  //makes the shift change
                    if (shiftedWeek >= 1 && shiftedWeek <= 52)
                    {
                        cells.Add(new WeekKey(person.id, p.id, shiftedWeek)); //adds the new changed cell to list
                    }
                }
            }
        }
        return cells;
    }
    public struct GridCellHours //used for one shifted cell and the exact hours assigned
    {
        public WeekKey Key; //locates person/project/week
        public int Hours; //stores hours for a cell

        public GridCellHours(WeekKey key, int hours)
        {
            Key = key;
            Hours = hours;
        }
    }
    // Same as GetGrid but includes hours for each cell.
    // Skips cells outside weeks 1-52 and any entries with zero or negative hours.
    // Used by AddProjectToGrid and RemoveProjectFromGrid to update hour totals.
    public List<GridCellHours> GetGridWithHours(Project p, int shift) //builds the grid with the shifted hours
    {
        List<GridCellHours> cells = new List<GridCellHours>(); //stores all valid shifts for a project

        foreach (Person person in p.people) //loop on each person on a project
        {
            if (!person.projects.ContainsKey(p)) //breaks without this please do not change I do not know why
            {
                continue;
            }

            foreach (KeyValuePair<int, int> originalWeek in person.projects[p]) //week hours for a person
            {
                int shiftedWeek = originalWeek.Key + shift; //aplies the shift
                int hours = originalWeek.Value; //find hours for that week

                if (shiftedWeek < 1 || shiftedWeek > 52) //keeps in the schedule
                {
                    continue;
                }

                if (hours <= 0) //this shouldn't happen when done but while testing would randomly drop below 0 so this is a temp fix
                {
                    continue;
                }

                WeekKey key = new WeekKey(person.id, p.id, shiftedWeek); //stores shifted assignments
                cells.Add(new GridCellHours(key, hours)); //used later to adjust hours total
            }
        }

        return cells;
    }


    // Input: None. Operates on the current state of all projects and their shifts.
    //
    // Logic: Clears both PersonWeekGrid and PersonWeekHours completely,
    //        then loops through every project and adds it to the grid
    //        using its current shift value.
    //
    // Purpose: Full reset and resync of the grid. Called after week moves,
    //          splits, and role reassigns where multiple projects may be affected
    //          and incremental updates would be unreliable.
    //
    // Output: None. Directly rebuilds both grid dictionaries from scratch.
    public void RebuildGrid() //has to clear 2 dictionaries now
    {
        PersonWeekGrid.Clear(); //person/proj/week grid cleared
        PersonWeekHours.Clear(); //person/week cleared

        foreach (Project p in Projects)
        {
            AddProjectToGrid(p); //adds the project back with shift
        }
    }

    // Input: A project and the new shift value to apply.
    //
    // Logic: Removes the project from its current grid position by subtracting
    //        its hours from both dictionaries, saves the new shift value,
    //        then re-adds the project at the new position.
    //
    // Purpose: The main way the greedy and handler commit a project move.
    //          Keeps PersonWeekGrid and PersonWeekHours accurate after every shift.
    //
    // Output: None. Directly updates the grid state.
    public void ApplyShift(Project p, int shift)
    {
        RemoveProjectFromGrid(p);  //removes old spot
        SetShift(p, shift);         //takes the new one post shift
        AddProjectToGrid(p);    //adds the proj back
    }
    // Represents a person/week that is over capacity.
    // Used by the greedy to rank and target the worst overloaded cells first.
    public struct OverloadCell //a person and week combo over 40 hours or capacity
    {
        public int PersonId;
        public int Week;
        public int AssignedHours; //sum for the week
        public int Capacity; //should always be 40 right now

        public int GetOverload() //calcs how many hours over 40 someone is
        {
            if (AssignedHours > Capacity)
            {
                return AssignedHours - Capacity;
            }

            return 0;
        }
    }
    // Finds the single most overloaded person/week in the schedule.
    // Returns true if an overload exists, with full details in the out parameter.
    // Returns false if no one is over capacity.
    public bool TryGetWorstOverloadCell(out OverloadCell worst)
    {
        worst = default; //2nd output used
        int worstOverload = 0; //Tracks largest overworked

        Dictionary<int, int> capacityByPersonId = new Dictionary<int, int>(People.Count); //person id to capacity lookup
        foreach (Person p in People)
        {
            if (p.capacity > 0) //Should be good for everyone
            {
                capacityByPersonId[p.id] = p.capacity; //Stores capacity for this person
            }
            else
            {
                capacityByPersonId[p.id] = 40; //Uses default 40. Right now sometimes without this it fails, should be ok later though once all together
            }
        }

        foreach (KeyValuePair<PersonWeekKey, int> kv in PersonWeekHours) //Every person/week
        {
            if (!capacityByPersonId.TryGetValue(kv.Key.PersonId, out int capacity)) //Skips anyone missing 
            {
                continue;
            }

            if (kv.Value <= capacity) //40 hours or less cannot be overworked
            {
                continue;
            }

            int overload = kv.Value - capacity; //How many hours over?
            if (overload <= worstOverload) //Skips if not worse than worst
            {
                continue;
            }

            worstOverload = overload; //New worst
            worst = new OverloadCell //Full details for use later
            {
                PersonId = kv.Key.PersonId,
                Week = kv.Key.Week,
                AssignedHours = kv.Value,
                Capacity = capacity
            };
        }

        return worstOverload > 0; //only returns if one over loaded person found
    }


    // Removes a project from both grid dictionaries using its current shift.
    // Subtracts hours cell by cell from PersonWeekGrid and PersonWeekHours.
    // Deletes entries entirely if they reach zero. Used by ApplyShift. 
    private void RemoveProjectFromGrid(Project p)
    {
        int shift = GetShift(p); //reads current shift to get right cell

        foreach (GridCellHours cell in GetGridWithHours(p, shift)) //iterates all shofted cells for a project
        {
            WeekKey key = cell.Key;
            int hours = cell.Hours;

            if (key.Week < 1 || key.Week > Weeks) //in bounds?
            {
                continue;
            }

            int current; //temp holder for current hours in a cell
            if (PersonWeekGrid.TryGetValue(key, out current)) //Proceeds only if cell exists
            {
                int next = current - hours; //remaining hours after removing projects hours
                if (next <= 0) //if no hours left remove 
                {
                    PersonWeekGrid.Remove(key); //deletes empty cell
                }
                else
                {
                    PersonWeekGrid[key] = next; //stoes cells new hours
                }
            }

            PersonWeekKey pwKey = new PersonWeekKey(key.PersonId, key.Week); //key for person/weeks
            int total; //holder for aggregated person week hours
            if (PersonWeekHours.TryGetValue(pwKey, out total)) //proceeds only if it exists
            {
                int nextTotal = total - hours; //Total after subtraction
                if (nextTotal <= 0) //Removes emtpy entires
                {
                    PersonWeekHours.Remove(pwKey);
                }
                else
                {
                    PersonWeekHours[pwKey] = nextTotal; //stores reduced person/week total
                }
            }
        }
    }
    // Adds a project to both grid dictionaries using its current shift.
    // Adds hours cell by cell into PersonWeekGrid and PersonWeekHours.
    // Creates new entries if they don't exist yet.
    private void AddProjectToGrid(Project p)
    {
        int shift = GetShift(p); //current shift of proj

        foreach (GridCellHours cell in GetGridWithHours(p, shift)) //iterates each shift
        {
            WeekKey key = cell.Key;
            int hours = cell.Hours;

            if (key.Week < 1 || key.Week > Weeks) //in bounds check
            {
                continue;
            }

            int existing = 0; //makes detailed cell value when no key found
            PersonWeekGrid.TryGetValue(key, out existing); //returns detailed value if present
            PersonWeekGrid[key] = existing + hours; //adds hours into person/proj/week dictionary

            PersonWeekKey pwKey = new PersonWeekKey(key.PersonId, key.Week); //person/week totals updated
            int total = 0; //sum of total hours 
            PersonWeekHours.TryGetValue(pwKey, out total); //
            PersonWeekHours[pwKey] = total + hours;
        }
    }
    // Builds and caches the sorted week sequence for a project.
    // Ensures earlier weeks cannot jump past later ones during moves.
    // Refreshed before every order check in PreservesProjectWeekOrder.
    private void CacheOrderedProjectWeeks(Project p) //projs original week sequence 
    {
        List<int> weeks = new List<int>(); //stores distinct weeks

        foreach (Person person in p.people) //each person on project
        {
            Dictionary<int, int> weekHours; //holds week/hours for person
            if (!person.projects.TryGetValue(p, out weekHours)) //skips someone not working
            {
                continue;
            }

            foreach (int week in weekHours.Keys) //makes sure no dup weeks and adds to list
            {
                if (!weeks.Contains(week))
                {
                    weeks.Add(week);
                }
            }
        }

        weeks.Sort(); //sorts so stays in order
        orderedProjectWeeks[p] = weeks; //saves for later checks
    }
    // Checks if moving a week to a target position keeps the project's week sequence intact.
    // Ensures the target week stays between its neighbouring weeks on both sides.
    // Returns true if the move is valid, false if it would break the order.
    public bool PreservesProjectWeekOrder(Project p, int sourceWeek, int targetWeek)
    {
        CacheOrderedProjectWeeks(p); //Refresh ordered weeks from current assignments so checks are not old

        if (!orderedProjectWeeks.ContainsKey(p)) //No order data means no restriction to enforce
        {
            return true;
        }

        List<int> ordered = orderedProjectWeeks[p];
        int index = ordered.IndexOf(sourceWeek); //Position of week being moved

        if (index < 0)
        {
            return true;
        }

        int leftBound = 1;  //lower bound.
        int rightBound = 52; //upper bound.

        if (index > 0) // If there is a prior week, target week can't be before it
        {
            leftBound = ordered[index - 1] + 1;
        }

        if (index < ordered.Count - 1) //If there is a next week, target cannot jump it
        {
            rightBound = ordered[index + 1] - 1;
        }

        if (targetWeek < leftBound) //Past left
        {
            return false;
        }

        if (targetWeek > rightBound) //Past right
        {
            return false;
        }

        return true;
    }


    // Grab all original task data for a project that can feed it into the OR-Tools solver.
    public List<(int PersonId, int Week, int Hours)> GetOriginalAssignments(Project p)
    {
        var assignments = new List<(int PersonId, int Week, int Hours)>();
        foreach (var person in p.people) // loop all workers
        {
            if (person.projects.TryGetValue(p, out var weeks))
            {
                foreach (var kvp in weeks)
                {
                    // kvp.Key is origin week，kvp.Value persent origin hours
                    assignments.Add((person.id, kvp.Key, kvp.Value));
                }
            }
        }
        return assignments;
    }

    // a heavy Lifter: Re maping the entire schedule based on the solver's optimized output.
    // 修改方法签名，使其支持 4 个参数的元组 (PersonId, Project, RawWeek, TaskIdx)
    public void UpdateFromFineGrainedAssignments(
        Dictionary<(int PersonId, Project Project, int RawWeek, int TaskIdx), int> newAssignments,
        Dictionary<int, List<(int PersonId, int Week, int Hours)>> originalTaskMap)
    {
        // 1. 清理：必须彻底清理 prj.people 和 person.projects
        var affectedProjects = newAssignments.Keys.Select(k => k.Project).Distinct().ToList();
        foreach (var prj in affectedProjects)
        {
            foreach (var person in People) person.projects.Remove(prj);
            prj.people.Clear();
        }

        // 2. 映射：只还原基础的 Person -> Project 关系
        foreach (var entry in newAssignments)
        {
            var (personId, prjRef, _, tIdx) = entry.Key;
            int targetWeek = entry.Value; // 求解器给出的新周

            if (originalTaskMap.TryGetValue(prjRef.id, out var projectTasks) && tIdx < projectTasks.Count)
            {
                var originalTask = projectTasks[tIdx];
                var person = People.First(p => p.id == personId);
                var actualPrj = this.Projects.First(p => p.id == prjRef.id);

                // 建立基础关联
                if (!person.projects.ContainsKey(actualPrj))
                    person.projects[actualPrj] = new Dictionary<int, int>();

                // 累加工时（同一人周可能有多个 Task）
                int current = person.projects[actualPrj].GetValueOrDefault(targetWeek, 0);
                person.projects[actualPrj][targetWeek] = current + originalTask.Hours;

                if (!actualPrj.people.Contains(person)) actualPrj.people.Add(person);
            }
        }

        // 3. 同步：依靠 RebuildGrid 一次性生成 PersonWeekHours
        // 这样能保证 Stats 里的总工时和 Person.projects 里的完全守恒
        RebuildGrid();
    }

    // Low-level helper to actually write the data into the dictionaries and update the heat-map grid.
    private void ApplySingleAssignment(int personId, Project prj, int week, int hours)
    {
        var person = People.First(p => p.id == personId);

        // Link the task to the person.
        if (!person.projects.ContainsKey(prj)) person.projects[prj] = new Dictionary<int, int>();
        person.projects[prj][week] = hours;

        // Update the scoring grid keys.
        var wk = new WeekKey(personId, prj.id, week);
        PersonWeekGrid[wk] = PersonWeekGrid.GetValueOrDefault(wk) + hours;

        var pwk = new PersonWeekKey(personId, week);
        PersonWeekHours[pwk] = PersonWeekHours.GetValueOrDefault(pwk) + hours;
    }

    public void SwapPersonInProject(Project p, Person oldPerson, Person newPerson)
    {
        RemoveProjectFromGrid(p);

        p.ReplaceStaff(oldPerson, newPerson);
        AddProjectToGrid(p);
    }
}