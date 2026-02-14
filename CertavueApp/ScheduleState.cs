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
    private readonly Dictionary<Project, List<int>> orderedProjectWeeks = new Dictionary<Project, List<int>>();


    public ScheduleState(List<Person> people, List<Project> projects)
    {
        People = people;
        Projects = projects;
        window = new Dictionary<Project, Window>();
        foreach (var proj in projects)
        {
            window[proj] = new Window(proj.startDate, proj.endDate);
        }

        shift = new Dictionary<Project, int>();
        foreach (var proj in projects)
        {
            shift[proj] = 0;   // all projects start with shift 0
            CacheOrderedProjectWeeks(proj);
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
        shift[p] = shiftToDo;
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
        var weeks = p.people //all work weeks on project
            .SelectMany(person => person.projects[p])  //returns weeks a person works
            .Distinct()  //removes duplicates aka 2 people workinf
            .ToList();

        if (weeks.Count == 0)
        {
            return new List<int> { 0 }; //breaks without this do not remove
        }
        int baselineStart = weeks[0].Key;
        int baselineEnd = weeks[0].Key;
        foreach (KeyValuePair<int, int> entry in weeks)
        {
            if (entry.Key < baselineStart) //earliest x
            {
                baselineStart = entry.Key;
            }
            if (entry.Key > baselineEnd) //latest x
            {
                baselineEnd = entry.Key;
            }
        }

        int duration = baselineEnd - baselineStart + 1; //how wide the project is or long

        int earliestStart = window[p].Start + 1;  //valid moves left
        int latestEnd = window[p].End - 1;        //valid moves right


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
    public struct GridCellHours
    {
        public WeekKey Key;
        public int Hours;

        public GridCellHours(WeekKey key, int hours)
        {
            Key = key;
            Hours = hours;
        }
    }

    public List<GridCellHours> GetGridWithHours(Project p, int shift)
    {
        List<GridCellHours> cells = new List<GridCellHours>();

        foreach (Person person in p.people)
        {
            if (!person.projects.ContainsKey(p))
            {
                continue;
            }

            foreach (KeyValuePair<int, int> originalWeek in person.projects[p])
            {
                int shiftedWeek = originalWeek.Key + shift;
                int hours = originalWeek.Value;

                if (shiftedWeek < 1 || shiftedWeek > 52)
                {
                    continue;
                }

                if (hours <= 0)
                {
                    continue;
                }

                WeekKey key = new WeekKey(person.id, p.id, shiftedWeek);
                cells.Add(new GridCellHours(key, hours));
            }
        }

        return cells;
    }


    //used at beginning to build the grid and add projects to it
    public void RebuildGrid() //has to clear 2 dictionaries now
    {
        PersonWeekGrid.Clear();
        PersonWeekHours.Clear();

        foreach (Project p in Projects)
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
    public struct OverloadCell
    {
        public int PersonId;
        public int Week;
        public int AssignedHours;
        public int Capacity;

        public int GetOverload()
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
        worst = new OverloadCell();
        bool found = false;

        foreach (KeyValuePair<PersonWeekKey, int> kv in PersonWeekHours)
        {
            Person person = null;

            foreach (Person p in People)
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

            int cap = 40;
            if (person.capacity > 0)
            {
                cap = person.capacity;
            }

            OverloadCell cell = new OverloadCell();
            cell.PersonId = kv.Key.PersonId;
            cell.Week = kv.Key.Week;
            cell.AssignedHours = kv.Value;
            cell.Capacity = cap;

            if (cell.GetOverload() <= 0)
            {
                continue;
            }

            if (!found || cell.GetOverload() > worst.GetOverload())
            {
                worst = cell;
                found = true;
            }
        }

        return found;
    }


    //removes a project from the grid when it is being shifted
    private void RemoveProjectFromGrid(Project p)
    {
        int shift = GetShift(p);

        foreach (GridCellHours cell in GetGridWithHours(p, shift))
        {
            WeekKey key = cell.Key;
            int hours = cell.Hours;

            if (key.Week < 1 || key.Week > Weeks)
            {
                continue;
            }

            int current;
            if (PersonWeekGrid.TryGetValue(key, out current))
            {
                int next = current - hours;
                if (next <= 0)
                {
                    PersonWeekGrid.Remove(key);
                }
                else
                {
                    PersonWeekGrid[key] = next;
                }
            }

            PersonWeekKey pwKey = new PersonWeekKey(key.PersonId, key.Week);
            int total;
            if (PersonWeekHours.TryGetValue(pwKey, out total))
            {
                int nextTotal = total - hours;
                if (nextTotal <= 0)
                {
                    PersonWeekHours.Remove(pwKey);
                }
                else
                {
                    PersonWeekHours[pwKey] = nextTotal;
                }
            }
        }
    }

    private void AddProjectToGrid(Project p)
    {
        int shift = GetShift(p);

        foreach (GridCellHours cell in GetGridWithHours(p, shift))
        {
            WeekKey key = cell.Key;
            int hours = cell.Hours;

            if (key.Week < 1 || key.Week > Weeks)
            {
                continue;
            }

            int existing = 0;
            PersonWeekGrid.TryGetValue(key, out existing);
            PersonWeekGrid[key] = existing + hours;

            PersonWeekKey pwKey = new PersonWeekKey(key.PersonId, key.Week);
            int total = 0;
            PersonWeekHours.TryGetValue(pwKey, out total);
            PersonWeekHours[pwKey] = total + hours;
        }
    }

    private void CacheOrderedProjectWeeks(Project p)
    {
        List<int> weeks = new List<int>();

        foreach (Person person in p.people)
        {
            Dictionary<int, int> weekHours;
            if (!person.projects.TryGetValue(p, out weekHours))
            {
                continue;
            }

            foreach (int week in weekHours.Keys)
            {
                if (!weeks.Contains(week))
                {
                    weeks.Add(week);
                }
            }
        }

        weeks.Sort();
        orderedProjectWeeks[p] = weeks;
    }

    public bool PreservesProjectWeekOrder(Project p, int sourceWeek, int targetWeek)
    {
        if (!orderedProjectWeeks.ContainsKey(p))
        {
            return true;
        }

        List<int> ordered = orderedProjectWeeks[p];
        int index = ordered.IndexOf(sourceWeek);

        if (index < 0)
        {
            return true;
        }

        int leftBound = 1;
        int rightBound = 52;

        if (index > 0)
        {
            leftBound = ordered[index - 1] + 1;
        }

        if (index < ordered.Count - 1)
        {
            rightBound = ordered[index + 1] - 1;
        }

        if (targetWeek < leftBound)
        {
            return false;
        }

        if (targetWeek > rightBound)
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