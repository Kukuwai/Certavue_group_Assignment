using OllamaSharp;
using OllamaSharp.Models.Chat;
using OllamaSpike;
using System.Diagnostics;

Console.WriteLine(" Testing Schedule Explanations\n");
Console.WriteLine("*" + new string('*', 50) + "\n");

var ollama = new OllamaApiClient(new Uri("http://localhost:11434"))
{
  SelectedModel = "llama3.2:3b"
};

// Test all scenarios. Here we are testing all five different scenarios that we can expect.
await RunScenario("Scenario 1: Hours Reduced", TestScenarios.HoursReduced());
await RunScenario("Scenario 2: Hours Increased", TestScenarios.HoursIncreased());
await RunScenario("Scenario 3: Person Removed", TestScenarios.PersonRemoved());
await RunScenario("Scenario 4: Person Added", TestScenarios.PersonAdded());
await RunScenario("Scenario 5: Multiple Changes", TestScenarios.MultipleChanges());

Console.WriteLine("\n All scenarios tested!");

// This method is to run each scenario
async Task RunScenario(string title, string prompt)
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
