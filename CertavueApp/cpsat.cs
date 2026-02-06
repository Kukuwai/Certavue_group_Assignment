using System;
using System.Collections.Generic;
using System.Linq;
using Google.OrTools.Sat;

public class cpsat
{
     public class SolveResult
    {
        public CpSolverStatus Status { get; set; }
        public int TotalConflicts { get; set; }
        public Dictionary<Project, int> ChosenShiftByProject { get; set; } = new();
    }
}