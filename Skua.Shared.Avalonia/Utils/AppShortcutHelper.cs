using System;
using System.IO;
using Velopack.Locators;
#if WINDOWS || NET6_0_OR_GREATER
using Velopack.Windows;
#endif

namespace Skua.Shared.Avalonia.Utils;

public static class AppShortcutHelper
{
    public static bool IsInstalled()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            string appDir = AppContext.BaseDirectory;
            if (File.Exists(Path.Combine(appDir, "..", "Update.exe")) ||
                File.Exists(Path.Combine(appDir, "sq.version")))
            {
                return true;
            }
        }
        catch
        {
        }

        try
        {
            if (VelopackLocator.Current?.CurrentlyInstalledVersion != null)
                return true;
        }
        catch
        {
        }

        return false;
    }

    public static void CreateOrUpdateShortcuts(bool createIfMissing = true)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            string appDir = AppContext.BaseDirectory;
            string clientExe = Path.Combine(appDir, "SkuaZZ.exe");
            string managerExe = Path.Combine(appDir, "SkuaZZ.Manager.exe");

            if (!File.Exists(clientExe) && !File.Exists(managerExe))
                return;

            string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string startMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);

            string[] targetDirs = new[] { desktopDir, startMenuDir };

            foreach (string dir in targetDirs)
            {
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                    continue;

                string clientLnk = Path.Combine(dir, "SkuaZZ.lnk");
                string managerLnk = Path.Combine(dir, "SkuaZZ Manager.lnk");

                // 1. Client shortcut (SkuaZZ.lnk -> SkuaZZ.exe)
                if (File.Exists(clientExe))
                {
                    bool needCreateClient = false;
                    if (File.Exists(clientLnk))
                    {
                        try
                        {
                            using var existingLink = new ShellLink(clientLnk);
                            string? target = existingLink.Target;
                            if (!string.IsNullOrEmpty(target) &&
                                (target.Contains("SkuaZZ.Manager", StringComparison.OrdinalIgnoreCase) ||
                                 target.Contains("Skua.Manager", StringComparison.OrdinalIgnoreCase)))
                            {
                                // Fix legacy/buggy shortcut that pointed to manager instead of client
                                existingLink.Target = clientExe;
                                existingLink.WorkingDirectory = appDir;
                                existingLink.Description = "SkuaZZ - AQW Client";
                                existingLink.IconPath = clientExe;
                                existingLink.IconIndex = 0;
                                existingLink.Save();
                            }
                        }
                        catch
                        {
                            if (createIfMissing)
                                needCreateClient = true;
                        }
                    }
                    else if (createIfMissing)
                    {
                        needCreateClient = true;
                    }

                    if (needCreateClient)
                    {
                        try
                        {
                            using var clientLink = new ShellLink();
                            clientLink.Target = clientExe;
                            clientLink.WorkingDirectory = appDir;
                            clientLink.Description = "SkuaZZ - AQW Client";
                            clientLink.IconPath = clientExe;
                            clientLink.IconIndex = 0;
                            clientLink.Save(clientLnk);
                        }
                        catch
                        {
                        }
                    }
                }

                // 2. Manager shortcut (SkuaZZ Manager.lnk -> SkuaZZ.Manager.exe)
                if (File.Exists(managerExe))
                {
                    bool needCreateMgr = false;
                    if (File.Exists(managerLnk))
                    {
                        // Existing shortcut is fine
                    }
                    else if (createIfMissing)
                    {
                        needCreateMgr = true;
                    }

                    if (needCreateMgr)
                    {
                        try
                        {
                            using var mgrLink = new ShellLink();
                            mgrLink.Target = managerExe;
                            mgrLink.WorkingDirectory = appDir;
                            mgrLink.Description = "SkuaZZ Manager";
                            mgrLink.IconPath = managerExe;
                            mgrLink.IconIndex = 0;
                            mgrLink.Save(managerLnk);
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }
        catch
        {
        }
    }

    public static void RemoveClientShortcuts()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            string appDir = AppContext.BaseDirectory;
            string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string startMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);

            foreach (string dir in new[] { desktopDir, startMenuDir })
            {
                if (string.IsNullOrWhiteSpace(dir))
                    continue;

                string clientLnk = Path.Combine(dir, "SkuaZZ.lnk");
                if (File.Exists(clientLnk))
                {
                    try
                    {
                        using var link = new ShellLink(clientLnk);
                        string? target = link.Target;
                        if (target != null && (target.StartsWith(appDir, StringComparison.OrdinalIgnoreCase) || target.Contains("SkuaZZ", StringComparison.OrdinalIgnoreCase)))
                        {
                            File.Delete(clientLnk);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }
        catch
        {
        }
    }
}
