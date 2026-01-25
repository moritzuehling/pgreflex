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
    _ = Task.Run(async () => await walListener.Listen(CancellationToken.None));

    var listener = new TcpListener(AppConfig.ListenEndPoint);
    listener.Start(backlog: 512);

    await dbManager.AnnouncePresence(walListener.Slot.Name, AppConfig.ListenHost, AppConfig.ListenPort, pinB64);

    Console.WriteLine("[server] ready. Waiting for connections...");
    Console.WriteLine($"[server] Startup took {sw.Elapsed.TotalSeconds:0.00}s");

    while (true)
    {
      TcpClient tcpClient = await listener.AcceptTcpClientAsync();
      _ = Task.Run(() => HandleClientAsync(tcpClient, serverCert, dbManager));
    }
  }

  private static async Task HandleClientAsync(TcpClient tcpClient, X509Certificate2 serverCert, DatabaseManager dbManager)
  {
    var remote = tcpClient.Client.RemoteEndPoint;
    var local = tcpClient.Client.LocalEndPoint;

    Console.WriteLine($"[conn] accepted remote={remote} local={local}");

    try
    {
      tcpClient.NoDelay = true;

      await using var networkStream = tcpClient.GetStream();

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
      var options = new System.Net.Security.SslServerAuthenticationOptions
      {
        ServerCertificate = serverCert,
        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck,
        ClientCertificateRequired = true,
      };

      using var cts = new CancellationTokenSource(AppConfig.HandshakeTimeout);
      await sslStream.AuthenticateAsServerAsync(options, cts.Token);

      Console.WriteLine($"[conn] tls established remote={remote} protocol={sslStream.SslProtocol} cipher={sslStream.NegotiatedCipherSuite}");

      var handler = new ProtocolHandler();
      var headerBuffer = new byte[4];

      // Helper to send messages
      async Task SendReply(Pgreflex.Protocol.ServerToClient msg)
      {
        var bytes = msg.ToByteArray();
        var lenBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(bytes.Length));
        await sslStream.WriteAsync(lenBytes);
        await sslStream.WriteAsync(bytes);
      }

      while (true)
      {
        // Read 4 bytes length
        int read = 0;
        while (read < 4)
        {
          int n = await sslStream.ReadAsync(headerBuffer.AsMemory(read, 4 - read));
          if (n == 0) return; // Disconnected
          read += n;
        }

        int length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(headerBuffer, 0));

        // Sanity check length
        if (length < 0 || length > 10 * 1024 * 1024)
        {
          Console.WriteLine($"[conn] invalid message length {length} remote={remote}");
          break;
        }

        var payload = new byte[length];
        read = 0;
        while (read < length)
        {
          int n = await sslStream.ReadAsync(payload.AsMemory(read, length - read));
          if (n == 0) return;
          read += n;
        }

        var msg = Pgreflex.Protocol.ClientToServer.Parser.ParseFrom(payload);
        await handler.HandleMessageAsync(msg, SendReply);
      }
    }
    catch (OperationCanceledException)
    {
      Console.WriteLine($"[conn] tls handshake timed out remote={remote}");
    }
    catch (Exception ex)
    {
      Console.WriteLine($"[conn] error remote={remote} {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
      try { tcpClient.Close(); } catch { /* ignore */ }
    }
  }
}
