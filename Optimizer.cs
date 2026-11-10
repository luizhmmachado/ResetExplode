using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ResetExplode;

/// <summary>Programa (agrupado pelo nome do executável) que pode ser fechado para liberar recursos.</summary>
public sealed class BackgroundApp : INotifyPropertyChanged
{
    public string ProcessName { get; init; } = "";
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public long MemoryBytes { get; init; }
    public int Count { get; init; }
    public bool HasWindow { get; init; }
    public bool Suggested { get; init; }
    public string MemoryText => $"{MemoryBytes / 1048576.0:N0} MB";

    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set { _selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record CleanupResult(long FreedBytes, int Trimmed, bool StandbyPurged, string? StandbyError);
public sealed record CloseResult(List<string> Closed, List<string> NotClosed);

/// <summary>Limpeza de memória e fechamento de programas em segundo plano.</summary>
public static class Optimizer
{
    /// <summary>Processos que nunca aparecem na lista (sistema, segurança, drivers, o próprio app).</summary>
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "lsaiso", "svchost",
        "dwm", "explorer", "fontdrvhost", "sihost", "ctfmon", "taskhostw", "RuntimeBroker", "StartMenuExperienceHost",
        "ShellExperienceHost", "SearchHost", "SearchIndexer", "TextInputHost", "SecurityHealthService", "SecurityHealthSystray",
        "MsMpEng", "NisSrv", "SgrmBroker", "audiodg", "conhost", "dllhost", "spoolsv", "WmiPrvSE", "LockApp", "ApplicationFrameHost",
        "SystemSettings", "smartscreen", "MemCompression", "Secure System", "WUDFHost", "dasHost", "nvcontainer", "NVDisplay.Container",
        "atiesrxx", "atieclxx", "amdfendrsr", "RadeonSoftware", "igfxEM", "PawnIO", "ResetExplode", "cmd", "powershell", "pwsh",
        "WindowsTerminal", "OpenConsole", "devenv", "Code", "dotnet",
        // Componentes do Windows/segurança que não devem ser encerrados.
        "backgroundTaskHost", "UserOOBEBroker", "MoNotificationUx", "ShellHost", "CHXSmartScreen", "SecHealthUI",
        "SecurityHealthHost", "DefenderSessionHelper",
    };

    /// <summary>Processos de segundo plano seguros de fechar durante o jogo (pré-marcados).</summary>
    private static readonly Dictionary<string, string> SuggestedApps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OneDrive"] = "Sincronização do OneDrive (pode usar disco e rede)",
        ["OneDriveStandaloneUpdater"] = "Atualizador do OneDrive",
        ["ms-teams"] = "Microsoft Teams",
        ["Teams"] = "Microsoft Teams (clássico)",
        ["Skype"] = "Skype",
        ["PhoneExperienceHost"] = "Vincular ao Celular",
        ["YourPhone"] = "Vincular ao Celular",
        ["CrossDeviceService"] = "Dispositivos móveis (vínculo do celular)",
        ["CrossDeviceResume"] = "Retomada de dispositivos móveis",
        ["Widgets"] = "Widgets do Windows",
        ["WidgetService"] = "Serviço de Widgets",
        ["WidgetBoard"] = "Widgets do Windows",
        ["MicrosoftStartFeedProvider"] = "Feed de notícias/widgets do Windows",
        ["AdobeARM"] = "Atualizador da Adobe",
        ["CCXProcess"] = "Adobe Creative Cloud",
        ["Creative Cloud"] = "Adobe Creative Cloud",
        ["AdobeIPCBroker"] = "Adobe (comunicação em segundo plano)",
        ["GoogleDriveFS"] = "Google Drive",
        ["GoogleUpdate"] = "Atualizador do Google",
        ["GoogleCrashHandler"] = "Relatório de erros do Google",
        ["GoogleCrashHandler64"] = "Relatório de erros do Google",
        ["Dropbox"] = "Dropbox",
        ["Cortana"] = "Cortana",
        ["OfficeClickToRun"] = "Atualizador do Office",
        ["MicrosoftEdgeUpdate"] = "Atualizador do Microsoft Edge",
    };

