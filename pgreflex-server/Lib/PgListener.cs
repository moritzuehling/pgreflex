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
  const string PgDuplicateObject = "42710";

  internal required PgOutputReplicationSlot ReplicationSlot;

  internal required PgreflexConfig Config;

  internal required string Secret { get; set; }

  private PgListener() { }

  public static async Task<PgListener> Initialize(PgreflexConfig config)
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
      catch (PostgresException exc) when (exc.SqlState == PgDuplicateObject)
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

    Console.WriteLine($"Replication Slot Name: {slot.Name}");

    var cancellationTokenSource = new CancellationTokenSource();
    var pgOptions = new PgOutputReplicationOptions(config.PublicationName, PgOutputProtocolVersion.V4, true, PgOutputStreamingMode.Off);

    var enumerable = rc.StartReplication(slot, pgOptions, cancellationTokenSource.Token);

    return new PgListener()
    {
      ReplicationSlot = slot,
      Secret = Guid.NewGuid().ToString(),
      Config = config,
    };
  }

  public async Task AnnouncePresence(string[] urls)
  {
    using (var conn = new NpgsqlConnection(Config.ConnectionString))
    {
      await conn.OpenAsync();
      var query = conn.CreateCommand();
      query.CommandText = "CREATE SCHEMA IF NOT EXISTS pgreflex;";
      await query.ExecuteNonQueryAsync();

      query.CommandText = """
        CREATE TABLE IF NOT EXISTS pgreflex.servers (
            slot_name TEXT PRIMARY KEY,
            uri TEXT[] NOT NULL,
            server_hostname TEXT NOT NULL,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            shared_secret TEXT NOT NULL
        );
      """;
      await query.ExecuteNonQueryAsync();

      query.CommandText = """
      CREATE OR REPLACE VIEW
        pgreflex.active_servers
      AS (
        SELECT
          s.*,
          rs.temporary,
          rs.active
        FROM
          pgreflex.servers s,
          pg_catalog.pg_replication_slots rs
        WHERE
          rs.slot_name = s.slot_name
      )
      """;
      await query.ExecuteNonQueryAsync();

      query.CommandText = "INSERT INTO pgreflex.servers (slot_name, uri, server_hostname, shared_secret) VALUES (@sn, @cu, @host, @sec)";
      query.Parameters.Add(new NpgsqlParameter<string>("sn", ReplicationSlot.Name));
      query.Parameters.Add(new NpgsqlParameter<string[]>("cu", urls));
      query.Parameters.Add(new NpgsqlParameter<string>("sec", this.Secret));
      query.Parameters.Add(new NpgsqlParameter<string>("host", Environment.MachineName));

      await query.ExecuteNonQueryAsync();
    }
  }
}
