// RealtimeDetection.cs
// Requires: Microsoft.Diagnostics.Tracing.TraceEvent (NuGet)
// Run as Administrator

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

namespace Monitoramento
{
    public class RealtimeDetection : IDisposable
    {
        private readonly string[] targetFoldersNormalized; // normalized folders with trailing separator
        private readonly int windowSeconds;
        private readonly int threshold;
        private readonly Protection protector;

        // per-pid queues of timestamps
        private readonly ConcurrentDictionary<int, ConcurrentQueue<DateTime>> pidEvents = new();
        private readonly ConcurrentDictionary<int, bool> pidHandled = new();

        private TraceEventSession session;
        private Task processingTask;
        private CancellationTokenSource cts;

        /// <summary>
        /// Construtor: passe os caminhos das pastas que você quer monitorar.
        /// Ex: new RealtimeDetection(new[] { documents, downloads, pictures, desktop }, 1, 8)
        /// </summary>
        public RealtimeDetection(IEnumerable<string> targetFolders, int windowSeconds = 1, int threshold = 10)
        {
            if (targetFolders == null) throw new ArgumentNullException(nameof(targetFolders));

            var list = new List<string>();
            foreach (var f in targetFolders)
            {
                if (string.IsNullOrWhiteSpace(f)) continue;
                try
                {
                    var nf = Path.GetFullPath(f).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    list.Add(nf);
                }
                catch { /* ignora caminhos inválidos */ }
            }

            if (list.Count == 0) throw new ArgumentException("Nenhuma pasta válida fornecida.");

            this.targetFoldersNormalized = list.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            this.windowSeconds = Math.Max(1, windowSeconds);
            this.threshold = Math.Max(1, threshold);
            this.protector = new Protection();
        }

        public void Start()
        {
            if (!(TraceEventSession.IsElevated() ?? false))
                Console.WriteLine("[WARN] RealtimeDetection: execute como Administrador para garantir visibilidade ETW.");

            cts = new CancellationTokenSource();

            string sessionName = "RansomDetectSession_" + Guid.NewGuid().ToString("N");
            session = new TraceEventSession(sessionName, null);

            session.EnableKernelProvider(
                KernelTraceEventParser.Keywords.FileIOInit |
                KernelTraceEventParser.Keywords.FileIO);

            processingTask = Task.Run(() =>
            {
                try
                {
                    var source = session.Source;

                    source.Kernel.FileIOCreate += data => HandleFileEvent(data.ProcessID, data.FileName, "Create");
                    source.Kernel.FileIOWrite += data => HandleFileEvent(data.ProcessID, data.FileName, "Write");
                    source.Kernel.FileIODelete += data => HandleFileEvent(data.ProcessID, data.FileName, "Delete");
                    source.Kernel.FileIORename += data => HandleFileEvent(data.ProcessID, data.FileName, "Rename");

                    source.Process();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RealtimeDetection] ERRO na session.Process(): {ex}");
                }
            }, cts.Token);

            Console.WriteLine($"[RealtimeDetection] Sessão ETW iniciada. Monitorando:");
            foreach (var f in targetFoldersNormalized) Console.WriteLine($"  - {f}");
            Console.WriteLine($"  window={windowSeconds}s threshold={threshold}");
        }

        private void HandleFileEvent(int pid, string fileName, string op)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName)) return;

                // Normaliza (usa separador do SO)
                string normalized = fileName.Replace('/', Path.DirectorySeparatorChar);

                // Testa se começa com qualquer pasta alvo
                bool match = false;
                foreach (var folder in targetFoldersNormalized)
                {
                    if (normalized.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                    {
                        match = true;
                        break;
                    }
                }
                if (!match) return;

                // Se já tratado, ignora
                if (pidHandled.TryGetValue(pid, out bool handled) && handled) return;

                var q = pidEvents.GetOrAdd(pid, _ => new ConcurrentQueue<DateTime>());
                q.Enqueue(DateTime.UtcNow);

                TrimQueue(q);

                if (q.Count >= threshold)
                {
                    pidHandled[pid] = true;

                    Task.Run(() =>
                    {
                        string pname = "?";
                        string ppath = normalized;
                        try
                        {
                            var proc = Process.GetProcessById(pid);
                            pname = proc.ProcessName;
                            try { ppath = proc.MainModule?.FileName ?? normalized; } catch { }
                        }
                        catch { /* processo pode ter terminado */ }

                        Console.WriteLine($"[RealtimeDetection] ALERT: PID {pid} ({pname}) excedeu threshold ({q.Count} ops em {windowSeconds}s) em pasta monitorada.");
                        try
                        {
                            protector.Protecao(pid, ppath, pname);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[RealtimeDetection] Erro ao chamar Protecao: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RealtimeDetection] Handler error: {ex.Message}");
            }
        }

        private void TrimQueue(ConcurrentQueue<DateTime> q)
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-windowSeconds);
            while (q.TryPeek(out DateTime dt) && dt < cutoff)
            {
                q.TryDequeue(out _);
            }
        }

        public void Stop()
        {
            try
            {
                cts?.Cancel();
                session?.Dispose();
                processingTask?.Wait(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RealtimeDetection] Erro ao parar: {ex.Message}");
            }
            finally
            {
                pidEvents.Clear();
                pidHandled.Clear();
                Console.WriteLine("[RealtimeDetection] Parado.");
            }
        }

        public void Dispose()
        {
            Stop();
            cts?.Dispose();
        }
    }
}