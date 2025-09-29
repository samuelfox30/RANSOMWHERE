using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Monitoramento
{
    public class Protection
    {
        // ---------- CONFIGURAÇÃO ----------
        readonly string[] SuspiciousNameParts = new[] {
            "ransom", "crypt", "encrypt", "locker", "evil", "payload", "wannacry", "locky"
        };

        readonly string[] Whitelist = new[] {
            "explorer", "cmd", "powershell", "pwsh", "devenv", "code", "svchost", "system", "lsass", "conhost", "onedrive", "backup"
        };

        // Janela curta para demo
        readonly int WindowSeconds = 1;      // 1 segundo
        readonly int ThresholdEvents = 3;    // 3 eventos em 1 segundo -> alerta rápido

        // Extensões que disparam resposta imediata ao renomear
        readonly string[] SuspiciousExtensions = new[] { ".locked", ".encrypted", ".wncry", ".wnry", ".crypt", ".enc" };

        // fila de eventos (timestamp + caminho)
        readonly ConcurrentQueue<(DateTime ts, string path)> events = new();
        CancellationTokenSource internalCts = new();

        public Protection()
        {
        }

        /// <summary>
        /// Inicia a proteção de forma assíncrona. Retorna uma Task que completa quando o loop é cancelado.
        /// </summary>
        /// <param name="pathToMonitor">Pasta a monitorar (se null, usa Documents do usuário)</param>
        /// <param name="externalToken">Token externo para cancelamento</param>
        public async Task StartAsync(string? pathToMonitor = null, CancellationToken externalToken = default)

        {
            // cria CTS ligado ao token externo para poder cancelar internamente
            internalCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            var token = internalCts.Token;

            string dir = string.IsNullOrWhiteSpace(pathToMonitor)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : pathToMonitor;

            if (!Directory.Exists(dir))
            {
                Console.WriteLine($"[PROT] Pasta não encontrada: {dir}");
                return;
            }

            Console.WriteLine($"[PROT][INFO] Monitorando: {dir}");
            Console.WriteLine($"[PROT][INFO] Window={WindowSeconds}s Threshold={ThresholdEvents}");

            using var watcher = new FileSystemWatcher(dir)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
            };

            watcher.Changed += OnFsEvent;
            watcher.Created += OnFsEvent;
            watcher.Deleted += OnFsEvent;
            watcher.Renamed += OnRenamed;

            // roda o loop de monitoramento até token ser cancelado
            try
            {
                await MonitorLoop(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROT][ERRO] MonitorLoop: {ex.Message}");
            }
            finally
            {
                watcher.EnableRaisingEvents = false;
                Console.WriteLine("[PROT][INFO] Proteção finalizada.");
            }
        }

        /// <summary>
        /// Solicita parada da proteção.
        /// </summary>
        public void Stop()
        {
            try
            {
                internalCts?.Cancel();
            }
            catch { }
        }

        // ----------------- Handlers -----------------

        void OnFsEvent(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (Directory.Exists(e.FullPath)) return;
                events.Enqueue((DateTime.UtcNow, e.FullPath));
                Console.WriteLine($"[PROT][FS] {e.ChangeType}: {e.FullPath}");
            }
            catch { /* ignora */ }
        }

        void OnRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                if (Directory.Exists(e.FullPath)) return;
                events.Enqueue((DateTime.UtcNow, e.FullPath));
                Console.WriteLine($"[PROT][FS] Renamed: {e.OldFullPath} -> {e.FullPath}");

                // Resposta imediata se a nova extensão for suspeita
                string ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
                if (SuspiciousExtensions.Contains(ext))
                {
                    Console.WriteLine($"[PROT][FAST-ALERT] Renomeação para extensão suspeita detectada: {ext}. Reagindo imediatamente.");
                    _ = Task.Run(async () => await ImmediateResponseAsync(e.FullPath));
                }
            }
            catch { /* ignora */ }
        }

        // ----------------- Monitor Loop -----------------

        async Task MonitorLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    TrimOldEvents();
                    int count = events.Count;

                    if (count >= ThresholdEvents)
                    {
                        // coletar caminhos únicos afetados na janela
                        var altered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        while (events.TryDequeue(out var item))
                        {
                            altered.Add(item.path);
                        }

                        Console.WriteLine($"\n[PROT][ALERTA] Detectados {count} eventos em {WindowSeconds}s → {altered.Count} arquivos afetados. Ação imediata iniciada.");

                        // Resposta: scoring curto e suspensão do topo
                        var candidates = await ScoreProcessesAsync(sampleIntervalMs: 200);

                        if (candidates.Count == 0)
                        {
                            Console.WriteLine("[PROT][ALERTA] Nenhum processo candidato detectado.");
                        }
                        else
                        {
                            Console.WriteLine("[PROT][ALERTA] Processos candidatos:");
                            foreach (var c in candidates)
                            {
                                Console.WriteLine($"  PID:{c.ProcessId} Name:{c.Name} Score:{c.Score:F1} CPU%:{c.CpuPercent:F2} Threads:{c.Threads}");
                            }

                            var top = candidates.FirstOrDefault();
                            if (top != null && top.Score >= 3.5)
                            {
                                Console.WriteLine($"[PROT][ALERTA] Tentando SUSPENDER PID {top.ProcessId} ({top.Name}) — score {top.Score:F1}");
                                if (TrySuspendProcess(top.ProcessId))
                                    Console.WriteLine($"[PROT][ALERTA] PID {top.ProcessId} SUSPENSO com sucesso.");
                                else
                                    Console.WriteLine($"[PROT][ALERTA] Falha ao suspender PID {top.ProcessId}.");
                            }
                            else
                            {
                                Console.WriteLine("[PROT][ALERTA] Nenhum candidato com score alto o suficiente para suspensão automática.");
                            }
                        }

                        Console.WriteLine("[PROT][ALERTA] Aguardando próximo ciclo...\n");
                    }

                    await Task.Delay(250, token); // checagem rápida
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PROT][ERRO] Loop monitor: {ex.Message}");
                }
            }
        }

        void TrimOldEvents()
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-WindowSeconds);
            while (events.TryPeek(out var item) && item.ts < cutoff)
                events.TryDequeue(out _);
        }

        // ----------------- Processo scoring -----------------

        class Candidate
        {
            public int ProcessId;
            public string Name = "";
            public double Score;
            public double CpuPercent;
            public int Threads;
            public bool SuspiciousNameMatch;
        }

        async Task<List<Candidate>> ScoreProcessesAsync(int sampleIntervalMs = 200)
        {
            var procs = Process.GetProcesses();
            var snapshot = new List<(Process p, TimeSpan t0)>();
            foreach (var p in procs)
            {
                try { snapshot.Add((p, p.TotalProcessorTime)); }
                catch { /* ignore */ }
            }

            await Task.Delay(sampleIntervalMs);

            var list = new List<Candidate>();
            foreach (var (p, t0) in snapshot)
            {
                try
                {
                    var t1 = p.TotalProcessorTime;
                    var deltaMs = (t1 - t0).TotalMilliseconds;
                    var cpuPercent = (deltaMs / Math.Max(1, sampleIntervalMs)) * 100.0 / Math.Max(1, Environment.ProcessorCount);

                    var name = p.ProcessName.ToLowerInvariant();
                    if (Whitelist.Contains(name)) continue;

                    bool nameMatch = SuspiciousNameParts.Any(s => name.Contains(s));
                    double score = 0;
                    if (nameMatch) score += 4.0;
                    if (cpuPercent > 5.0) score += Math.Min(4.0, (cpuPercent - 5.0) / 5.0);
                    int threads = p.Threads?.Count ?? 0;
                    if (threads > 20) score += 1.0;
                    if (threads > 50) score += 1.0;
                    try
                    {
                        var age = DateTime.UtcNow - p.StartTime.ToUniversalTime();
                        if (age.TotalSeconds < 60) score += 0.8;
                    }
                    catch { }

                    if (score > 0.5)
                    {
                        list.Add(new Candidate
                        {
                            ProcessId = p.Id,
                            Name = name,
                            Score = score,
                            CpuPercent = cpuPercent,
                            Threads = threads,
                            SuspiciousNameMatch = nameMatch
                        });
                    }
                }
                catch { /* ignore protected processes */ }
            }

            return list.OrderByDescending(x => x.Score).Take(10).ToList();
        }

        // ----------------- Resposta imediata -----------------

        async Task ImmediateResponseAsync(string samplePath)
        {
            try
            {
                var candidates = await ScoreProcessesAsync(sampleIntervalMs: 200);

                if (candidates.Count == 0)
                {
                    Console.WriteLine("[PROT][FAST] Nenhum candidato detectado com sample rápido.");
                    return;
                }

                foreach (var c in candidates)
                    Console.WriteLine($"[PROT][FAST] Candidate PID:{c.ProcessId} {c.Name} Score:{c.Score:F1} CPU:{c.CpuPercent:F2}");

                var top = candidates.FirstOrDefault();
                if (top != null && top.Score >= 3.5)
                {
                    Console.WriteLine($"[PROT][FAST] Tentando suspender top PID {top.ProcessId} ({top.Name})");
                    if (TrySuspendProcess(top.ProcessId))
                        Console.WriteLine($"[PROT][FAST] PID {top.ProcessId} suspenso com sucesso.");
                    else
                        Console.WriteLine($"[PROT][FAST] Falha ao suspender PID {top.ProcessId}.");
                }
                else
                {
                    Console.WriteLine("[PROT][FAST] Nenhum candidato com score alto suficiente para suspensão imediata.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROT][FAST] Erro em ImmediateResponse: {ex.Message}");
            }
        }

        // ----------------- Suspend / Resume / Kill -----------------
        // Usa NtSuspendProcess / NtResumeProcess via P/Invoke (funciona em muitos casos; requer privilégios em outros)
        [DllImport("ntdll.dll", SetLastError = true)]
        static extern int NtSuspendProcess(IntPtr processHandle);

        [DllImport("ntdll.dll", SetLastError = true)]
        static extern int NtResumeProcess(IntPtr processHandle);

        bool TrySuspendProcess(int pid)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                IntPtr h = p.Handle;
                int rc = NtSuspendProcess(h);
                return rc == 0;
            }
            catch { return false; }
        }

        bool TryResumeProcess(int pid)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                IntPtr h = p.Handle;
                int rc = NtResumeProcess(h);
                return rc == 0;
            }
            catch { return false; }
        }

        public void TryKillProcess(int pid)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                Console.WriteLine($"[PROT] Tentando encerrar PID {pid} ({p.ProcessName}) ...");
                p.Kill(true);
                p.WaitForExit(3000);
                Console.WriteLine($"[PROT] PID {pid} encerrado.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROT] Falha ao encerrar PID {pid}: {ex.Message}");
            }
        }
    }
}
