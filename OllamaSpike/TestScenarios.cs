using OllamaSharp;
using OllamaSharp.Models.Chat;

using System.Diagnostics;
namespace OllamaSpike;



public class TestScenarios
{
  // Scenario 1: Hours reduced
  public static string HoursReduced()
  {
    return @"
You are a schedule assistant. Explain in 1-2 sentences:

Project: Website Redesign
Person: Alice
Week: 1
Changed from: 40 hours to 30 hours

Why did this happen?
";
  }

  // Scenario 2: Hours increased
  public static string HoursIncreased()
  {
    return @"
You are a schedule assistant. Explain in 1-2 sentences:

Project: Mobile App Development
Person: Bob
Week: 2
Changed from: 20 hours to 35 hours

Why did this happen?
";
  }

  // Scenario 3: Person removed from project
  public static string PersonRemoved()
  {
    return @"
You are a schedule assistant. Explain in 1-2 sentences:

Project: Database Migration
Person: Charlie
Week: 3
Changed from: 30 hours to 0 hours (removed)

Why was Charlie removed from this project?
";
  }

  // Scenario 4: New person added
  public static string PersonAdded()
  {
    return @"
You are a schedule assistant. Explain in 1-2 sentences:

Project: API Integration
Person: Diana (new)
Week: 1
Changed from: 0 hours to 25 hours (added)

Why was Diana added to this project?
";
  }

  // Scenario 5: Multiple people, multiple changes
  public static string MultipleChanges()
  {
    return @"
You are a schedule assistant. Explain what happened:

Project: Cloud Migration
Week: 4

Changes:
- Alice: 40 hours → 30 hours (reduced)
- Bob: 20 hours → 0 hours (removed)
- Charlie: 0 hours → 40 hours (added)

Summarize these changes in 2-3 sentences.
";
  }

  // This method is to run each scenario
  public static async Task RunScenario(string title, string prompt, OllamaApiClient ollama)
  {
    Console.WriteLine($" {title}");
    Console.WriteLine(new string('-', 50));

    var stopwatch = Stopwatch.StartNew();

    try
    {
      var chat = new Chat(ollama);
      string response = "";

      await foreach (var token in chat.SendAsync(prompt))
      {
        response += token;
      }

      stopwatch.Stop();

      Console.WriteLine($"Explanation: {response}");
      Console.WriteLine($"Time: {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F2}s)");
    }
    catch (Exception ex)
    {
      Console.WriteLine($" Error: {ex.Message}");
    }

    Console.WriteLine();
  }
}