using OllamaSharp;
using OllamaSharp.Models.Chat;
using OllamaSpike;
using System.Diagnostics;

Console.WriteLine(" Testing Schedule Explanations\n");
Console.WriteLine("*" + new string('*', 50) + "\n");

var ollama = new OllamaApiClient(new Uri("http://localhost:11434"))
{
  SelectedModel = "llama3.2:3b"
  //SelectedModel = "llama3.2:1b"
};


// Test all scenarios. Here we are testing all five different scenarios that we can expect.
await TestScenarios.RunScenario("Scenario 1: Hours Reduced", TestScenarios.HoursReduced(), ollama);
await TestScenarios.RunScenario("Scenario 2: Hours Increased", TestScenarios.HoursIncreased(), ollama);
await TestScenarios.RunScenario("Scenario 3: Person Removed", TestScenarios.PersonRemoved(), ollama);
await TestScenarios.RunScenario("Scenario 4: Person Added", TestScenarios.PersonAdded(), ollama);
await TestScenarios.RunScenario("Scenario 5: Multiple Changes", TestScenarios.MultipleChanges(), ollama);

Console.WriteLine("\n All scenarios tested!");

// Test real scenario
// await RealScenarioTest.TestFindPeopleExplanation(ollama);

// CSV tests;
Console.WriteLine("\n REAL CSV FORMAT TEST:\n");
// await CSVScheduleTest.TestScheduleOptimizationExplanation(ollama);

// Test Greedy + Ollama Explanation
await GreedyExplanationTest.TestGreedyWithExplanation(ollama);

// LangChain Test
// Console.WriteLine("\n" + new string('=', 60));
// Console.WriteLine("LANGCHAIN INTEGRATION TEST:");
// Console.WriteLine(new string('=', 60) + "\n");
// await LangChainTest.RunBasicTest();



