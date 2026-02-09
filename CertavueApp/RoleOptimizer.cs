using System;
using System.Collections.Generic;
using System.Linq;

public class RoleOptimizer
{
    public OptimizationResult Optimize(ScheduleState state, int maxPasses = 99999999)
    {
        var handler = new ScheduleHandler(state);
        double currentFitness = handler.CalculateFitnessScore(state);

        bool improvedAny = false;
        int weeksImproved = 0;
        int combinationsChecked = 0;

        for (int pass = 0; pass < maxPasses; pass++)
        {

        }

   

}
private class ConflictTask
{
    public int Week { get; set; }
    public Person OverloadedPerson { get; set; }
    public Project Project { get; set; }
}

private class WeekOptimizationResult
{
    public bool Improved { get; set; }
    public double BestFitness { get; set; }
    public int CombinationsChecked { get; set; }
}

private class AssignmentSnapshot
{
    public Dictionary<Person, Dictionary<Project, List<int>>> PersonAssignments { get; set; }
    public Dictionary<Project, HashSet<Person>> ProjectPeople { get; set; }
}

public class OptimizationResult
{
    public bool Improved { get; set; }
    public double FinalFitness { get; set; }
    public int WeeksImproved { get; set; }
    public int CombinationsChecked { get; set; }
}


}