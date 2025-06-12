using System.Text;

namespace CodexGui.Apps.CodexGui;

static class PatObfuscator
{
    public static string Encode(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    public static string Decode(string value)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch
        {
            return string.Empty;
        }
    }
}
