using System.Globalization;

namespace Windows.ApplicationModel.Resources;

public sealed class ResourceLoaderConfiguration
{
    public ResourceLoaderConfiguration(string resourceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceRoot);
        ResourceRoot = Path.GetFullPath(resourceRoot);
    }

    public string ResourceRoot { get; }

    public string DefaultCultureName { get; init; } = "en-US";

    public Func<CultureInfo> UICultureProvider { get; init; } = static () => CultureInfo.CurrentUICulture;
}
