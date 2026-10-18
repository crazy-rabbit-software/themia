using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-602] [S-6400] The Tasks widget's styles: how a row lays out its due date, under the title
/// (List) or beside it on one line (Compact). What was its Layout setting.
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<TasksStyle>))]
public enum TasksStyle
{
    List = 0,
    Compact = 1,
}
