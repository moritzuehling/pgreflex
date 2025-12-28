using System.Net.WebSockets;
using System.Text;

public static class WebSocketsExtensions
{
  public static async Task<string> ReceiveMessageAssync(this WebSocket socket, CancellationToken token)
  {
    MemoryStream ms = new MemoryStream();
    var underlying = new byte[8192];

    while (true)
    {
      var buffer = new ArraySegment<byte>(underlying);
      var result = await socket.ReceiveAsync(buffer, token);

      ms.Write(buffer.Array!, buffer.Offset, result.Count);

      if (result.EndOfMessage)
        break;
    }

    return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
  }

  public static async Task SendStringAsync(this WebSocket socket, string contents)
  {
    var data = Encoding.UTF8.GetBytes(contents);
    var buff = new ArraySegment<byte>(data);

    await socket.SendAsync(buff, WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, CancellationToken.None);
  }
}
