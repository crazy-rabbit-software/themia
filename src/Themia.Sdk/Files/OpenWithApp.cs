namespace Themia.App.Services.Files;

/// <summary>[S-6417] One app of a file's Open with: its handler's name (the key to open with it), what the menu says, and a file whose icon is the app's.</summary>
public sealed record OpenWithApp(string Name, string Label, string? IconFile);
