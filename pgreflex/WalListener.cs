using Npgsql;
using Npgsql.Replication;
using Npgsql.Replication.Internal;
using Npgsql.Replication.PgOutput;

class WalListener
{
  public required LogicalReplicationConnection Connection { get; set; }
  public required PgOutputReplicationSlot Slot { get; set; }


  public static async Task<WalListener> Create(DatabaseManager db)
  {
    var rc = new LogicalReplicationConnection(db.DataSource.ConnectionString);
    await rc.Open();

    var slot = await rc.CreatePgOutputReplicationSlot(
        "pgreflex_" + Guid.NewGuid().ToString("N"),
        temporarySlot: true,
        LogicalSlotSnapshotInitMode.NoExport
      );

    return new WalListener
    {
      Connection = rc,
      Slot = slot,
    };
  }

  public async Task Listen(CancellationToken token)
  {
    var enumerable = this.Connection.StartReplication(
      Slot,
      new PgOutputReplicationOptions("pgreflex", PgOutputProtocolVersion.V4, true, PgOutputStreamingMode.Off, true, false),
      token
    );
    Console.WriteLine("[wal] Started listening!");

    await foreach (var message in enumerable)
    {
      Console.WriteLine("[wal] Got replicate message: " + message.GetType().Name);
    }
  }
}
