using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Npgsql;
using Google.Protobuf;

namespace Pgreflex;

public static class Program
{
  public static async Task Main()
  {
    var sw = System.Diagnostics.Stopwatch.StartNew();
    DotNetEnv.Env.TraversePath().Load();

    Console.WriteLine($"pgreflex starting...");

    var source = NpgsqlDataSource.Create(AppConfig.DatabaseConnectionString);

    var dbManager = new DatabaseManager { DataSource = source };
    if (AppConfig.InitializeSchema)
    {
      if (AppConfig.ResetSchema)
      {
        await dbManager.ResetSchema();
      }

      await dbManager.InitSchema();
    }

    SubscriptionManager sub = new SubscriptionManager();
    _ = Task.Run(() => sub.Listen(dbManager, CancellationToken.None));


    // Generate a self-signed cert for TLS (for now).
    // Later, you can store/pin the public key/cert via Postgres discovery.
    using var serverCert = CertificateFactory.CreateSelfSignedServerCert(
        subjectName: $"CN=pgreflex-{Environment.MachineName}",
        validDays: 30
    );

    // SubjectPublicKeyInfo bytes (this is the “real” public key identity)
    byte[] spki = serverCert.PublicKey.ExportSubjectPublicKeyInfo();
    // Pin = base64(SHA256(SPKI))
    string pinB64 = Convert.ToBase64String(SHA256.HashData(spki));


    var walListener = await WalListener.Create(dbManager);
    _ = Task.Run(async () =>
    {
      try { await walListener.Listen(CancellationToken.None); }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }
    });

    _ = Task.Run(async () =>
    {
      while (true)
      {
        var ce = await walListener.ChangeEvents.ReadAsync();
        await sub.HandleWalUpdate(ce);
      }
    });

    var listener = new TcpListener(AppConfig.ListenEndPoint);
    listener.Start(backlog: 512);

    await dbManager.AnnouncePresence(walListener.Slot.Name, AppConfig.ListenHost, AppConfig.ListenPort, pinB64);

    Console.WriteLine("[server] ready. Waiting for connections...");
    Console.WriteLine($"[server] Startup took {sw.Elapsed.TotalSeconds:0.00}s");

    while (true)
    {
      TcpClient tcpClient = await listener.AcceptTcpClientAsync();
      _ = Task.Run(() => HandleClientAsync(tcpClient, serverCert, dbManager, sub));
    }
  }

  private static async Task HandleClientAsync(TcpClient tcpClient, X509Certificate2 serverCert, DatabaseManager dbManager, SubscriptionManager sub)
  {
    var remote = tcpClient.Client.RemoteEndPoint;
    var local = tcpClient.Client.LocalEndPoint;

    Console.WriteLine($"[conn] accepted remote={remote} local={local}");

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

      Console.WriteLine($"[conn] tls established remote={remote} protocol={sslStream.SslProtocol} cipher={sslStream.NegotiatedCipherSuite}");

      Connection protocolClient = new Connection(sslStream, sub);
      await Task.Delay(-1);
    }
    catch (OperationCanceledException)
    {
      Console.WriteLine($"[conn] tls handshake timed out remote={remote}");
    }
    catch (Exception ex)
    {
      Console.WriteLine($"[conn] error remote={remote} {ex.GetType().Name}: {ex.Message}");
    }
  }
}
