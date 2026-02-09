using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Google.Protobuf;
using Pgreflex.Protocol;

class Client
{
  Stream Underlying { get; set; }

  Channel<ClientMessage> MessageChannel { get; set; }

  public ChannelReader<ClientMessage> Messages { get { return MessageChannel.Reader; } }

  Thread ReadThread;


  public Client(Stream underlying)
  {
    Underlying = underlying;
    MessageChannel = Channel.CreateUnbounded<ClientMessage>();

    ReadThread = new Thread(Read);
    ReadThread.Start();
  }

  public void SendMessage(ServerToClient msg)
  {
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

        Console.WriteLine("Got message" + msg.Message.AddSubscriptionToGroup.GroupId);

        while (true)
        {
          if (MessageChannel.Writer.TryWrite(msg))
            break;

          Thread.Yield();
        }
      }
    }
    catch (EndOfStreamException)
    {
      Console.WriteLine("Client disconnected");
      MessageChannel.Writer.Complete();
    }
  }
}


record ClientMessage
{
  public required Client Client;
  public required ClientToServer Message;
}
