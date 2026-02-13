
using System.Threading.Channels;
using Google.Protobuf;
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
    Console.WriteLine("SendMessage!");
    lock (Underlying)
    {
      msg.WriteDelimitedTo(Underlying);
    }
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
        Console.WriteLine("Got len: " + len);

        byte[] rawMessage = new byte[len];
        Underlying.ReadExactly(rawMessage, 0, len);

        var message = ClientToServer.Parser.ParseFrom(rawMessage);

        Console.WriteLine("msg_int: " + message.MessageId);

        var msg = new ClientMessage()
        {
          Client = this,
          Message = message
        };

        Console.WriteLine("Got message " + msg.Message.AddSubscriptionToGroup.GroupId);
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