    /// <summary>
    /// Apps de segundo plano conhecidos que aparecem na lista mas não vêm pré-marcados:
    /// o usuário decide se aquele app (Armoury Crate, Steam, Discord, Bun...) pode fechar.
    /// </summary>
    private static readonly Dictionary<string, string> KnownBackground = new(StringComparer.OrdinalIgnoreCase)
    {
        ["msedge"] = "Microsoft Edge (processos em segundo plano)",
        ["msedgewebview2"] = "Microsoft Edge WebView2 (usado por vários apps)",
        ["GameBar"] = "Xbox Game Bar",
        ["GameBarFTPServer"] = "Xbox Game Bar",
        ["Copilot"] = "Microsoft Copilot",
        ["bun"] = "Bun (runtime JavaScript)",
        ["opencode-cli"] = "Bun (runtime JavaScript usado por ferramentas de desenvolvimento)",

        // ASUS / Armoury Crate
        ["ArmouryCrate"] = "Armoury Crate (ASUS)",
        ["ArmouryCrate.UserSessionHelper"] = "Armoury Crate (auxiliar da sessão)",
        ["ArmouryCrate.Service"] = "Armoury Crate (serviço)",
        ["ArmouryCrateControlInterface"] = "Armoury Crate (interface de controle)",
        ["ArmourySocketServer"] = "Armoury Crate (servidor local)",
        ["asus_framework"] = "ASUS Framework",
        ["AsusAppService"] = "ASUS App Service",
        ["ASUSFrameworkService"] = "ASUS Framework Service",
        ["ROGLiveService"] = "ROG Live Service",
        ["LightingService"] = "ASUS Aura (iluminação)",
        ["AsusCertService"] = "ASUS Certificate Service",
        ["AsusFanControlService"] = "ASUS Fan Control",
        ["AsusOptimization"] = "ASUS Optimization",
        ["AsusSoftwareManager"] = "ASUS Software Manager",
        ["AsusUpdateChecker"] = "ASUS Update Checker",
        ["AsusSystemAnalysis"] = "ASUS System Analysis",
        ["AsusSystemDiagnosis"] = "ASUS System Diagnosis",
        ["AsusScreenXpert"] = "ASUS ScreenXpert",
        ["AsusTPCenter"] = "ASUS Touchpad",
        ["AsusHotplugCpu"] = "ASUS Hotplug CPU",
        ["AcPowerNotification"] = "ASUS (notificações de energia)",

        // Periféricos, áudio e iluminação
        ["RazerSynapse"] = "Razer Synapse",
        ["RazerCentralService"] = "Razer Central",
        ["RazerCortex"] = "Razer Cortex",
        ["lghub"] = "Logitech G HUB",
        ["lghub_agent"] = "Logitech G HUB",
        ["LogiOptionsMgr"] = "Logitech Options+",
        ["iCUE"] = "Corsair iCUE",
        ["Corsair.Service"] = "Corsair iCUE (serviço)",
        ["MSIAfterburner"] = "MSI Afterburner",
        ["MSICenter"] = "MSI Center",
        ["GigabyteControlCenter"] = "Gigabyte Control Center",
        ["SteelSeriesGG"] = "SteelSeries GG",
        ["SteelSeriesEngine"] = "SteelSeries Engine",
        ["WallpaperEngine"] = "Wallpaper Engine",
        ["wallpaper32"] = "Wallpaper Engine",
        ["wallpaper64"] = "Wallpaper Engine",
        ["Voicemod"] = "Voicemod",
        ["Nahimic"] = "Nahimic (áudio)",
        ["SonicStudio"] = "ASUS Sonic Studio",
        ["SonicRadar"] = "ASUS Sonic Radar",
        ["WavesSvc"] = "Waves Audio",
        ["DolbyDAX"] = "Dolby Audio",
        ["RealtekAudioConsole"] = "Realtek Audio Console",

        // Launchers, jogos e comunicação
        ["DiscordSystemHelper"] = "Auxiliar do Discord",
        ["steam"] = "Steam",
        ["steamwebhelper"] = "Steam (interface)",
        ["Spotify"] = "Spotify",
        ["EpicGamesLauncher"] = "Epic Games Launcher",
        ["Battle.net"] = "Battle.net",
        ["RiotClientServices"] = "Riot Client",
        ["EADesktop"] = "EA app",
        ["UbisoftConnect"] = "Ubisoft Connect",
        ["GalaxyClient"] = "GOG Galaxy",
        ["Playnite"] = "Playnite",
        ["obs64"] = "OBS Studio",
        ["obs32"] = "OBS Studio",
        ["Streamlabs"] = "Streamlabs",
        ["Docker Desktop"] = "Docker Desktop",
        ["com.docker.backend"] = "Docker Desktop",
    };

