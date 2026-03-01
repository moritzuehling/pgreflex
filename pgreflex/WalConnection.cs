using System.Net.Sockets;
using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;

namespace Pgreflex;

class WalConnection
{
  Channel<(TcpClient client, bool isConnect)> Events = Channel.CreateUnbounded<(TcpClient client, bool isConnect)>();

  public ChannelWriter<(TcpClient client, bool isConnect)> ConnectionEvents => Events.Writer;

  Task<WalListener>? WalListenerTask;

  public async Task Run(string connectionString, DatabaseManager dbManager, ChannelWriter<ChangeEvent> writer)
  {
    HashSet<TcpClient> clients = new HashSet<TcpClient>();

    var reader = Events.Reader;

    WalListener? listener = null;

    while (true)
    {

      var (client, isConnect) = await reader.ReadAsync();
      if (isConnect)
        clients.Add(client);
      else
        clients.Remove(client);

      Log()("got a", isConnect ? "connect" : "disconnect", "event, now have", clients.Count, "clients");
      if (clients.Count == 0)
      {
        if (await NothingAvailableAfter(reader, TimeSpan.FromSeconds(10)))
        {
          Log()("No reconnection, disconnecting logical replication!");

          // We need to disconnect
          WalListenerTask = null;
          listener?.CancellationTokenSource.Cancel();
          listener = null;
        }
      }
      else
      {
        if (listener == null)
        {
          WalListenerTask = WalListener.Create(connectionString, dbManager);
          listener = await WalListenerTask;
          _ = Task.Run(async () => await listener.Listen(writer));
        }
      }
    }
  }

  public async Task<WalListener> WaitForConnection()
  {
    for (int i = 0; i < 10; i++)
    {
      var wlt = WalListenerTask;
      if (wlt != null)
        return await wlt;

      await Task.Delay(TimeSpan.FromMilliseconds(5));
    }

    throw new Exception("No WAL connection requested?!");
  }


  async Task<bool> NothingAvailableAfter<T>(ChannelReader<T> reader, TimeSpan timespan)
  {
    if (reader.Count > 0)
      return false;

    await Task.WhenAny(Task.Delay(timespan), reader.WaitToReadAsync().AsTask());
    return reader.Count == 0;
  }
}
