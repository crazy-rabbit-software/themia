using Themia.Core.Localization;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-3500] What the line that takes the edit bar's place while placing says the click will do: "Click to place Clock",
/// "Click to move 3 widgets".
/// </summary>
public static class PlacementHint
{
    /// <summary>The click's words for <paramref name="payload"/>, without the cancel part of the line.</summary>
    public static string Verb(PlacementPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        // [S-2471] A ready-made group is placed by its name, as one widget is.
        if (payload.ReadyMade is { } group)
        {
            return Str.Shell_Place_PlaceOne.Format(("name", group.Name.Text()));
        }

        if (payload.SingleKind is { } kind)
        {
            var name = ModuleCatalog.Of(kind).Name;
            return payload.Mode switch
            {
                PlacementMode.Move => Str.Shell_Place_MoveOne.Format(("name", name)),
                PlacementMode.Copy => Str.Shell_Place_CopyOne.Format(("name", name)),
                _ => Str.Shell_Place_PlaceOne.Format(("name", name)),
            };
        }

        return payload.Mode switch
        {
            PlacementMode.Move => Str.Shell_Place_MoveMany.Plural(payload.Count),
            PlacementMode.Copy => Str.Shell_Place_CopyMany.Plural(payload.Count),
            _ => Str.Shell_Place_PlaceMany.Plural(payload.Count),
        };
    }
}
