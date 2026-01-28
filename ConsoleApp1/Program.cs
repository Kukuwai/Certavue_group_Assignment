using System;
using System.Collections.Generic;
using Google.OrTools.LinearSolver;

public class Program
{
    public static void Main()
    {
        // --- Example data ---
        var people = new[] { "Alice", "Bob", "Cara" };
        var projects = new[] { "P1", "P2", "P3" };

        // Current plan x0 (hours/week)
        var x0 = new Dictionary<(string person, string project), double>
        {
            { ("Alice","P1"), 20 }, { ("Alice","P2"), 15 }, { ("Alice","P3"), 10 }, // Alice = 45
            { ("Bob","P1"),   10 }, { ("Bob","P2"),   20 }, { ("Bob","P3"),  5 },  // Bob   = 35
            { ("Cara","P1"),  15 }, { ("Cara","P2"),   0 }, { ("Cara","P3"), 20 }   // Cara  = 35
        };

        // Capacity cap (hours/week)
        var cap = new Dictionary<string, double>
        {
            { "Alice", 40 }, // over by 5 in x0
            { "Bob",   30 }, // over by 5
            { "Cara",  35 }  // ok
        };

        // Optional project demand req (hours/week). If you don't want this, set req = null
        Dictionary<string, double>? req = new()
        {
            { "P1", 45 },
            { "P2", 35 },
            { "P3", 35 }
        };

        var result = SolveBasicConflicts(people, projects, x0, cap, req, unmetWeight: 1000);

        Console.WriteLine($"Status: {result.Status}");
        if (result.Status != Solver.ResultStatus.OPTIMAL && result.Status != Solver.ResultStatus.FEASIBLE)
        {
            Console.WriteLine("No feasible solution found.");
            return;
        }

        Console.WriteLine("\n--- New allocations (x) ---");
        foreach (var p in people)
        {
            double total = 0;
            Console.WriteLine($"\n{p}:");
            foreach (var pr in projects)
            {
                var v = result.X[(p, pr)];
                total += v;
                Console.WriteLine($"  {pr}: {v:0.##} (was {x0.GetValueOrDefault((p, pr), 0):0.##})");
            }
            Console.WriteLine($"  Total: {total:0.##} / cap {cap[p]:0.##}");
        }

        if (result.Unmet != null)
        {
            Console.WriteLine("\n--- Unmet project demand (u) ---");
            foreach (var pr in projects)
            {
                Console.WriteLine($"  {pr}: {result.Unmet[pr]:0.##} hours/week unmet");
            }
        }
    }

    public record LpResult(
        Solver.ResultStatus Status,
        Dictionary<(string person, string project), double> X,
        Dictionary<string, double>? Unmet
    );

    public static LpResult SolveBasicConflicts(
        IReadOnlyList<string> people,
        IReadOnlyList<string> projects,
        Dictionary<(string person, string project), double> x0,
        Dictionary<string, double> cap,
        Dictionary<string, double>? req = null,
        double unmetWeight = 1000
    )
    {
        // CBC is usually available; if not, try "GLOP_LINEAR_PROGRAMMING" (LP only)
        Solver solver = Solver.CreateSolver("CBC_MIXED_INTEGER_PROGRAMMING")
                      ?? throw new Exception("Failed to create solver. Check OR-Tools installation.");

        // Decision vars x[p, pr] >= 0
        var x = new Dictionary<(string person, string project), Variable>();
        foreach (var person in people)
        foreach (var project in projects)
        {
            x[(person, project)] = solver.MakeNumVar(0.0, double.PositiveInfinity, $"x_{person}_{project}");
        }

        // Optional unmet demand u[pr] >= 0
        Dictionary<string, Variable>? u = null;
        if (req != null)
        {
            u = new Dictionary<string, Variable>();
            foreach (var project in projects)
                u[project] = solver.MakeNumVar(0.0, double.PositiveInfinity, $"unmet_{project}");
        }

        // Capacity constraints: sum_p x[i,p] <= cap[i]
        foreach (var person in people)
        {
            var ct = solver.MakeConstraint(double.NegativeInfinity, cap[person], $"cap_{person}");
            foreach (var project in projects)
                ct.SetCoefficient(x[(person, project)], 1.0);
        }

        // Only reduce: x[i,p] <= x0[i,p]
        foreach (var person in people)
        foreach (var project in projects)
        {
            double planned = x0.GetValueOrDefault((person, project), 0.0);
            var ct = solver.MakeConstraint(double.NegativeInfinity, planned, $"max_{person}_{project}");
            ct.SetCoefficient(x[(person, project)], 1.0);
        }

        // Demand constraints (optional): sum_i x[i,p] + u[p] == req[p]
        if (req != null && u != null)
        {
            foreach (var project in projects)
            {
                var ct = solver.MakeConstraint(req[project], req[project], $"req_{project}");
                foreach (var person in people)
                    ct.SetCoefficient(x[(person, project)], 1.0);
                ct.SetCoefficient(u[project], 1.0);
            }
        }

        // Objective:
        // minimise unmetWeight * sum(u) + sum(x0 - x)
        // equivalently minimise unmetWeight * sum(u) - sum(x) + const
        // constants don't matter, so:
        // minimise unmetWeight * sum(u) - sum(x)
        var obj = solver.Objective();
        obj.SetMinimization();

        // -sum(x)
        foreach (var person in people)
        foreach (var project in projects)
        {
            obj.SetCoefficient(x[(person, project)], -1.0);
        }

        // + unmetWeight * sum(u)
        if (req != null && u != null)
        {
            foreach (var project in projects)
                obj.SetCoefficient(u[project], unmetWeight);
        }

        var status = solver.Solve();

        // Extract solution
        var xSol = new Dictionary<(string person, string project), double>();
        foreach (var key in x.Keys)
            xSol[key] = x[key].SolutionValue();

        Dictionary<string, double>? unmetSol = null;
        if (u != null)
        {
            unmetSol = new Dictionary<string, double>();
            foreach (var project in projects)
                unmetSol[project] = u[project].SolutionValue();
        }

        return new LpResult(status, xSol, unmetSol);
    }
}
