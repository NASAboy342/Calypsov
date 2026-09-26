using Photino.NET;
using Photino.NET.Server;
using System.Drawing;
using System.Text;
using Calypsov.Api;
using Calypsov.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Calypsov;
//NOTE: To hide the console window, go to the project properties and change the Output Type to Windows Application.
// Or edit the .csproj file and change the <OutputType> tag from "WinExe" to "Exe".
class Program
{
#if DEBUG
    public static bool IsDebugMode = true;
#else
    public static bool IsDebugMode = false;
#endif

    [STAThread]
    static void Main(string[] args)
    {
        // A packaged app isn't launched with its own folder as the working directory
        // (e.g. a macOS .app launched from Finder), but PhotinoServer's static file host
        // resolves "wwwroot" relative to the working directory. Pin it to the executable's
        // own directory so wwwroot (and any other relative lookup) resolves correctly
        // regardless of how the app was launched.
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        var app = PhotinoServer.CreateStaticFileServer(args, out string baseUrl);

        // In-memory-backed for now; swap the storage or add real encrypt/decrypt
        // logic behind this interface later without touching the API surface below.
        IEncryptionSettingsService encryptionSettings =
            // new MemoryEncryptionSettingsService(new MemoryCache(new MemoryCacheOptions()));
            new StorageEncryptionSettingsService();

        app.MapEncryptionEndpoints(encryptionSettings);

        IBrowserProfileService browserProfiles = new StorageBrowserProfileService();

        app.MapBrowserEndpoints(browserProfiles);

        // The appUrl is set to the local development server when in debug mode.
        // This helps with hot reloading and debugging.
        // The cache-busting query string on the production URL forces the WebView to fetch a
        // fresh index.html on every launch. Without it, the WebView's HTTP cache persists across
        // app restarts (it's tied to the app's data store, not the process), so after a rebuild it
        // can keep serving an old cached index.html that references now-deleted, old-hashed asset
        // chunks (e.g. old-SettingsView-<hash>.js), causing confusing 404s and stale behavior.
        string appUrl = IsDebugMode
            ? "http://localhost:5173"
            : $"{baseUrl}/index.html?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        Console.WriteLine($"Serving Vue app at {appUrl}");

        // Window title declared here for visibility
        string windowTitle = "Calypsov";

        // Creating a new PhotinoWindow instance with the fluent API.
        // .Load(appUrl) is called separately below, once the server is running,
        // so the dialog endpoints can be mapped against this window first.
        var window = new PhotinoWindow()
            .SetTitle(windowTitle)
            // Window/taskbar icon (Windows + Linux; no-op on macOS, which instead
            // gets its Dock icon from AppIcon/app.icns once the app is bundled as a .app).
            // Resolved against the executable's own directory rather than the process's
            // working directory, since a packaged .app is not launched from its own folder.
            .SetIconFile(Path.Combine(AppContext.BaseDirectory, "AppIcon", "app.ico"))
            // Resize to a percentage of the main monitor work area
            //.Resize(50, 50, "%")
            .SetUseOsDefaultSize(false)
            .SetSize(new Size(800, 600))
            // Center window in the middle of the screen
            .Center()
            // Users can resize windows by default.
            // Let's make this one fixed instead.
            .SetResizable(true)
            .RegisterCustomSchemeHandler("app", (object sender, string scheme, string url, out string contentType) =>
            {
                contentType = "text/javascript";
                return new MemoryStream(Encoding.UTF8.GetBytes(@"
                        (() =>{
                            window.setTimeout(() => {
                                alert(`🎉 Dynamically inserted JavaScript.`);
                            }, 1000);
                        })();
                    "));
            })
            // Most event handlers can be registered after the
            // PhotinoWindow was instantiated by calling a registration 
            // method like the following RegisterWebMessageReceivedHandler.
            // This could be added in the PhotinoWindowOptions if preferred.
            .RegisterWebMessageReceivedHandler((object sender, string message) =>
            {
                var window = (PhotinoWindow)sender;

                // The message argument is coming in from sendMessage.
                // "window.external.sendMessage(message: string)"
                string response = $"Received message: \"{message}\"";

                // Send a message back the to JavaScript event handler.
                // "window.external.receiveMessage(callback: Function)"
                window.SendWebMessage(response);
            });

        app.MapDialogEndpoints(window);

        app.RunAsync();

        window.Load(appUrl); // Can be used with relative path strings or "new URI()" instance to load a website.

        window.WaitForClose(); // Starts the application event loop
    }
}
