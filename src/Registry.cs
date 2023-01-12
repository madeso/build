namespace Workbench;

// access registry on windows, returns None on non-windows

public static class Registry
{
    public static string? hklm(string key_name, string value_name)
    {
        if (Core.IsWindows() == false)
        {
            return null;
        }

        var root = Microsoft.Win32.Registry.LocalMachine;

        var key = root.OpenSubKey(key_name);
        if (key == null) { return null; }

        var kind = key.GetValueKind(value_name);
        if (kind != Microsoft.Win32.RegistryValueKind.String) { return null; }

        var value = key.GetValue(value_name);
        if (value == null) { return null; }

        return value.ToString();
    }
}