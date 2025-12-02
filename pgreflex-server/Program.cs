var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();


var config = new PgreflexConfig();
app.Configuration.GetRequiredSection("pgreflex").Bind(config);
if (config.ConnectionString == null)
{
  Console.Error.WriteLine("No database URL specificied, provide a pgreflex:ConnectionString");
  return;
}

Console.WriteLine("Confgi" + config.ConnectionString);
var listener = await PgListener.Initialize(config);

// TODO: Add middleware that implements HTTP `beaerer` auth with secret key
// Alternative: maybe configure auth in database? i.e.
// -> for connection, the client pushes a token (HMAC?) into a table
// -> this server uses this then once to authenticate (and re-authenticate?) the client
// -> positive about that approach would be 0 config for shared secrets (DB is trusted 3rd party)

app.MapGet("/", () => "reflexgres server working");
app.MapGet("/status", () => "ok");
app.MapFallback(() => "not found");

app.UseWebSockets();
app.Run();
