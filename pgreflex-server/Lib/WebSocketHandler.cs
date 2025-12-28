class WebSocketHandler
{
  public static void UseWebSockets(WebApplication app, PgListener listener)
  {

    app.Use(async (context, next) =>
    {
      if (context.Request.Path == "/ws")
      {
        if (context.WebSockets.IsWebSocketRequest)
        {
          using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
          var data = await webSocket.ReceiveMessageAssync(context.RequestAborted);
          Console.WriteLine("got: " + data);
          if (StringUtil.ConstantTimeCompare(data, listener.Secret))
          {
            await webSocket.SendStringAsync("authenticated");
          }
          else
          {
            Console.WriteLine("has: " + listener.Secret);
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
