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
        _ = Task.Run(async () =>
        {
          foreach (var inv in invalidations)
          {
            try
            {
              lock (inv.Connection)
              {
                Console.WriteLine(inv.Connection);
                Console.WriteLine("invalidated group " + inv.GroupId);
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
              Console.WriteLine("Encountered error when trying to send invalidation: " + e);
            }
          }
        });
      }
    }
  }

  public List<TableSubscription> HandleInvalidations(ChangeEvent ce, SubscriptionState subs)
  {
    var res = new List<TableSubscription>();
    Console.WriteLine($"Handling change event of ${ce.Table}");

    var table = subs.TryGetValue(ce.Table, out var subsToCheck);
    if (subsToCheck == null)
    {
      Console.WriteLine($"Table {ce.Table} was never subscribed (available: {string.Join(", ", subs.Keys)})");
      return res;
    }

    if (subsToCheck.IsEmpty)
    {
      Console.WriteLine($"Table {ce.Table} empty. Skipping.");
      return res;
    }

    foreach (var sub in subsToCheck)
    {
      var isMatch = sub.Conditions.Conditions.All(cond => true);
      if (isMatch)
        res.Add(sub);
    }

    Console.WriteLine($"Checked {subsToCheck.Count} subs, {res.Count} matched.");
    return res;
  }

  public bool Check(Condition cond, ChangeEvent ev)
  {
    var col = ev.ChangedColumns.Find(a => a.ColumnName == cond.Column);
    if (col == null)
    {
      Console.WriteLine($"Column {cond.Column} does not exist in change event for ${ev.Table}");
      return false;
    }

    switch (cond.Operand)
    {
      case Operand.Eq:
        if (cond.IsNull)
          return col.Value == null || col.Value == DBNull.Value;

        if (col.Value == null || col.Value == DBNull.Value)
          return false;

        if (cond.HasStr)
          return (string)cond.Str == (string)col.Value;

        if (cond.HasNum)
          return (double)cond.Num == (double)col.Value;

        if (cond.HasB)
          return (bool)cond.B == (bool)col.Value;

        if (cond.HasTimestampMicros)
          return DateTimeOffset.FromUnixTimeMilliseconds((long)cond.TimestampMicros) == (DateTimeOffset)col.Value;

        throw new Exception("Unknown operand type!");

      case Operand.Neq:
        if (cond.IsNull)
          return col.Value != null || col.Value != DBNull.Value;

        if (col.Value == null || col.Value == DBNull.Value)
          return true;

        if (cond.HasStr)
          return (string)cond.Str != (string)col.Value;

        if (cond.HasNum)
          return (double)cond.Num != (double)col.Value;

        if (cond.HasB)
          return (bool)cond.B != (bool)col.Value;

        if (cond.HasTimestampMicros)
          return DateTimeOffset.FromUnixTimeMilliseconds((long)cond.TimestampMicros) != (DateTimeOffset)col.Value;

        throw new Exception("Unknown operand type!");

      case Operand.Gt:
        if (col.Value == null || col.Value == DBNull.Value)
          return false;

        if (cond.HasStr)
          return cond.Str.CompareTo((string)col.Value) > 0;

        if (cond.HasNum)
          return cond.Num.CompareTo((double)col.Value) > 0;

        if (cond.HasB)
          return cond.B.CompareTo((bool)col.Value) > 0;

        if (cond.HasTimestampMicros)
          return DateTimeOffset.FromUnixTimeMilliseconds((long)cond.TimestampMicros) > (DateTimeOffset)col.Value;

        throw new Exception("Unknown operand type!");

      case Operand.Gte:
        if (col.Value == null || col.Value == DBNull.Value)
          return false;

        if (cond.HasStr)
          return cond.Str.CompareTo((string)col.Value) >= 0;

        if (cond.HasNum)
          return cond.Num.CompareTo((double)col.Value) >= 0;

        if (cond.HasB)
          return cond.B.CompareTo((bool)col.Value) >= 0;

        if (cond.HasTimestampMicros)
          return DateTimeOffset.FromUnixTimeMilliseconds((long)cond.TimestampMicros) >= (DateTimeOffset)col.Value;

        throw new Exception("Unknown operand type!");


      case Operand.Lt:
        if (col.Value == null || col.Value == DBNull.Value)
          return false;

        if (cond.HasStr)
          return cond.Str.CompareTo((string)col.Value) < 0;

        if (cond.HasNum)
          return cond.Num.CompareTo((double)col.Value) < 0;

        if (cond.HasB)
          return cond.B.CompareTo((bool)col.Value) < 0;

        if (cond.HasTimestampMicros)
          return DateTimeOffset.FromUnixTimeMilliseconds((long)cond.TimestampMicros) < (DateTimeOffset)col.Value;

        throw new Exception("Unknown operand type!");

      case Operand.Lte:
        if (col.Value == null || col.Value == DBNull.Value)
          return false;

        if (cond.HasStr)
          return cond.Str.CompareTo((string)col.Value) <= 0;

        if (cond.HasNum)
          return cond.Num.CompareTo((double)col.Value) <= 0;

        if (cond.HasB)
          return cond.B.CompareTo((bool)col.Value) <= 0;

        if (cond.HasTimestampMicros)
          return DateTimeOffset.FromUnixTimeMilliseconds((long)cond.TimestampMicros) <= (DateTimeOffset)col.Value;

        throw new Exception("Unknown operand type!");

      default:
        throw new Exception("unspported operand :(");
    }
  }
}
