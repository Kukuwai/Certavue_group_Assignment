using Xunit;

public class ProjectTests
{
  [Fact]
  public void Project_FourParameterConstructor_SetsPropertiesCorrectly()
  {
    // ACT
    var project = new Project("Project_A", 1, 10, 80);

    // ASSERT
    Assert.Equal("Project_A", project.name);
    Assert.Equal(1, project.startDate);
    Assert.Equal(10, project.endDate);
    Assert.Equal(80, project.hoursNeeded);
    Assert.True(project.id > 0);
  }

  [Fact]
  public void Project_ThreeParameterConstructor_Works()
  {
    // ACT
    var project = new Project("Project_B", 5, 15);

    // ASSERT
    Assert.Equal("Project_B", project.name);
    Assert.Equal(5, project.startDate);
    Assert.Equal(15, project.endDate);
    Assert.Equal(0, project.hoursNeeded);  // Not set in 3-param constructor
  }

  [Fact]
  public void Project_CanAddPeople()
  {
    // ARRANGE
    var project = new Project("Project_C", 1, 10, 60);
    var alice = new Person("Alice", 40, "Developer");
    var bob = new Person("Bob", 40, "QA");

    // ACT
    project.people.Add(alice);
    project.people.Add(bob);

    // ASSERT
    Assert.Equal(2, project.people.Count);
    Assert.Contains(alice, project.people);
    Assert.Contains(bob, project.people);
  }

  [Fact]
  public void Project_Id_AutoIncrements()
  {
    // ACT
    var project1 = new Project("Project_X", 1, 10, 50);
    var project2 = new Project("Project_Y", 5, 15, 60);

    // ASSERT
    Assert.True(project2.id > project1.id);
  }
}