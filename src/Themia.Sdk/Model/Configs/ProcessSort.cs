using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-2411] What the process list is ranked by.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProcessSort>))]
public enum ProcessSort
{
    /// <summary>Processor share since the last read, busiest first.</summary>
    Cpu = 0,

    /// <summary>Private working set (Task Manager's Memory column), largest first.</summary>
    Memory = 1,
}
