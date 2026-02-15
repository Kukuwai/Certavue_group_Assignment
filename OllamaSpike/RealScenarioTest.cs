using OllamaSharp;
using OllamaSharp.Models.Chat;

namespace OllamaSpike;

public class Project
{
  public string Name { get; set; }
  public int RequiredHours { get; set; }
}

public class Person
{
  public string Name { get; set; }
  public int AvailableHours { get; set; }
}

public class RealScenarioTest
{
  public static async Task TestFindPeopleExplanation(OllamaApiClient ollama)
  {
    Console.WriteLine("Real Scenario: Explain FindPeopleForNewProject");
    Console.WriteLine(new string('*', 50));

    // Simulate  actual data
    var project = new Project { Name = "Web Redesign", RequiredHours = 30 };

    var allPeople = new List<Person>
        {
            new Person { Name = "Alice", AvailableHours = 40 },
            new Person { Name = "Bob", AvailableHours = 35 },
            new Person { Name = "Charlie", AvailableHours = 15 }, // Not enough
            new Person { Name = "Diana", AvailableHours = 50 }
        };

    // Simulate FindPeopleForNewProject logic
    var selectedPeople = allPeople
        .Where(p => p.AvailableHours >= project.RequiredHours)
        .ToList();

    var rejectedPeople = allPeople
        .Where(p => p.AvailableHours < project.RequiredHours)
        .ToList();

    // To Build prompt, we need everything in text (String).
    string selectedNames = string.Join(", ", selectedPeople.Select(p => $"{p.Name} ({p.AvailableHours}h)"));
    string rejectedNames = string.Join(", ", rejectedPeople.Select(p => $"{p.Name} ({p.AvailableHours}h)"));
    Console.WriteLine(selectedNames);
    Console.WriteLine(rejectedNames);

    string prompt = $@"
You are a project scheduling assistant. Explain this selection in 2-3 sentences:

Project: {project.Name}
Required Hours: {project.RequiredHours} hours/week

SELECTED ({selectedPeople.Count} people):
{selectedNames}

NOT SELECTED ({rejectedPeople.Count} people):
{rejectedNames}

Explain why these people were selected and others were not.
";

    Console.WriteLine(prompt);

    // Get explanation from Ollama
    try
    {
      var chat = new Chat(ollama);
      string response = "";

      await foreach (var token in chat.SendAsync(prompt))
      {
        response += token;
      }

      Console.WriteLine($"Explanation:\n{response}");
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error: {ex.Message}");
    }

    Console.WriteLine();
  }
}