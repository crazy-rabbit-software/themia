namespace Themia.App.Services;

/// <summary>
/// [S-15900] The host's loads that draw later (a file's or a desktop icon's shell icon, a thumbnail, a folder's count, a
/// picture read from disk), each announced as it starts: a Debug render's settle listens and waits for them before it
/// takes a picture (<c>RenderSettle</c>), never a fixed sleep. In the app nobody listens and a load is only passed on.
/// </summary>
internal static class LoadWatch
{
    /// <summary>Who hears of each load still running; null in the app.</summary>
    public static Action<Task>? Listener { get; set; }

    /// <summary><paramref name="load"/>, announced to the listener when it is still running.</summary>
    public static Task<T> Of<T>(Task<T> load)
    {
        Announce(load);
        return load;
    }

    /// <summary><paramref name="load"/>, announced to the listener when it is still running.</summary>
    public static Task Of(Task load)
    {
        Announce(load);
        return load;
    }

    private static void Announce(Task load)
    {
        if (Listener is { } listener && !load.IsCompleted)
        {
            listener(load);
        }
    }
}
