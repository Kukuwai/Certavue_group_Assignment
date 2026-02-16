using System;
using System.IO;
using System.Text;

namespace OllamaSpike
{
  public class MultiTextWriter : TextWriter
  {
    private readonly TextWriter _writer1;
    private readonly TextWriter _writer2;

    public MultiTextWriter(TextWriter writer1, TextWriter writer2)
    {
      _writer1 = writer1;
      _writer2 = writer2;
    }

    public override Encoding Encoding => _writer1.Encoding;

    public override void Write(char value)
    {
      _writer1.Write(value);
      _writer2.Write(value);
    }

    public override void WriteLine(string? value)
    {
      _writer1.WriteLine(value);
      _writer2.WriteLine(value);
    }

    public override void Flush()
    {
      _writer1.Flush();
      _writer2.Flush();
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        _writer1.Flush();
        _writer2.Flush();
      }
      base.Dispose(disposing);
    }
  }
}