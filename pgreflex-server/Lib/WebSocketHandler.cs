using System.Security.Cryptography;

class WebSocketHandler
{
  public static void UseWebSockets(WebApplication app, PgListener listener)
  {

    app.Use(async (context, next) =>
    {

      if (context.Request.Path == "/ws")
      {
        var connectionId = RandomNumberGenerator.GetHexString(8);

        Console.WriteLine($"[{connectionId}] New WS connection from {context.Request.HttpContext.Connection.RemoteIpAddress}:{context.Request.HttpContext.Connection.RemotePort}");
        if (context.WebSockets.IsWebSocketRequest)
        {
          using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

          // Cancel after 5 seconds *or* incorrect token.
          var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, new CancellationTokenSource(10000).Token);
          var data = await webSocket.ReceiveMessageAssync(connectTimeout.Token, listener.Secret.Length + 10);
          Console.WriteLine($"[{connectionId}] requested authentication with secret ${data.Substring(0, 8)}... (${data.Length})");
          if (StringUtil.ConstantTimeCompare(data, listener.Secret))
          {
            Console.WriteLine($"[{connectionId}] Authentication Successful!");
            await webSocket.SendStringAsync("authenticated");
          }
          else
          {
            Console.WriteLine($"[{connectionId}] Authentication unsucessful!");
            await webSocket.SendStringAsync("wrong secret!");
            await webSocket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.InvalidPayloadData, "wrong secret", context.RequestAborted);
            return;
          }

        }
        else
        {
          context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
      }
      else
      {
        await next(context);
      }
    });
  }
}
