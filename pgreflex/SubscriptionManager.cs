using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Channels;
using Pgreflex.Protocol;



class SubscriptionManager
{
  record SubscriptionManagerMessage
  {
    public ClientMessage? ClientMessage;
    public ChangeEvent? ChangeEvent;
  }


  Channel<SubscriptionManagerMessage> MessageQueue = Channel.CreateUnbounded<SubscriptionManagerMessage>(new UnboundedChannelOptions()
  {
    SingleReader = true,
    SingleWriter = false,
  });

  public ValueTask HandleClientMessage(ClientMessage cmst) =>
    MessageQueue.Writer.WriteAsync(new SubscriptionManagerMessage() { ClientMessage = cmst });

  public ValueTask HandleWalUpdate(ChangeEvent ev) =>
    MessageQueue.Writer.WriteAsync(new SubscriptionManagerMessage() { ChangeEvent = ev });

  public async Task Listen(DatabaseManager dbManager, CancellationToken? token = null)
  {
    var subs = new SubscriptionList();

    var reader = MessageQueue.Reader;
    while (true)
    {
      var message = await reader.ReadAsync(token ?? CancellationToken.None);
      var cmsg = message.ClientMessage;
      var ce = message.ChangeEvent;

      if (cmsg != null)
      {
        var astg = cmsg.Message.AddSubscriptionToGroup;
        if (astg != null)
        {
          await dbManager.EnsureFullyReplicated(astg.Conditions.Table, astg.Conditions.Schema);

          subs.Add(astg.Conditions.Table, new TableSubscription
          {
            Connection = cmsg.Client,
            Conditions = astg.Conditions,
            GroupId = astg.GroupId
          });

          lock (cmsg.Client)
          {
            cmsg.Client.SendMessage(new ServerToClient
            {
              InReplyTo = cmsg.Message.MessageId,
              SubscriptionAcknowledged = new SubscriptionAcknowledged
              {
                GroupId = astg.GroupId,
                SubscriptionId = astg.SubscriptionId,
                Watching = true,
              }
            });
          }
        }
      }
      else if (ce != null)
      {
        var invalidations = HandleInvalidations(ce, subs.Tables);
        subs.Remove(invalidations.Select(a => a.GroupId).ToList());
        _ = Task.Run(() =>
        {
          foreach (var inv in invalidations)
          {
            try
            {
              lock (inv.Connection)
              {
                Log()(inv.Connection);
                inv.Connection.SendMessage(new ServerToClient
                {
                  InvalidateGroup = new()
                  {
                    GroupId = inv.GroupId,
                  }
                });

              }
            }
            catch (Exception e)
            {
              Log()("Encountered error when trying to send invalidation: ", e);
            }
          }
        });
      }
    }
  }

  public List<TableSubscription> HandleInvalidations(ChangeEvent ce, SubscriptionState subs)
  {
    var res = new List<TableSubscription>();

    var table = subs.TryGetValue(ce.Table, out var subsToCheck);
    if (subsToCheck == null)
    {
      Log()($"Table {ce.Table} was never subscribed (available: {string.Join(", ", subs.Keys)})");
      return res;
    }

    if (subsToCheck.IsEmpty)
    {
      Log()($"Table {ce.Table} empty. Skipping.");
      return res;
    }

    foreach (var sub in subsToCheck)
    {
      var isMatch = sub.Conditions.Conditions.All(cond => Check(cond, ce));
      if (isMatch)
        res.Add(sub);
    }

    return res;
  }

  public bool Check(Condition cond, ChangeEvent ev)
  {
    var changedCol = ev.ChangedColumns.Find(a => a.ColumnName == cond.Column);
    if (changedCol == null)
    {
      Warn()($"Column {cond.Column} does not exist in change event for ${ev.Table}");
      return false;
    }


    if (cond.Operand == Operand.In)
    {
      foreach (var entry in cond.Value)
      {
        if (CheckEq(cond.Operand, entry, changedCol))
          return true;

        return false;
      }
    }

    if (cond.Value.Count != 1)
      throw new Exception("Must supply exactly 1 comperator!");

    return CheckEq(cond.Operand, cond.Value[0], changedCol);
  }

