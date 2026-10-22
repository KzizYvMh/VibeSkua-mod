using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;

namespace Skua.Core.Utils;

/// <summary>
/// Describes one running VibeSkua launcher (tabbed host window).
/// </summary>
public sealed class LauncherHostInfo
{
    public int Pid { get; set; }
    public long Hwnd { get; set; }
    public int Index { get; set; }
    public int TabCount { get; set; }
    public long StartTimeTicks { get; set; }
    public long LastFocusTicks { get; set; }

    public string Name => $"Launcher {Index}";
    public string DisplayName => $"{Name} ({TabCount} {(TabCount == 1 ? "tab" : "tabs")})";
}

/// <summary>
/// File based registry shared by every launcher and the Manager, so accounts can be routed to a specific launcher.
/// Each launcher owns %TEMP%\VibeSkuaHosts\{pid}.json and listens on its own named pipe.
/// </summary>
public static class LauncherRegistry
{
    public const string LegacyPipeName = "VibeSkuaTabHostPipe";

    public const string NewLauncherArg = "--new-launcher";
    public const string LauncherArg = "--launcher";
    public const string EmptyArg = "--empty";

    private const string LockName = "VibeSkuaHostRegistryLock";

    public static string RegistryDirectory => Path.Combine(Path.GetTempPath(), "VibeSkuaHosts");

    public static string PipeNameFor(int pid) => $"{LegacyPipeName}-{pid}";

    private static string FileFor(int pid) => Path.Combine(RegistryDirectory, $"{pid}.json");

    /// <summary>
    /// Returns every launcher that is still alive, ordered by index. Stale entries are deleted.
    /// </summary>
    public static List<LauncherHostInfo> GetLiveHosts()
    {
        List<LauncherHostInfo> hosts = new();
        if (!Directory.Exists(RegistryDirectory))
            return hosts;

        foreach (string file in Directory.GetFiles(RegistryDirectory, "*.json"))
        {
            LauncherHostInfo? info = null;
            try
            {
                info = JsonSerializer.Deserialize<LauncherHostInfo>(File.ReadAllText(file));
            }
            catch (IOException)
            {
                // Being rewritten right now; skip this round.
                continue;
            }
            catch { }

            if (info == null || !IsAlive(info))
            {
                try { File.Delete(file); } catch { }
                continue;
            }

            hosts.Add(info);
        }

        return hosts.OrderBy(h => h.Index).ToList();
    }

    public static LauncherHostInfo? GetHost(int pid)
    {
        return GetLiveHosts().FirstOrDefault(h => h.Pid == pid);
    }

    public static LauncherHostInfo? GetMostRecentlyFocused()
    {
        return GetLiveHosts().OrderByDescending(h => h.LastFocusTicks).FirstOrDefault();
    }

    /// <summary>
    /// Registers the current process as a launcher and returns its entry with a free index.
    /// </summary>
    public static LauncherHostInfo RegisterCurrent(long hwnd)
    {
        using Mutex mutex = new(false, LockName);
        bool locked = false;
        try
        {
            try { locked = mutex.WaitOne(2000); } catch (AbandonedMutexException) { locked = true; }

            HashSet<int> usedIndexes = GetLiveHosts().Where(h => h.Pid != Environment.ProcessId).Select(h => h.Index).ToHashSet();
            int index = 1;
            while (usedIndexes.Contains(index))
                index++;

            using Process current = Process.GetCurrentProcess();
            LauncherHostInfo info = new()
            {
                Pid = current.Id,
                Hwnd = hwnd,
                Index = index,
                StartTimeTicks = current.StartTime.ToUniversalTime().Ticks,
                LastFocusTicks = DateTime.UtcNow.Ticks
            };
            Write(info);
            return info;
        }
        finally
        {
            if (locked)
            {
                try { mutex.ReleaseMutex(); } catch { }
            }
        }
    }

    private static readonly object WriteLock = new();

    public static void Write(LauncherHostInfo info)
    {
        lock (WriteLock)
        try
        {
            Directory.CreateDirectory(RegistryDirectory);
            string target = FileFor(info.Pid);
            string temp = target + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(info));
            File.Move(temp, target, true);
        }
        catch { }
    }

    public static void Remove(int pid)
    {
        try { File.Delete(FileFor(pid)); } catch { }
    }

    /// <summary>
    /// Sends one line to the launcher with the given pid. Retries until <paramref name="timeoutMs"/> elapses
    /// so a launcher that is still starting up has time to open its pipe.
    /// </summary>
    public static bool TrySend(int pid, string line, int timeoutMs = 1500)
    {
        return TrySendToPipe(PipeNameFor(pid), line, timeoutMs, () => IsProcessAlive(pid));
    }

    public static bool TrySendToPipe(string pipeName, string line, int timeoutMs, Func<bool>? keepTrying = null)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        do
        {
            try
            {
                using NamedPipeClientStream pipeClient = new(".", pipeName, PipeDirection.Out, PipeOptions.None);
                pipeClient.Connect(500);
                using StreamWriter sw = new(pipeClient);
                sw.WriteLine(line);
                sw.Flush();
                return true;
            }
            catch
            {
                if (keepTrying != null && !keepTrying())
                    return false;
                Thread.Sleep(150);
            }
        }
        while (DateTime.UtcNow < deadline);

        return false;
    }

    /// <summary>
    /// Removes the launcher routing arguments from a client argument list.
    /// </summary>
    public static string[] ExtractRoutingArgs(string[] args, out bool newLauncher, out int launcherPid, out bool empty)
    {
        newLauncher = false;
        launcherPid = 0;
        empty = false;
        List<string> remaining = new();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == NewLauncherArg)
            {
                newLauncher = true;
            }
            else if (arg == EmptyArg)
            {
                empty = true;
            }
            else if (arg == LauncherArg)
            {
                if (i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out launcherPid);
                    i++;
                }
            }
            else
            {
                remaining.Add(arg);
            }
        }
        return remaining.ToArray();
    }

    private static bool IsAlive(LauncherHostInfo info)
    {
        try
        {
            using Process proc = Process.GetProcessById(info.Pid);
            if (proc.HasExited)
                return false;
            return info.StartTimeTicks == 0 || proc.StartTime.ToUniversalTime().Ticks == info.StartTimeTicks;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using Process proc = Process.GetProcessById(pid);
            return !proc.HasExited;
        }
        catch
        {
            return false;
        }
    }
}
