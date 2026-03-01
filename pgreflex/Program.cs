global using static Logger;
using System.Net;
using Npgsql;

namespace Pgreflex;


public static class Program
{
  public static async Task Main()
  {
    var sw = System.Diagnostics.Stopwatch.StartNew();
    DotNetEnv.Env.TraversePath().Load();

    Log()($"pgreflex starting...");

    ReplicationManager m = new ReplicationManager(AppConfig.DatabaseConnectionString, AppConfig.ListenEndPoint);
    await m.Run();


  }

}
