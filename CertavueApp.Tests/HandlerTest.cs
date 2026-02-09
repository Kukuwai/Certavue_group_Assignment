using Xunit;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class ScheduleHandlerTests
{

private ScheduleHandler GetMockHandler()
{
    var person = new Person("Person_01", "Developer"); 
    
    var p1 = new Project("Project_001", 2, 10, 40) { duration = 2 }; 
    var p3 = new Project("Project_003", 3, 12, 60) { duration = 3 };
    var p4 = new Project("Project_004", 3, 11, 20) { duration = 1 };
    var p2 = new Project("Project_002", 6, 15, 80) { duration = 4 };

    var projects = new List<Project> { p1, p2, p3, p4 };
    var people = new List<Person> { person };

    foreach (var p in projects)
    {
        p.people.Add(person);

        var projectWeeks = new List<int>();
        for (int i = 0; i < p.duration; i++)
        {
            projectWeeks.Add(p.startDate + i);
        }

        if (!person.projects.ContainsKey(p))
        {
            person.projects.Add(p, projectWeeks);
        }
    }

    var state = new ScheduleState(people, projects);

    foreach (var p in projects)
    {
        state.ApplyShift(p, 0); 
    }

    return new ScheduleHandler(state);
}

[Fact]
public void Summary_ShouldDetectTripleBooking_WhenWeek3HasThreeProjects()
{
    // Arrange
    var handler = GetMockHandler();

    // Act
    var detailedConflicts = handler.GetDetailedConflictList();

    // Assert
    var week3Detail = detailedConflicts.FirstOrDefault(d => d.Contains("Week 3"));
    
    Assert.NotNull(week3Detail);
    Assert.Contains("Project_001", week3Detail);
    Assert.Contains("Project_003", week3Detail);
    Assert.Contains("Project_004", week3Detail);
}

    [Fact]
    public void EvaluateMove_ShouldReturnNegativeDelta_WhenMovingOutOfConflict()
    {
        // Arrange
    var handler = GetMockHandler();
    var state = handler.GetCurrentState();
    var p4 = state.Projects.First(p => p.name == "Project_004");

    // Act
    var score = handler.EvaluateMove(p4, 10);

    // Assert
    Assert.True(score.DeltaDoubleBooked <= 0);
    Assert.Equal(1, score.OverlapAfter); 
    
    Assert.Equal(10, score.ShiftDistance);
    }

    [Fact]
    public void GetGapsPerPerson_ShouldCorrectShowAvailableWeeks()
    {
        // Arrange
        var handler = GetMockHandler();

        // Act
        var gaps = handler.GetGapsPerPerson();

        // Assert
        Assert.True(gaps.ContainsKey("Person_01"));
        Assert.Contains("1", gaps["Person_01"]); 
    }

    [Fact]
    public void FormatWeeksIntoRanges_ShouldHandleContinuousAndDisjointWeeks()
    {
        // Arrange
        var handler = GetMockHandler();
        var testWeeks = new List<int> { 1, 2, 3, 5, 7, 8 };

        var method = handler.GetType().GetMethod("FormatWeeksIntoRanges", 
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = method!.Invoke(handler, new object[] { testWeeks }) as string;
        Assert.Equal("1-3, 5, 7-8", result);
    }

    [Fact]
public void Debug_Grid_Content()
{
    var handler = GetMockHandler();
    var state = handler.GetCurrentState();
    
    Console.WriteLine($"Grid keys count: {state.PersonWeekGrid.Count}");
    
    foreach(var kvp in state.PersonWeekGrid)
    {
        Console.WriteLine($"PersonID: {kvp.Key.PersonId}, Week: {kvp.Key.Week}, Count: {kvp.Value}");
    }
}
}