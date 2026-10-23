namespace PxBrowser;

internal static class UrlPolicy
{
    public static bool IsAllowedNavigation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;

        if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(uri.Host);
        }

        if (uri.Scheme.Equals("about", StringComparison.OrdinalIgnoreCase))
        {
            return value.Equals("about:blank", StringComparison.OrdinalIgnoreCase);
        }

        // Blob URLs are origin-bound and are used by legitimate web apps for
        // PDFs/media. They do not grant local-file or OS-protocol access.
        return uri.Scheme.Equals("blob", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAllowedPopup(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
               uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
               value.Equals("about:blank", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAllowedTypedUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
               uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTrustedUpdateUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;
        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return false;
        return uri.AbsolutePath.StartsWith("/Hum1Tab/lil-browser/", StringComparison.OrdinalIgnoreCase);
    }

    public static string PermissionOrigin(Uri? uri)
    {
        if (uri == null || string.IsNullOrWhiteSpace(uri.Host)) return "unknown";
        return $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}:{uri.Port}";
    }
}
