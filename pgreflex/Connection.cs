
using System.Threading.Channels;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Pgreflex.Protocol;

class Connection
{
  Stream Underlying { get; set; }

  SubscriptionManager Subscription { get; set; }

  Thread ReadThread;


  public Connection(Stream underlying, SubscriptionManager sub)
  {
    Underlying = underlying;
    Subscription = sub;

    ReadThread = new Thread(Read);
    ReadThread.Start();
  }

  public void SendMessage(ServerToClient msg)
  {
    Console.WriteLine("Send: " + WalListener.Last.Elapsed.TotalMicroseconds);

    lock (Underlying)
    {
      var header = new byte[4];
      var bytes = msg.ToByteArray();
      BitConverter.TryWriteBytes(new Span<byte>(header, 0, 4), bytes.Length);

      Underlying.Write(header, 0, 4);
      Underlying.Write(bytes, 0, bytes.Length);
      Underlying.Flush();

    }
    Console.WriteLine("Send2: " + WalListener.Last.Elapsed.TotalMicroseconds);

  }

  public void Read()
  {
    try
    {
      byte[] lenBuf = new byte[4];
      while (true)
      {
        Underlying.ReadExactly(lenBuf, 0, 4);
        var len = BitConverter.ToInt32(lenBuf, 0);

        byte[] rawMessage = new byte[len];
        Underlying.ReadExactly(rawMessage, 0, len);

        var message = ClientToServer.Parser.ParseFrom(rawMessage);
        var msg = new ClientMessage()
        {
          Client = this,
          Message = message
        };

        Subscription.HandleClientMessage(msg).AsTask().Wait(-1);

      }
    }
    catch (EndOfStreamException)
    {
      Console.WriteLine("Client disconnected");
    }
  }
}


record ClientMessage
{
  public required Connection Client;
  public required ClientToServer Message;
}
