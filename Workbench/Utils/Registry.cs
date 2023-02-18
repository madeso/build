namespace Workbench.Utils;

// access registry on windows, returns None on non-windows

public static class Registry
{
    public static string? Hklm(string keyName, string valueName)
    {
        if (Core.IsWindows() == false)
        {
            return null;
        }

        var root = Microsoft.Win32.Registry.LocalMachine;

        var key = root.OpenSubKey(keyName);
        if (key == null) { return null; }

        var kind = key.GetValueKind(valueName);
        if (kind != Microsoft.Win32.RegistryValueKind.String) { return null; }

        var value = key.GetValue(valueName);
        if (value == null) { return null; }

        return value.ToString();
    }
}
