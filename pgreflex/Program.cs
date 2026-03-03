global using static Logger;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pgreflex;

public static class Program
{
  public static async Task Main()
  {
    Log()($"pgreflex starting...");
    DotNetEnv.Env.TraversePath().Load();


    if (string.IsNullOrEmpty(AppConfig.DatabaseConnectionFile) && string.IsNullOrEmpty(AppConfig.DatabaseConnectionString))
    {
      Error()("You must either supply a PGREFLEX_CONNECTION_FILE or a PGREFLEX_CONNECTION_STRING envar.");
      return;
    }

    if (AppConfig.DatabaseConnectionString != "")
    {
      Log()("Connecting to connection string manager");
      ReplicationManager m = new ReplicationManager(AppConfig.DatabaseConnectionString, AppConfig.ListenEndPoint);
      await m.Run();
    }
    else
    {
      Log()("Using connection file", AppConfig.DatabaseConnectionFile);
      int port = 0;
      List<ReplicationManager> activeManagers = new();

      while (true)
      {
        try
        {
          var targetConnectionStrings = JsonSerializer.Deserialize<List<string>>(await File.ReadAllTextAsync(AppConfig.DatabaseConnectionFile))!;
          foreach (var connectionString in targetConnectionStrings)
          {
            if (activeManagers.Find(a => a.ConnectionString == connectionString) == null)
            {
              var newManager = new ReplicationManager(connectionString, new IPEndPoint(AppConfig.ListenEndPoint.Address, AppConfig.ListenEndPoint.Port + (port++)));
              _ = Task.Run(() => newManager.Run());
              activeManagers.Add(newManager);
            }
          }

          var removed = activeManagers.Where(manager => targetConnectionStrings.Find(a => a == manager?.ConnectionString) == null).ToArray();
          foreach (var manager in removed)
          {
            manager.Dispose();
          }
          activeManagers.RemoveAll(a => removed.Contains(a));
        }
        catch (Exception e)
        {
          Error()("Error reading file", AppConfig.DatabaseConnectionFile, ":", e);
        }

        await Task.Delay(1000);
      }
    }
  }

}