  private bool CheckEq(Operand op, ColValue cmpVal, ChangedColumn changedCol)
  {
    switch (op)
    {
      case Operand.Eq:

        if (cmpVal.IsNull)
          return changedCol.Value == null || changedCol.Value == DBNull.Value;

        if (changedCol.Value == null || changedCol.Value == DBNull.Value)
          return false;

        if (cmpVal.HasStr)
        {
          return (string)cmpVal.Str == (string)changedCol.Value;
        }

        if (cmpVal.HasNum)
          return (double)cmpVal.Num == (double)changedCol.Value;

        if (cmpVal.HasB)
          return (bool)cmpVal.B == (bool)changedCol.Value;

        if (cmpVal.HasTimestampMicros)
          return DateTimeOffset.FromUnixTimeMilliseconds((long)cmpVal.TimestampMicros) == (DateTimeOffset)changedCol.Value;

        throw new Exception("Unknown operand type!");

      case Operand.Neq:
        if (cmpVal.IsNull)
          return changedCol.Value != null || changedCol.Value != DBNull.Value;

        if (changedCol.Value == null || changedCol.Value == DBNull.Value)
          return true;

        if (cmpVal.HasStr)
          return (string)cmpVal.Str != (string)changedCol.Value;

        if (cmpVal.HasNum)
          return (double)cmpVal.Num != (double)changedCol.Value;

        if (cmpVal.HasB)
          return (bool)cmpVal.B != (bool)changedCol.Value;

        if (cmpVal.HasTimestampMicros)
          return DateTimeOffset.FromUnixTimeMilliseconds((long)cmpVal.TimestampMicros) != (DateTimeOffset)changedCol.Value;

        throw new Exception("Unknown operand type!");

      case Operand.Gt:
        if (changedCol.Value == null || changedCol.Value == DBNull.Value)
          return false;

        if (cmpVal.HasStr)
          return cmpVal.Str.CompareTo((string)changedCol.Value) > 0;

        if (cmpVal.HasNum)
          return cmpVal.Num.CompareTo((double)changedCol.Value) > 0;

        if (cmpVal.HasB)
          return cmpVal.B.CompareTo((bool)changedCol.Value) > 0;

        if (cmpVal.HasTimestampMicros)
          return DateTimeOffset.FromUnixTimeMilliseconds((long)cmpVal.TimestampMicros) > (DateTimeOffset)changedCol.Value;

        throw new Exception("Unknown operand type!");

      case Operand.Gte:
        if (changedCol.Value == null || changedCol.Value == DBNull.Value)
          return false;

        if (cmpVal.HasStr)
          return cmpVal.Str.CompareTo((string)changedCol.Value) >= 0;

        if (cmpVal.HasNum)
          return cmpVal.Num.CompareTo((double)changedCol.Value) >= 0;

        if (cmpVal.HasB)
          return cmpVal.B.CompareTo((bool)changedCol.Value) >= 0;

        if (cmpVal.HasTimestampMicros)
          return DateTimeOffset.FromUnixTimeMilliseconds((long)cmpVal.TimestampMicros) >= (DateTimeOffset)changedCol.Value;

        throw new Exception("Unknown operand type!");


      case Operand.Lt:
        if (changedCol.Value == null || changedCol.Value == DBNull.Value)
          return false;

        if (cmpVal.HasStr)
          return cmpVal.Str.CompareTo((string)changedCol.Value) < 0;

        if (cmpVal.HasNum)
          return cmpVal.Num.CompareTo((double)changedCol.Value) < 0;

        if (cmpVal.HasB)
          return cmpVal.B.CompareTo((bool)changedCol.Value) < 0;

        if (cmpVal.HasTimestampMicros)
          return DateTimeOffset.FromUnixTimeMilliseconds((long)cmpVal.TimestampMicros) < (DateTimeOffset)changedCol.Value;

        throw new Exception("Unknown operand type!");

      case Operand.Lte:
        if (changedCol.Value == null || changedCol.Value == DBNull.Value)
          return false;

        if (cmpVal.HasStr)
          return cmpVal.Str.CompareTo((string)changedCol.Value) <= 0;

        if (cmpVal.HasNum)
          return cmpVal.Num.CompareTo((double)changedCol.Value) <= 0;

        if (cmpVal.HasB)
          return cmpVal.B.CompareTo((bool)changedCol.Value) <= 0;

        if (cmpVal.HasTimestampMicros)
          return DateTimeOffset.FromUnixTimeMilliseconds((long)cmpVal.TimestampMicros) <= (DateTimeOffset)changedCol.Value;
        throw new Exception("Unknown operand type!");
    }
    throw new Exception("unspported operand :(");
  }
}
