using Themia.Core.Widgets;

namespace Themia.Sdk;

/// <summary>
/// [S-6907] Where a widget stands in the add menu's tree ([S-3510]): the folder it is at home in,
/// the run of rows it is in there (a line parts two runs), its place in that run, and, when it is
/// one of Popular's handful, its place in that column ([S-3511]).
/// </summary>
public sealed record AddPlacement(AddFolderId Shelf, int Group, int Rank, int? Popular = null);
