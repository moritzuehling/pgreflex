using System.Runtime.CompilerServices;


static class Logger
{
  static object Dummy = new object();

  public delegate void LogFn(params object[] args);

  public static LogFn Log([CallerMemberName] string caller = "Unknown", [CallerFilePath] string file = "unknown.cs", [CallerLineNumber] int line = 0)
  {
    return (object[] args) =>
    {
      lock (Dummy)
      {
        WriteCallsite(caller, file, line);
        Console.WriteLine($"{string.Join(" ", args.Select(a => a.ToString()))}");
      }
    };
  }

  public static LogFn Warn([CallerMemberName] string caller = "Unknown", [CallerFilePath] string file = "unknown.cs", [CallerLineNumber] int line = 0)
  {
    return (object[] args) =>
    {
      lock (Dummy)
      {
        WriteCallsite(caller, file, line);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"{string.Join(" ", args.Select(a => a.ToString()))}");
      }
    };
  }
  public static LogFn Error([CallerMemberName] string caller = "Unknown", [CallerFilePath] string file = "unknown.cs", [CallerLineNumber] int line = 0)
  {
    return (object[] args) =>
    {
      lock (Dummy)
      {
        WriteCallsite(caller, file, line);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"{string.Join(" ", args.Select(a => a.ToString()))}");
      }
    };
  }

  static void WriteCallsite(string caller, string file, int line)
  {
    Console.ForegroundColor = ConsoleColor.DarkBlue;
    Console.Write(Path.GetFileNameWithoutExtension(file));
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write($".cs:{line} ");
    Console.ForegroundColor = ConsoleColor.DarkYellow;
    Console.Write(caller);
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write("() ");
    Console.ResetColor();
  }

  private static string LongestL(string raw, ref int length)
  {
    length = Math.Max(raw.Length, length);
    return raw.PadLeft(length);
  }
  private static string LongestR(string raw, ref int length)
  {
    length = Math.Max(raw.Length, length);
    return raw.PadRight(length);
  }
}
