using Microsoft.Extensions.Options;
using Npgsql;
using Npgsql.Replication;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;

class PgListener
{
  /// <summary>
  /// Class 42 — Syntax Error or Access Rule Violation
  /// 42710: duplicate_object
  /// https://www.postgresql.org/docs/current/errcodes-appendix.html
  /// </summary>
  const int PgDuplicateObject = 42710;


  public static async Task<bool> Initialize(PgreflexConfig config)
  {
    Console.WriteLine("Creating publication for the database");
    using (var conn = new NpgsqlConnection(config.ConnectionString))
    {
      await conn.OpenAsync();

      try
      {
        var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE PUBLICATION \"{config.PublicationName}\" FOR ALL TABLES";
        await cmd.ExecuteNonQueryAsync();

        Console.WriteLine(" -> Publication was successfully created!");
      }
      catch (PostgresException exc) when (exc.ErrorCode == PgDuplicateObject)
      {
        Console.WriteLine(" -> Not creating publication, already exists");
      }
    }

    Console.WriteLine("Connecting and creating replication slot");
    var rc = new LogicalReplicationConnection(config.ConnectionString);
    await rc.Open();

    var slot = await rc.CreatePgOutputReplicationSlot(
        "pgreflex_" + Guid.NewGuid().ToString("N"),
        temporarySlot: true,
        LogicalSlotSnapshotInitMode.NoExport
        );

    var cancellationTokenSource = new CancellationTokenSource();
    var pgOptions = new PgOutputReplicationOptions(config.PublicationName, PgOutputProtocolVersion.V4, true, PgOutputStreamingMode.Off);

    var enumerable = rc.StartReplication(slot, pgOptions, cancellationTokenSource.Token);

    return true;
  }
}
