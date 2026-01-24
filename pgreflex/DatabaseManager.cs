using System.Diagnostics;
using Npgsql;

public class DatabaseManager
{
  public required NpgsqlDataSource DataSource;

  public async Task ResetSchema()
  {
    Console.WriteLine("[schema] Debug: Deleting existing schema");
    await using var cmd = DataSource.CreateCommand("DROP SCHEMA IF EXISTS pgreflex CASCADE;");
    await cmd.ExecuteNonQueryAsync();
  }

  public async Task InitSchema()
  {
    Console.WriteLine("[schema] Ensuring pgreflex schema exists...");
    var script = LoadSqlScript("init.sql");
    await using var command = DataSource.CreateCommand(script);
    await command.ExecuteNonQueryAsync();
  }

  public async Task<List<string>> GetRecentClientCertificates()
  {
    await using var command = DataSource.CreateCommand("SELECT client_certificate_hash FROM pgreflex.client_authentications WHERE expires_at > CURRENT_TIMESTAMP");
    List<string> res = new();
    await using var reader = await command.ExecuteReaderAsync();
    await reader.ReadAsync();
    do
    {
      res.Add(reader.GetString(0));
    } while (await reader.ReadAsync());

    return res;
  }

  public async Task AnnouncePresence(string slotName, string host, int port, string certificateHash)
  {
    Console.WriteLine($"[db] Announcing Presence for slot name {slotName}");
    await using var command = DataSource.CreateCommand("INSERT INTO pgreflex.servers (slot_name, host, port, certificate_hash) VALUES ($1, $2, $3, $4)");
    command.Parameters.Add(new NpgsqlParameter(null, slotName));
    command.Parameters.Add(new NpgsqlParameter(null, host));
    command.Parameters.Add(new NpgsqlParameter(null, port));
    command.Parameters.Add(new NpgsqlParameter(null, certificateHash));
    await command.ExecuteNonQueryAsync();

    File.WriteAllText("/tmp/pgreflex-server-spki-sha256.b64", certificateHash);
  }


  private string LoadSqlScript(string name)
  {
    var asm = typeof(DatabaseManager).Assembly;

    var fullName = asm
      .GetManifestResourceNames()
      .Single(n => n.EndsWith(name, StringComparison.Ordinal));

    using var stream = asm.GetManifestResourceStream(fullName)
      ?? throw new InvalidOperationException($"Resource not found: {fullName}");

    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
  }
}
