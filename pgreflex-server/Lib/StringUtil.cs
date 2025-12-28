using System.Security.Cryptography;
using System.Text;

class StringUtil
{
  public static bool ConstantTimeCompare(string a, string b)
  {
    Console.WriteLine(a.Length);
    Console.WriteLine(b.Length);

    var ahash = SHA256.HashData(Encoding.UTF8.GetBytes(a));
    var bhash = SHA256.HashData(Encoding.UTF8.GetBytes(b));

    // Initializing this this way in the hopes that it's less
    // likely that the compile does weird things?
    var diffBytes = -ahash.Length;
    for (var i = 0; i < ahash.Length; i++)
    {
      diffBytes += (ahash[i] != bhash[i]) ? 2 : 1;
      Console.WriteLine($"ahash[i]: {ahash[i]} bhash[i]: {bhash[i]} diffBytes: {diffBytes}");
    }

    Console.WriteLine("db" + diffBytes);

    // At this point, we're basically sure it's the same string
    // so I'm not worried about not-equal time anymore
    // The heavy lifting is done by (securely) hashing the data in the first place.
    return diffBytes == 0 ? (a == b) : false;
  }
}
