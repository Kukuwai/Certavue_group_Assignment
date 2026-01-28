using MathNet.Numerics.Optimization;
using QuikGraph;
using static Project;

public class Person
{
    private static int idCounter = 0; 
    public int id {get; }
    public string name {get; set;}
    public HashSet<Project> projects { get; } = new();

    public Person(string name)
    {
        id = ++idCounter;
        this.name = name;
    }

}
