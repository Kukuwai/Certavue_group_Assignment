using System.Linq;
using System.Collections.Generic;
using MoreLinq; // Requires the MoreLINQ package

//Since we designed it without a schedule class in the loader we keep track here making the "grid" in the CSV form
public class ScheduleState
{
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


    //finds all valid shifts for each project 
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


    //used at beginning to build the grid and add projects to it
    public void RebuildGrid() //has to clear 2 dictionaries now
    {
        PersonWeekGrid.Clear(); //person/proj/week grid cleared
        PersonWeekHours.Clear(); //person/week cleared

        foreach (Project p in Projects)
        {
            AddProjectToGrid(p); //adds the project back with shift
        }
    }

    //updates the grid with shifts
    public void ApplyShift(Project p, int shift)
    {
        RemoveProjectFromGrid(p);  //removes old spot
        SetShift(p, shift);         //takes the new one post shift
        AddProjectToGrid(p);    //adds the proj back
    }
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

    //removes a project from the grid when it is being shifted
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





    public void SwapPersonInProject(Project p, Person oldPerson, Person newPerson)
    {
        RemoveProjectFromGrid(p);

        p.ReplaceStaff(oldPerson, newPerson);
        AddProjectToGrid(p);
    }
}