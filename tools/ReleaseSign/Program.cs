using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Omnikit.Core.Updates;

// ReleaseSign – offline beim Entwickler, nie in der CI.
//   keygen <schlüssel.pem>                               neuen Signierschlüssel erzeugen (mit Passwort verschlüsselt)
//   manifest <app|agent> <version> <setup.exe> <out.json>  Manifest zu einem Setup schreiben (Größe, SHA-256)
//   sign <schlüssel.pem> <manifest.json>                  Manifest signieren → <manifest.json>.sig
//   verify <manifest.json> <app|agent>                    mit den in Omnikit eingebauten Schlüsseln prüfen
return args switch
{
    ["keygen", var keyFile] => ReleaseSigning.KeyGen(keyFile, ReadPassword(confirm: true)),
    ["manifest", var product, var version, var setup, var output] => ReleaseSigning.WriteManifest(product, version, setup, output),
    ["sign", var keyFile, var manifest] => ReleaseSigning.Sign(keyFile, manifest, ReadPassword(confirm: false)),
    ["verify", var manifest, var product] => ReleaseSigning.VerifyWithBuiltInKeys(manifest, product),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("Aufruf: ReleaseSign keygen <schlüssel.pem> | manifest <app|agent> <version> <setup.exe> <out.json> | sign <schlüssel.pem> <manifest.json> | verify <manifest.json> <app|agent>");
    return 2;
}

static string ReadPassword(bool confirm)
{
    var first = Prompt("Passwort des Signierschlüssels: ");
    if (confirm && Prompt("Noch einmal: ") != first)
    {
        throw new InvalidOperationException("Die Passwörter stimmen nicht überein.");
    }

    return first;
}

static string Prompt(string text)
{
    Console.Write(text);
    var password = new StringBuilder();
    while (Console.ReadKey(intercept: true) is var key && key.Key != ConsoleKey.Enter)
    {
        if (key.Key == ConsoleKey.Backspace && password.Length > 0)
        {
            password.Length--;
        }
        else if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
        }
    }

    Console.WriteLine();
    return password.ToString();
}

/// <summary>Die eigentliche Arbeit, ohne Konsole (für Tests).</summary>
internal static class ReleaseSigning
{
    public const int MinPasswordLength = 16;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static int KeyGen(string keyFile, string password)
    {
        if (password.Length < MinPasswordLength)
        {
            Console.Error.WriteLine($"Bitte ein Passwort mit mindestens {MinPasswordLength} Zeichen.");
            return 1;
        }

        if (File.Exists(keyFile))
        {
            Console.Error.WriteLine("Die Datei gibt es schon – ein vorhandener Schlüssel wird nie überschrieben.");
            return 1;
        }

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(keyFile, ExportEncrypted(key, password));

        // Der öffentliche Teil ist nicht geheim; als eigene Datei, damit Skripte ihn ohne Abtippen übernehmen können
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        File.WriteAllText(keyFile + ".pub", publicKey);
        Console.WriteLine("Schlüssel gespeichert. Öffentlicher Teil für UpdateSource.TrustedKeys (auch in " + keyFile + ".pub):");
        Console.WriteLine(publicKey);
        return 0;
    }

    public static string ExportEncrypted(ECDsa key, string password) =>
        key.ExportEncryptedPkcs8PrivateKeyPem(password, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 600_000));

    public static int WriteManifest(string product, string version, string setupFile, string output)
    {
        var kind = product switch { "app" => UpdateProduct.App, "agent" => UpdateProduct.Agent, _ => (UpdateProduct?)null };
        if (kind is not { } known || UpdateVerifier.ParseVersion(version) is not { } parsed)
        {
            Console.Error.WriteLine("Produkt muss „app“ oder „agent“ sein, die Version z. B. „4.0.1“ (ohne Vorab-Zusatz).");
            return 1;
        }

        var expectedName = UpdateVerifier.SetupFileName(known, parsed);
        if (!string.Equals(Path.GetFileName(setupFile), expectedName, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Das Setup muss „{expectedName}“ heißen.");
            return 1;
        }

        var bytes = File.ReadAllBytes(setupFile);
        var manifest = new UpdateManifest(1, product, version, expectedName, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        File.WriteAllText(output, JsonSerializer.Serialize(manifest, JsonOptions));
        Console.WriteLine($"Manifest geschrieben: {output}");
        return 0;
    }

    public static int Sign(string keyFile, string manifestFile, string password)
    {
        using var key = ECDsa.Create();
        key.ImportFromEncryptedPem(File.ReadAllText(keyFile), password);
        var signature = key.SignData(File.ReadAllBytes(manifestFile), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        File.WriteAllBytes(manifestFile + ".sig", signature);
        Console.WriteLine($"Signatur geschrieben: {manifestFile}.sig");
        return 0;
    }

    public static int VerifyWithBuiltInKeys(string manifestFile, string product)
    {
        var kind = product == "agent" ? UpdateProduct.Agent : UpdateProduct.App;
        var verdict = UpdateVerifier.Verify(
            File.ReadAllBytes(manifestFile), File.ReadAllBytes(manifestFile + ".sig"), UpdateSource.TrustedKeys, kind, new Version(0, 0, 0), out _);
        Console.WriteLine($"Ergebnis mit den eingebauten Schlüsseln: {verdict}");
        return verdict == UpdateVerdict.Valid ? 0 : 1;
    }
}
