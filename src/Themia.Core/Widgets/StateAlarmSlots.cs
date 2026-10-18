using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>[S-4623] The host's half of <see cref="AlarmSlot"/>: every view's alarms, collected from the state.</summary>
public static class StateAlarmSlots
{
    extension(AlarmSlot)
    {
        /// <summary>
        /// [S-4623] The alarms there are: one per alarm widget, of every view, whichever is shown. A widget
        /// id met twice (never written so, but a hand-edited file) is the first one's.
        /// </summary>
        public static IReadOnlyList<AlarmSlot> Of(IEnumerable<ViewState> views)
        {
            ArgumentNullException.ThrowIfNull(views);
            var slots = new List<AlarmSlot>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var view in views)
            {
                foreach (var widget in view.Widgets)
                {
                    if (widget.Config is IAlarm alarm && seen.Add(widget.Id))
                    {
                        slots.Add(new AlarmSlot(widget.Id, alarm));
                    }
                }
            }

            return slots;
        }
    }
}
