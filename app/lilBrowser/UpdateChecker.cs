using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PxBrowser;

internal sealed record UpdateInfo(string Version, string Sha256, string Url);

internal static class UpdateChecker
{
    public const string Owner = "Hum1Tab";
    public const string Repository = "lil-browser";

    public static string RepositoryUrl => $"https://github.com/{Owner}/{Repository}";
    public static string ReleasesUrl => $"{RepositoryUrl}/releases";
    public static string ManifestUrl =>
        $"https://raw.githubusercontent.com/{Owner}/{Repository}/main/update.json";
    public static string SignatureUrl => ManifestUrl + ".sig";

    // Public half of the release signing key. The private half is kept outside the repo.
    private const string PublicModulus =
        "4jRclI2T6y6pqf3M1pccJN2BSSArytt4ofcDHfQeDlUVKmtCBW2+sMYG7yBH6VeaaomWpkNQNCKxgjKbFdiSZBDmv88ZlMZmRZXtRu5xqiiZ3VouH2frrDDA8iTk65ztvtqrNeUtFSp47NYqdvaQRvGWpV0NxB+Ru9MRULc8zFQJbbuupGzZ5AvZENNtOt4o4CdKr6OTNNVMR0mjhRbtrPKaIjyExiECciAq+sEmcoRcx8PvrBwQqMV+DlTi8vJB4VOm7lW2Q2Qsi2q3PTkmEP6hRYfN+mBWEXLhbPj8zPn/rv2T4+xUY0Pz1uUP2ludZh5W6fhQFZCyU4pVmZl/6Q==";
    private const string PublicExponent = "AQAB";

    public static async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("lIlBrowser/0.4");

        var manifestBytes = await http.GetByteArrayAsync(ManifestUrl, cancellationToken);
        var signatureText = await http.GetStringAsync(SignatureUrl, cancellationToken);
        if (!Verify(manifestBytes, signatureText.Trim())) return null;

        using var document = JsonDocument.Parse(manifestBytes);
        var root = document.RootElement;
        var versionText = root.GetProperty("version").GetString();
        var url = root.GetProperty("url").GetString();
        var sha256 = root.GetProperty("sha256").GetString();
        if (!Version.TryParse(versionText, out var version) ||
            version <= CurrentVersion() ||
            !IsTrustedReleaseUrl(url) ||
            !IsSha256(sha256))
        {
            return null;
        }

        return new UpdateInfo(version.ToString(3), sha256!.ToLowerInvariant(), url!);
    }

    private static bool Verify(byte[] manifest, string signatureText)
    {
        try
        {
            var signature = Convert.FromBase64String(signatureText);
            var parameters = new RSAParameters
            {
                Modulus = Convert.FromBase64String(PublicModulus),
                Exponent = Convert.FromBase64String(PublicExponent),
            };
            using var rsa = RSA.Create();
            rsa.ImportParameters(parameters);
            return rsa.VerifyData(manifest, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    private static Version CurrentVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
    }

    private static bool IsTrustedReleaseUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;
        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return false;
        return uri.AbsolutePath.StartsWith($"/{Owner}/{Repository}/releases/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSha256(string? value)
    {
        return value is { Length: 64 } &&
               value.All(c => Uri.IsHexDigit(c));
    }
}
