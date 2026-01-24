using Google.Protobuf;
using Pgreflex.Protocol;

namespace Pgreflex;

public class ProtocolHandler
{
    public async Task HandleMessageAsync(ClientToServer message, Func<ServerToClient, Task> sendReply)
    {
        switch (message.ContentsCase)
        {
            case ClientToServer.ContentsOneofCase.AddSubscriptionToGroup:
                await HandleAddSubscriptionAsync(message.AddSubscriptionToGroup, sendReply, message.MessageId);
                break;
                
            default:
                Console.WriteLine($"[protocol] Unknown message type: {message.ContentsCase}");
                break;
        }
    }

    private async Task HandleAddSubscriptionAsync(AddSubscriptionToGroup msg, Func<ServerToClient, Task> sendReply, int inReplyTo)
    {
        Console.WriteLine($"[protocol] Subscribing group={msg.GroupId} sub={msg.SubscriptionId}");
        
        // TODO: Actually register subscription logic here
        
        var reply = new ServerToClient
        {
            InReplyTo = inReplyTo,
            SubscriptionAcknowledged = new SubscriptionAcknowledged
            {
                GroupId = msg.GroupId,
                SubscriptionId = msg.SubscriptionId,
                Watching = true
            }
        };

        await sendReply(reply);
    }
}
