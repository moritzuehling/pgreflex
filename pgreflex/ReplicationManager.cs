
using System.Net;
using System.Net.Sockets;
using Npgsql;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Reflection.PortableExecutable;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Pgreflex;

class ReplicationManager : IDisposable
{
  public string ConnectionString { get; set; }
  private IPEndPoint EndPoint { get; set; }
  DatabaseManager DbManager { get; set; }
  string SlotName = "pgreflex_" + Guid.NewGuid().ToString("N");
  SubscriptionManager SubscriptionManager { get; } = new SubscriptionManager();
  CancellationTokenSource Cancelled = new CancellationTokenSource();
  TcpListener Listener;

  public ReplicationManager(string connectionString, IPEndPoint listenEndpoint)
  {
    this.ConnectionString = connectionString;
    this.DbManager = new DatabaseManager { DataSource = NpgsqlDataSource.Create(ConnectionString) };
    EndPoint = listenEndpoint;
    Listener = new TcpListener(listenEndpoint);
  }

  public async Task Run()
  {
    AddLoggerPrefix(DbManager.Host);
    Log()("Listening on", EndPoint);

    if (AppConfig.InitializeSchema)
    {
      if (AppConfig.ResetSchema)
      {
        await DbManager.ResetSchema();
      }

      await DbManager.InitSchema();
    }

    Log()("Creating certificate, initializing");
    var serverCert = CertificateFactory.CreateSelfSignedServerCert($"CN=pgreflex", 10);

    Channel<ChangeEvent> changeEvents = Channel.CreateUnbounded<ChangeEvent>();

    var walConnection = new WalConnection();
    _ = Task.Run(() => walConnection.Run(ConnectionString, DbManager, changeEvents.Writer));
    _ = Task.Run(() => SubscriptionManager.Listen(DbManager, Cancelled.Token));

    Listener.Start(backlog: 512);

    Log()("Announcing presence");
    await DbManager.AnnouncePresence(SlotName, AppConfig.AnnounceHost, AppConfig.ListenPort, serverCert.SpkiBase64Hash());

    int count = 0;

    while (true)
    {
      TcpClient tcpClient = await Listener.AcceptTcpClientAsync(Cancelled.Token);

      // EnsureListener may only be called from this main accept loop, and must be awaited
      _ = Task.Run(() => HandleClientAsync(tcpClient, serverCert, DbManager, SubscriptionManager, walConnection, ++count));
    }
  }

  private async Task HandleClientAsync(TcpClient tcpClient, X509Certificate2 serverCert, DatabaseManager dbManager, SubscriptionManager sub, WalConnection connection, int count)
  {
    AddLoggerPrefix($"client #{count}");

    var remote = tcpClient.Client.RemoteEndPoint;
    var local = tcpClient.Client.LocalEndPoint;
    Log()($"accepted remote={remote} local={local}");

    try
    {
      tcpClient.NoDelay = true;

      var networkStream = tcpClient.GetStream();

      // Get all client certificates announced in the db in the last 30 seconds
      // (both insert/query rely on CURRENT_TIMESTAMP on db server)
      var certificates = await dbManager.GetRecentClientCertificates();

      await using var sslStream = new SslStream(
          networkStream,
          leaveInnerStreamOpen: false,
          (sender, cert, chain, errors) =>
          {
            // The certificate is validate later.
            if (cert is null) return false;
            var spki = new X509Certificate2(cert).PublicKey.ExportSubjectPublicKeyInfo();
            if (spki is null) return false;

            var certHash = Convert.ToBase64String(SHA256.HashData(spki));

            return certificates.Contains(certHash);
          }
      );

      // TLS handshake
      var options = new SslServerAuthenticationOptions
      {
        ServerCertificate = serverCert,
        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
        ClientCertificateRequired = true,
      };

      using var cts = new CancellationTokenSource(AppConfig.HandshakeTimeout);
      await sslStream.AuthenticateAsServerAsync(options, cts.Token);

      Log()($"tls established remote={remote} protocol={sslStream.SslProtocol} cipher={sslStream.NegotiatedCipherSuite}");

      await connection.ConnectionEvents.WriteAsync((tcpClient, true));
      await connection.WaitForConnection();
      Log()("WAL connection acquired, handling client events!");

      Connection protocolClient = new Connection(sslStream, sub, Cancelled.Token);
      await protocolClient.Done;
    }
    catch (OperationCanceledException)
    {
      Log()($"tls handshake timed out remote={remote}");
    }
    catch (Exception ex)
    {
      Log()($"error remote={remote} {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
      await connection.ConnectionEvents.WriteAsync((tcpClient, false));
    }
  }

  public void Dispose()
  {
    Log()("Stopping listening!");
    this.Cancelled.Cancel();
    Listener.Dispose();
  }
}