    /// <summary>
    /// Lista os programas da sessão do usuário. Por padrão mostra os que têm janela, os apps de segundo
    /// plano conhecidos e os programas de terceiros (mesmo sem janela, como Armoury Crate e Bun).
    /// <paramref name="showAll"/> também inclui os processos do sistema que não estão na lista protegida.
    /// </summary>
    public static List<BackgroundApp> ListApps(bool showAll = false)
    {
        int session = Process.GetCurrentProcess().SessionId;
        var groups = new Dictionary<string, List<Process>>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.SessionId != session || Protected.Contains(p.ProcessName)) continue;
                if (!groups.TryGetValue(p.ProcessName, out var list)) groups[p.ProcessName] = list = new List<Process>();
                list.Add(p);
            }
            catch { }
        }

        var result = new List<BackgroundApp>();
        foreach (var (name, procs) in groups)
        {
            bool hasWindow = procs.Any(p => SafeWindow(p) != IntPtr.Zero);
            bool suggested = false;
            string? description = null;
            if (SuggestedApps.TryGetValue(name, out string? suggestedDetail)) { suggested = true; description = suggestedDetail; }
            else if (KnownBackground.TryGetValue(name, out string? knownDetail)) description = knownDetail;
            bool known = description != null;

            string company = procs.Select(SafeCompany).FirstOrDefault(c => c.Length > 0) ?? "";
            string path = procs.Select(SafePath).FirstOrDefault(p => p.Length > 0) ?? "";
            bool thirdParty = (!string.IsNullOrEmpty(company) && !company.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
                              || (!string.IsNullOrEmpty(path) && !IsWindowsPath(path));

            if (!showAll && !hasWindow && !known && !thirdParty) continue;

            string title = name;
            try { title = procs[0].MainModule?.FileVersionInfo.FileDescription is { Length: > 0 } d ? d : name; } catch { }
            string detail = description ?? (hasWindow ? "Programa aberto" : !string.IsNullOrEmpty(company) ? company : "Segundo plano");
            if (procs.Count > 1) detail += $" · {procs.Count} processos";
            long mem = procs.Sum(p => { try { return p.WorkingSet64; } catch { return 0L; } });

            result.Add(new BackgroundApp
            {
                ProcessName = name, Title = title, Detail = detail, MemoryBytes = mem, Count = procs.Count,
                HasWindow = hasWindow, Suggested = suggested, Selected = suggested
            });
        }
        return result.OrderByDescending(a => a.Suggested).ThenByDescending(a => a.HasWindow).ThenByDescending(a => a.MemoryBytes).ToList();
    }

    private static IntPtr SafeWindow(Process p) { try { return p.MainWindowHandle; } catch { return IntPtr.Zero; } }
    private static string SafePath(Process p) { try { return p.MainModule?.FileName ?? ""; } catch { return ""; } }
    private static string SafeCompany(Process p) { try { return p.MainModule?.FileVersionInfo.CompanyName ?? ""; } catch { return ""; } }

    /// <summary>Arquivos em C:\Windows são componentes do próprio Windows; o resto é programa instalado.</summary>
    private static bool IsWindowsPath(string path)
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Fecha os programas: com janela → pede para fechar (como clicar no X), sem forçar, para não perder nada
    /// não salvo; programa que só roda em segundo plano (sem janela) → encerra, inclusive os auxiliares.
    /// </summary>
    public static CloseResult CloseApps(IEnumerable<BackgroundApp> apps)
    {
        var closed = new List<string>();
        var notClosed = new List<string>();
        foreach (var app in apps)
        {
            var procs = Process.GetProcessesByName(app.ProcessName);
            foreach (var p in procs)
            {
                try
                {
                    if (SafeWindow(p) != IntPtr.Zero) p.CloseMainWindow();
                    else if (!app.HasWindow) p.Kill(); // app só de segundo plano: encerra os auxiliares também
                }
                catch { }
            }
            // Dá até 4 s para fecharem sozinhos.
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < 4 && Process.GetProcessesByName(app.ProcessName).Length > 0) Thread.Sleep(200);
            if (Process.GetProcessesByName(app.ProcessName).Length == 0) closed.Add(app.Title);
            else notClosed.Add(app.Title);
        }
        return new CloseResult(closed, notClosed);
    }

    /// <summary>
    /// Esvazia a lista de espera (standby) da RAM, como o ISLC, e enxuga o working set dos processos.
    /// A lista de espera é cache que o Windows libera sozinho quando precisa; esvaziá-la antes de jogar
    /// ajuda quando a RAM está quase cheia e o jogo engasga ao carregar áreas novas.
    /// </summary>
    public static CleanupResult CleanMemory()
    {
        long before = AvailableBytes();
        int trimmed = 0;
        int self = Environment.ProcessId;
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.Id == self || p.Id <= 4) continue;
                if (EmptyWorkingSet(p.Handle)) trimmed++;
            }
            catch { }
        }

        bool purged = false;
        string? error = null;
        try
        {
            EnablePrivilege("SeProfileSingleProcessPrivilege");
            int command = 4; // MemoryPurgeStandbyList
            int status = NtSetSystemInformation(80 /* SystemMemoryListInformation */, ref command, sizeof(int));
            purged = status == 0;
            if (!purged) error = $"o Windows recusou (0x{status:X8}); é preciso rodar como administrador";
        }
        catch (Exception ex) { error = ex.Message; }

        Thread.Sleep(300);
        return new CleanupResult(Math.Max(0, AvailableBytes() - before), trimmed, purged, error);
    }

    public static (long Total, long Available) MemoryStatus()
    {
        var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        return GlobalMemoryStatusEx(ref m) ? ((long)m.ullTotalPhys, (long)m.ullAvailPhys) : (0, 0);
    }

    private static long AvailableBytes() => MemoryStatus().Available;

    private static void EnablePrivilege(string name)
    {
        if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x0020 | 0x0008, out var token)) // ADJUST | QUERY
            throw new Win32Exception();
        try
        {
            if (!LookupPrivilegeValue(null, name, out long luid)) throw new Win32Exception();
            var tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Luid = luid, Attributes = 2 }; // SE_PRIVILEGE_ENABLED
            AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
        finally { CloseHandle(token); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct TOKEN_PRIVILEGES { public uint PrivilegeCount; public long Luid; public uint Attributes; }

    [DllImport("psapi.dll")] private static extern bool EmptyWorkingSet(IntPtr hProcess);
    [DllImport("ntdll.dll")] private static extern int NtSetSystemInformation(int infoClass, ref int info, int length);
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupPrivilegeValue(string? system, string name, out long luid);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES state, uint len, IntPtr prev, IntPtr retLen);
}
