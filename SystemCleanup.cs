using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;

namespace ResetExplode;

/// <summary>Resultado de uma limpeza: bytes liberados, arquivos apagados e o que foi ignorado (em uso).</summary>
public sealed record CleanupReport(long FreedBytes, int Files, int Skipped, int Folders, string? Error = null);

/// <summary>Serviços que foram encerrados e os que não puderam ser encerrados (nome do serviço).</summary>
public sealed record ServiceStopResult(List<string> Stopped, List<string> Failed);

/// <summary>Serviço opcional do Windows que pode ser encerrado para liberar RAM/CPU durante o jogo.</summary>
public sealed class OptionalService : INotifyPropertyChanged
{
    public string Name { get; init; } = "";
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";

    private bool _running;
    public bool Running
    {
        get => _running;
        set
        {
            _running = value;
            Changed(nameof(Running));
            Changed(nameof(StatusText));
            Changed(nameof(CanStop));
        }
    }

    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set { _selected = value; Changed(nameof(Selected)); }
    }

    /// <summary>Só dá para marcar o que está em execução; serviço já parado não tem o que encerrar.</summary>
    public bool CanStop => Running;
    public string StatusText => Running ? "EM EXECUÇÃO" : "JÁ PARADO";

    private void Changed(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Limpeza do sistema que não é só memória: lixeira, arquivos temporários de todos os usuários e caches
/// que o Windows/jogos recriam sozinhos, além de encerrar serviços opcionais.
/// Nada aqui mexe em segurança (Defender, Windows Update, VBS/HVCI, UAC) nem em arquivos pessoais.
/// </summary>
public static class SystemCleanup
{
    private sealed record ServiceDef(string Name, string Title, string Detail, bool Recommended);

    /// <summary>
    /// Serviços que podem ser encerrados sem quebrar o Windows. "Recommended" = seguro para a maioria
    /// dos PCs (telemetria, relatórios de erro, fax, registro remoto...). Os demais ficam desmarcados
    /// porque dependem do uso (impressora, busca, Bluetooth, Xbox/Game Pass, indexação).
    /// </summary>
    private static readonly ServiceDef[] ServiceCatalog =
    {
        new("DiagTrack", "Experiências do Usuário Conectado e Telemetria",
            "Coleta e envia dados de diagnóstico para a Microsoft.", true),
        new("dmwappushservice", "Roteamento de Mensagens WAP",
            "Usado pela telemetria; não é necessário em um PC doméstico.", true),
        new("WerSvc", "Relatório de Erros do Windows",
            "Prepara relatórios quando um programa fecha sozinho. O Windows continua funcionando sem ele.", true),
        new("Fax", "Fax",
            "Só serve para enviar/receber fax por modem.", true),
        new("RemoteRegistry", "Registro Remoto",
            "Permite que outro PC leia o registro desta máquina. Encerrar também é mais seguro.", true),
        new("RetailDemo", "Conteúdo de Demonstração de Varejo",
            "Modo de demonstração usado em lojas; inútil em casa.", true),
        new("MapsBroker", "Gerenciador de Mapas Baixados",
            "Baixa mapas offline para o app Mapas.", true),
        new("PhoneSvc", "Serviço de Telefonia",
            "Gerencia o modem de telefonia do PC.", true),
        new("lfsvc", "Localização Geográfica",
            "Fornece a localização para apps (clima, mapas). Desligar não afeta jogos.", true),
        new("WMPNetworkSvc", "Compartilhamento de Mídia do Windows Media Player",
            "Compartilha bibliotecas de mídia pela rede.", true),
        new("TrkWks", "Acompanhamento de Links Distribuídos",
            "Rastreia atalhos entre volumes; dispensável em um PC doméstico.", true),
        new("WpcMonSvc", "Controle dos Pais",
            "Aplica limites de tempo e conteúdo para contas infantis.", true),
        new("SEMgrSvc", "Pagamentos e NFC",
            "Pagamentos por aproximação; inútil em desktop.", true),
        new("wisvc", "Windows Insider Service",
            "Só é usado por quem participa do programa Windows Insider.", true),

        new("WSearch", "Windows Search (indexação)",
            "Indexa arquivos para a busca do menu Iniciar. Encerrar libera disco e CPU, mas a busca fica mais lenta.", false),
        new("SysMain", "SysMain (Superfetch)",
            "Pré-carrega programas na RAM. Em SSD costuma ajudar; em PC com pouca RAM pode atrapalhar.", false),
        new("Spooler", "Fila de Impressão",
            "Só encerre se não usar impressora/fax.", false),
        new("PcaSvc", "Assistência de Compatibilidade de Programas",
            "Aplica correções automáticas de compatibilidade a programas antigos.", false),
        new("DPS", "Serviço de Política de Diagnóstico",
            "Coleta de diagnóstico usada por ferramentas de suporte.", false),
        new("XblAuthManager", "Xbox Live · Autenticação",
            "Só é necessário para jogos e apps do Xbox/Game Pass.", false),
        new("XblGameSave", "Xbox Live · Save na Nuvem",
            "Sincroniza saves do Xbox/Game Pass.", false),
        new("XboxNetApiSvc", "Xbox Live · Rede",
            "Rede dos jogos Xbox/Game Pass para PC.", false),
        new("XboxGipSvc", "Xbox · Acessórios",
            "Controles e acessórios Xbox.", false),
        new("BTAGService", "Bluetooth · Gateway de Áudio",
            "Áudio de fones/caixas Bluetooth.", false),
        new("BthAvctpSvc", "Bluetooth · Áudio/Controle Remoto",
            "Perfis de áudio e controle remoto Bluetooth.", false),
        new("bthserv", "Suporte a Bluetooth",
            "Só encerre se não usar nenhum dispositivo Bluetooth.", false),
        new("TabletInputService", "Teclado de Toque e Escrita Manual",
            "Teclado virtual e painel de entrada; dispensável em desktop sem tela touch.", false),
        new("WbioSrvc", "Serviço Biométrico",
            "Leitor de digital / reconhecimento facial (Windows Hello).", false),
        new("stisvc", "Aquisição de Imagens (WIA)",
            "Scanners e câmeras que usam o Windows Image Acquisition.", false),
        new("SSDPSRV", "Descoberta SSDP",
            "Descoberta de dispositivos UPnP/DLNA na rede.", false),
        new("upnphost", "Host de Dispositivo UPnP",
            "Compartilha e descobre dispositivos UPnP/DLNA.", false),
        new("AJRouter", "Roteador AllJoyn",
            "Rede de dispositivos da Internet das Coisas; quase nunca usado.", false),
        new("SharedAccess", "Compartilhamento de Conexão com a Internet",
            "Compartilha a internet deste PC com outros dispositivos.", false),
        new("icssvc", "Hotspot Móvel",
            "Transforma o PC em ponto de acesso Wi-Fi.", false),
        new("DoSvc", "Otimização de Entrega",
            "Baixa e compartilha atualizações do Windows pela rede. Volta sozinho quando precisa.", false),
    };

    // ------------------------------------------------------------------ lixeira

    /// <summary>Esvazia a lixeira de todas as unidades e devolve quanto espaço foi liberado.</summary>
    public static CleanupReport EmptyRecycleBin()
    {
        long before = RecycleBinBytes();
        int hr = SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 /*NOCONFIRMATION*/ | 0x2 /*NOPROGRESSUI*/ | 0x4 /*NOSOUND*/);
        long after = RecycleBinBytes();
        string? error = hr == 0 ? null : $"o Windows recusou (0x{hr:X8})";
        return new CleanupReport(Math.Max(0, before - after), 0, 0, 0, error);
    }

    private static long RecycleBinBytes()
    {
        long total = 0;
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                string bin = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
                if (Directory.Exists(bin)) total += FolderBytes(bin);
            }
            catch { }
        }
        return total;
    }

    private static long FolderBytes(string root)
    {
        long total = 0;
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            string dir = stack.Pop();
            string[] entries;
            try { entries = Directory.GetFileSystemEntries(dir); }
            catch { continue; }
            foreach (var entry in entries)
            {
                try
                {
                    var attr = File.GetAttributes(entry);
                    if ((attr & FileAttributes.Directory) != 0)
                    {
                        if ((attr & FileAttributes.ReparsePoint) == 0) stack.Push(entry);
                    }
                    else total += new FileInfo(entry).Length;
                }
                catch { }
            }
        }
        return total;
    }

    // ------------------------------------------------------------------ temporários

    /// <summary>
    /// Apaga o conteúdo das pastas temporárias de todos os usuários, do Windows e os caches que se recriam
    /// sozinhos (Windows Update, Otimização de Entrega, shaders de GPU, relatórios de erro, cache de internet).
    /// Arquivos em uso são mantidos — por isso dá para rodar com o PC em uso, sem reiniciar.
    /// O Prefetch não é apagado: ele acelera a abertura de programas e jogos.
    /// </summary>
    public static CleanupReport CleanTemporaryFiles()
    {
        var roots = TempFolders().Where(Directory.Exists).ToList();
        long freed = 0;
        int files = 0, skipped = 0;
        foreach (var root in roots)
        {
            try { CleanDirectory(root, ref freed, ref files, ref skipped); }
            catch { skipped++; }
        }
        return new CleanupReport(freed, files, skipped, roots.Count);
    }

    private static IEnumerable<string> TempFolders()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? path)
        {
            try { if (!string.IsNullOrWhiteSpace(path)) set.Add(Path.GetFullPath(path)); }
            catch { }
        }

        Add(Path.GetTempPath());
        Add(Environment.GetEnvironmentVariable("TEMP"));
        Add(Environment.GetEnvironmentVariable("TMP"));

        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        Add(Path.Combine(windows, "Temp"));
        Add(Path.Combine(windows, @"SoftwareDistribution\Download")); // cache de atualizações baixadas
        Add(Path.Combine(windows, @"Minidump"));
        Add(Path.Combine(windows, @"LiveKernelReports"));

        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        Add(Path.Combine(programData, @"Microsoft\Windows\DeliveryOptimization\Cache"));
        Add(Path.Combine(programData, @"Microsoft\Windows\WER\ReportQueue"));
        Add(Path.Combine(programData, @"Microsoft\Windows\WER\ReportArchive"));

        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Add(Path.Combine(local, "Temp"));
        Add(Path.Combine(local, @"Microsoft\Windows\INetCache"));
        Add(Path.Combine(local, @"Microsoft\Windows\WER\ReportQueue"));
        Add(Path.Combine(local, @"Microsoft\Windows\WER\ReportArchive"));
        Add(Path.Combine(local, "CrashDumps"));
        Add(Path.Combine(local, "D3DSCache"));                        // cache de shaders DirectX
        Add(Path.Combine(local, @"NVIDIA\DXCache"));
        Add(Path.Combine(local, @"NVIDIA\GLCache"));
        Add(Path.Combine(local, @"NVIDIA Corporation\NV_Cache"));
        Add(Path.Combine(local, @"AMD\DxCache"));
        Add(Path.Combine(local, @"AMD\DxcCache"));
        Add(Path.Combine(local, @"AMD\GLCache"));
        Add(Path.Combine(local, @"Intel\ShaderCache"));

        // %TEMP% de todos os perfis de usuário (o app roda como administrador, então consegue limpar todos).
        try
        {
            string users = Path.Combine(Path.GetPathRoot(windows) ?? @"C:\", "Users");
            foreach (var user in Directory.GetDirectories(users))
                Add(Path.Combine(user, @"AppData\Local\Temp"));
        }
        catch { }

        return set;
    }

    /// <summary>Pastas que nunca são limpas: contêm a extração do próprio app e caches de ferramentas em uso.</summary>
    private static bool Excluded(string path)
    {
        string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        return name.Equals(".net", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".dotnet", StringComparison.OrdinalIgnoreCase)
            || name.Equals("ResetExplode", StringComparison.OrdinalIgnoreCase)
            || name.Equals("NuGetScratch", StringComparison.OrdinalIgnoreCase);
    }

    private static void CleanDirectory(string root, ref long freed, ref int files, ref int skipped)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            string dir = stack.Pop();
            string[] entries;
            try { entries = Directory.GetFileSystemEntries(dir); }
            catch { skipped++; continue; }

            foreach (var entry in entries)
            {
                FileAttributes attr;
                try { attr = File.GetAttributes(entry); }
                catch { skipped++; continue; }

                if ((attr & FileAttributes.Directory) != 0)
                {
                    if ((attr & FileAttributes.ReparsePoint) != 0 || Excluded(entry)) continue;
                    stack.Push(entry);
                    continue;
                }

                try
                {
                    long size = new FileInfo(entry).Length;
                    if ((attr & FileAttributes.ReadOnly) != 0) File.SetAttributes(entry, FileAttributes.Normal);
                    File.Delete(entry);
                    freed += size;
                    files++;
                }
                catch { skipped++; } // arquivo em uso: mantido
            }
        }
    }

    // ------------------------------------------------------------------ DNS

    /// <summary>Limpa o cache DNS (resolve nomes de servidores/lobby que mudaram de endereço).</summary>
    public static void FlushDns()
    {
        var psi = new ProcessStartInfo("ipconfig.exe", "/flushdns")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("ipconfig.exe não encontrado.");
        p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit(10000);
    }

    // ------------------------------------------------------------------ serviços

    /// <summary>Lista os serviços opcionais que existem neste PC, já com o estado atual.</summary>
    public static List<OptionalService> ListServices()
    {
        var states = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, State FROM Win32_Service");
            using var results = searcher.Get();
            foreach (ManagementObject mo in results)
            {
                using (mo)
                {
                    if (mo["Name"] is string name && mo["State"] is string state)
                        states[name] = state.Equals("Running", StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch
        {
            // Sem WMI: cai para consultas individuais via sc.exe.
            foreach (var def in ServiceCatalog)
            {
                string output = Run("sc.exe", $"query \"{def.Name}\"");
                if (output.Contains("SERVICE_NAME", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("NOME_DO_SERVIÇO", StringComparison.OrdinalIgnoreCase))
                    states[def.Name] = output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
            }
        }

        var list = new List<OptionalService>();
        foreach (var def in ServiceCatalog)
        {
            if (!states.TryGetValue(def.Name, out bool running)) continue; // serviço não existe nesta edição do Windows
            list.Add(new OptionalService
            {
                Name = def.Name,
                Title = def.Title,
                Detail = def.Detail,
                Running = running,
                Selected = running && def.Recommended,
            });
        }
        return list;
    }

    /// <summary>Pede para encerrar os serviços (voltam ao normal no próximo reinício do Windows).</summary>
    public static ServiceStopResult StopServices(IEnumerable<string> names)
    {
        var list = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var name in list) Run("sc.exe", $"stop \"{name}\"");

        // O Windows encerra de forma assíncrona: espera até 12 s por todos.
        var pending = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
        var sw = Stopwatch.StartNew();
        while (pending.Count > 0 && sw.Elapsed.TotalSeconds < 12)
        {
            foreach (var name in pending.ToList())
                if (!IsRunning(name)) pending.Remove(name);
            if (pending.Count > 0) Thread.Sleep(300);
        }

        return new ServiceStopResult(
            list.Where(n => !pending.Contains(n)).ToList(),
            pending.ToList());
    }

    private static bool IsRunning(string name)
    {
        string output = Run("sc.exe", $"query \"{name}\"");
        return output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
    }

    private static string Run(string file, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args)
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return "";
            string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(15000);
            return output;
        }
        catch (Exception ex) { return ex.Message; }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? root, uint flags);
}
