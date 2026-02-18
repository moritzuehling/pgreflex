using System.Net;

namespace Pgreflex;

public static class AppConfig
{
  public static string ListenHost => GetEnv("PGREFLEX_LISTEN_HOST", "127.0.0.1");
  public static string AnnounceHost => GetEnv("PGREFLEX_ANNOUNCE_HOST", ListenHost);
  public static int ListenPort => GetEnvInt("PGREFLEX_LISTEN_PORT", 5435);
  public static TimeSpan HandshakeTimeout => TimeSpan.FromMilliseconds(GetEnvInt("PGREFLEX_TLS_HANDSHAKE_TIMEOUT_MS", 10_000));

  public static bool InitializeSchema => GetEnvBool("PGREFLEX_INIT_SCHEMA", true);
  public static bool ResetSchema => GetEnvBool("PGREFLEX_RESET_SCHEMA", false);

  public static string DatabaseConnectionString = GetEnv("PGREFLEX_CONNECTION_STRING", "Host=127.0.0.1;Username=postgres;Password=postgres;Database=reflexgres");

  public static IPEndPoint ListenEndPoint
  {
    get
    {
      if (!IPAddress.TryParse(ListenHost, out var ip))
      {
        // Allow "localhost" etc.
        ip = Dns.GetHostAddresses(ListenHost)
            .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            ?? IPAddress.Loopback;
      }
      return new IPEndPoint(ip, ListenPort);
    }
  }

  private static string GetEnv(string name, string @default)
      => Environment.GetEnvironmentVariable(name) ?? @default;
  private static int GetEnvInt(string name, int @default)
  {
    var s = GetEnv(name, @default.ToString());
    if (string.IsNullOrWhiteSpace(s)) return @default;
    return int.TryParse(s, out var v) ? v : @default;
  }
  private static bool GetEnvBool(string name, bool @default)
  {
    return GetEnvInt(name, @default ? 1 : 0) != 0;
  }
}
