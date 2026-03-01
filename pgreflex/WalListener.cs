using System.Data;
using System.Diagnostics;
using System.Threading.Channels;
using Npgsql;
using Npgsql.Replication;
using Npgsql.Replication.Internal;
using Npgsql.Replication.PgOutput;
using Npgsql.Replication.PgOutput.Messages;
using Pgreflex;

class WalListener
{
  public required LogicalReplicationConnection Connection { get; set; }
  public required PgOutputReplicationSlot Slot { get; set; }

  private Channel<ChangeEvent> Changes = Channel.CreateUnbounded<ChangeEvent>(new() { SingleWriter = true });
  public ChannelReader<ChangeEvent> ChangeEvents { get { return Changes.Reader; } }

  public static async Task<WalListener> Create(DatabaseManager db)
  {
    await db.CreatePublication();

    var rc = new LogicalReplicationConnection(AppConfig.DatabaseConnectionString);
    await rc.Open();

    var slot = await rc.CreatePgOutputReplicationSlot(
        "pgreflex_" + Guid.NewGuid().ToString("N"),
        temporarySlot: true,
        LogicalSlotSnapshotInitMode.NoExport,
        twoPhase: false
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
    Log()("[wal] Started listening!");


    var w = Changes.Writer;

    // We can make a massive simplication here:
    // There's 3 cases: update, insert, delete
    // We want to invalidate when:
    // delete/insert: the row matches the conditions
    // update: the old *or* new row matches the conditions

    // So, we can simplify: we push the old and new row seperately into the queue - which makes the check later trivial
    await foreach (var message in enumerable)
    {
      Log()("replication lag: " + (DateTimeOffset.Now - message.ServerClock).TotalMilliseconds);

      if (message is InsertMessage insert)
      {
        await w.WriteAsync(new ChangeEvent
        {
          Table = insert.Relation.RelationName,
          Schema = insert.Relation.Namespace,
          ChangedColumns = await FromReplicationTuple(insert.NewRow),
        });
      }
      else if (message is FullUpdateMessage update)
      {
        var oldTuple = await FromReplicationTuple(update.OldRow);
        var newTuple = await FromReplicationTuple(update.NewRow);

        var res = newTuple.First(a => (a.ColumnName == "created_at"));

        if (ReplicationTuplesEqual(oldTuple, newTuple))
        {
          Log()($"Skipping update to {update.Relation.RelationName}, old and new tuple equal");
          continue;
        }

        await w.WriteAsync(new ChangeEvent
        {
          Table = update.Relation.RelationName,
          Schema = update.Relation.Namespace,
          ChangedColumns = oldTuple
        });

        await w.WriteAsync(new ChangeEvent
        {
          Table = update.Relation.RelationName,
          Schema = update.Relation.Namespace,
          ChangedColumns = newTuple,
        });
      }
      else if (message is FullDeleteMessage delete)
      {
        await w.WriteAsync(new ChangeEvent
        {
          Table = delete.Relation.RelationName,
          Schema = delete.Relation.Namespace,
          ChangedColumns = await FromReplicationTuple(delete.OldRow),
        });
      }

      Connection.SetReplicationStatus(message.WalEnd);
    }
  }

  private async Task<List<ChangedColumn>> FromReplicationTuple(ReplicationTuple tuple)
  {
    var res = new List<ChangedColumn>();
    await foreach (var col in tuple)
    {
      var cc = new ChangedColumn()
      {
        ColumnName = col.GetFieldName(),
        ColumnType = col.GetPostgresType(),
        ValType = col.GetFieldType(),
        Value = await col.Get<object>(),
      };

      res.Add(cc);
    }

    return res;
  }

  private bool ReplicationTuplesEqual(List<ChangedColumn> a, List<ChangedColumn> b)
  {
    for (var i = 0; i < a.Count; i++)
    {
      var av = a[i].Value;
      var bv = b[i].Value;

      var aNull = av == DBNull.Value || av == null;
      var bNull = bv == DBNull.Value || bv == null;


      // Both null, we continue
      if (aNull && bNull)
        continue;

      // one of them is null, but the other isn't -> something changed
      if (aNull || bNull)
        return false;

      if (!av!.Equals(bv!))
        return false;
    }

    return true;
  }
}
