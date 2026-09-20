namespace CapFrameX.Service.Core.Platform;

/// <summary>
/// Where the service says which port it actually took.
/// </summary>
/// <remarks>
/// The token file answers "may I talk to it"; this one answers "where". Both are needed and
/// neither can be assumed: the default port can be taken - CapFrameX 1.x's own web service used to
/// sit on the one this service started with - so a frontend that hard-codes a number is a frontend
/// that fails on exactly the machines the transition is for.
///
/// Not a secret, so it is written plainly rather than through the secret writer: knowing the port
/// gets nobody past the guard.
/// </remarks>
public static class ServiceEndpointFile
{
    /// <summary>Name of the file inside the runtime directory.</summary>
    public const string FileName = "service.port";

    /// <summary>Full path of the file for a given layout.</summary>
    /// <param name="paths">Supplies the runtime directory.</param>
    public static string PathFor(IAppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return Path.Combine(paths.RuntimeDirectory, FileName);
    }

    /// <summary>Records the port this service instance is listening on.</summary>
    /// <param name="paths">Supplies the runtime directory.</param>
    /// <param name="port">The port Kestrel bound.</param>
    public static void Publish(IAppPaths paths, int port)
    {
        ArgumentNullException.ThrowIfNull(paths);

        Directory.CreateDirectory(paths.RuntimeDirectory);
        File.WriteAllText(PathFor(paths), port.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Removes it, because a port left behind points at a service that has stopped.</summary>
    /// <param name="paths">Supplies the runtime directory.</param>
    public static void Revoke(IAppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        try
        {
            File.Delete(PathFor(paths));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing to be done about it, and it is not worth failing a shutdown over.
        }
    }

    /// <summary>Reads the port a running service published, if there is one.</summary>
    /// <param name="paths">Supplies the runtime directory.</param>
    public static int? Read(IAppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        try
        {
            var path = PathFor(paths);

            return File.Exists(path) && int.TryParse(File.ReadAllText(path).Trim(), out var port)
                ? port
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
