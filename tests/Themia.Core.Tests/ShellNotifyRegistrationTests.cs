using System.Text.RegularExpressions;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-5511] <c>SHChangeNotifyRegister</c> is given one folder per call. On Windows 11 build 26300
/// a call naming two or more entries reads past them and crashes the process inside
/// windows.storage.dll (an access violation at 0xFFFFFFFFFFFFFFFF), which made Themia exit on
/// every start with the desktop's items chosen; one call per folder registers each of them.
/// The call cannot run in a test without risking the host, so the rule is read from the source.
/// </summary>
public sealed partial class ShellNotifyRegistrationTests
{
    [Fact]
    public void S5511_each_shell_change_registration_names_one_folder()
    {
        var calls = 0;
        var offenders = new List<string>();
        foreach (var file in RepoFiles.CSharp(RepoFiles.Src))
        {
            var text = File.ReadAllText(file);
            foreach (Match call in Call().Matches(text))
            {
                var arguments = Arguments(text, call.Index + call.Length);
                if (arguments is null)
                {
                    continue; // the declaration: a parameter list, not a call
                }

                calls++;
                if (arguments.Count != 6 || arguments[4] != "1")
                {
                    offenders.Add($"{Path.GetFileName(file)}: SHChangeNotifyRegister({string.Join(", ", arguments)})");
                }
            }
        }

        Assert.True(calls > 0, "no SHChangeNotifyRegister call found under src/");
        Assert.True(offenders.Count == 0, "a registration names more than one folder:\n" + string.Join("\n", offenders));
    }

    /// <summary>The call's top-level arguments, or null when it is the P/Invoke's declaration.</summary>
    private static List<string>? Arguments(string text, int open)
    {
        var depth = 1;
        var start = open;
        var found = new List<string>();
        for (var i = open; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '(':
                    depth++;
                    break;
                case ')' when --depth == 0:
                    found.Add(text[start..i].Trim());
                    return found.Any(a => a.Contains(' ') && Declared().IsMatch(a)) ? null : found;
                case ',' when depth == 1:
                    found.Add(text[start..i].Trim());
                    start = i + 1;
                    break;
            }
        }

        return null;
    }

    [GeneratedRegex(@"\bSHChangeNotifyRegister\(")]
    private static partial Regex Call();

    /// <summary>A parameter: a type then a name (<c>nint hwnd</c>, <c>ChangeNotifyEntry* entry</c>).</summary>
    [GeneratedRegex(@"^[\w.]+\*?\s+\w+$")]
    private static partial Regex Declared();
}
