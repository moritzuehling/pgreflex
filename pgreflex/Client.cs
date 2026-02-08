using System.Net;
using System.Runtime.CompilerServices;
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
    var cis = new CodedInputStream(Underlying, true);
    while (true)
    {

      var len = cis.ReadInt32();
      Console.WriteLine("Got len: " + len);
      var sub = CodedInputStream.CreateWithLimits(Underlying, len, 100);
      var msg_int = ClientToServer.Parser.ParseFrom(sub);

      Console.WriteLine("msg_int" + msg_int.MessageId);

      var msg = new ClientMessage()
      {
        Client = this,
        Message = msg_int
      };

      Console.WriteLine("Got message" + msg.Message.AddSubscriptionToGroup.GroupId);


      while (true)
      {
        MessageChannel.Writer.TryWrite(msg);
        Thread.Yield();
      }
    }
  }
}


record ClientMessage
{
  public required Client Client;
  public required ClientToServer Message;
}
