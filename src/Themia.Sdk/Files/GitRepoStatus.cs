using Themia.Core.Widgets;

namespace Themia.App.Services.Files;

/// <summary>A folder's repository: its root, and the parsed status.</summary>
public sealed record GitRepoStatus(string Root, GitSummary Summary);
