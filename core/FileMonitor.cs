using System;
using System.IO;
using System.Collections.Concurrent;
using Timer = System.Timers.Timer;

namespace Monitoramento
{
    internal class Program
    {
        // ====== CONFIGURAÇÕES ======
        private static readonly string[] PastasParaMonitorar = new[]
        {
            $@"C:\Users\{Environment.UserName}\Documents",
            $@"C:\Users\{Environment.UserName}\OneDrive\Área de Trabalho",
            //@"D:\RANSOMWHERE\Teste ramsomwhere"
        };

        private static readonly string PastaLogsBase = "Logs";

        private const int AlertaLimite  = 10; // ≥ eventos
        private const int IntervaloSegs = 5;  // em ≤ segundos

        // ====== INFRA ======
        private static readonly ConcurrentQueue<DateTime> eventTimestamps = new();
        private static readonly Timer cronometro = new(1000); // checa a cada 1 s

        static void Main()
        {
            // Cria pasta do dia (ex.: 2025-07-14)
            string pastaDia = Path.Combine(PastaLogsBase, DateTime.Now.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(pastaDia);

            Console.WriteLine("[INFO] Iniciando monitoramento…");

            foreach (var pasta in PastasParaMonitorar)
            {
                if (!Directory.Exists(pasta))
                {
                    Console.WriteLine($"[AVISO] Pasta não encontrada: {pasta}");
                    continue;
                }

                // Arquivo de log exclusivo para essa pasta
                string horaMin = DateTime.Now.ToString("HH-mm");
                string nomeSeguro = SanitizeForFileName(new DirectoryInfo(pasta).Name);
                string logPath = Path.Combine(pastaDia, $"{nomeSeguro}-log-{horaMin}.txt");

                var watcher = new FileSystemWatcher(pasta)
                {
                    IncludeSubdirectories = true,
                    EnableRaisingEvents   = true,
                    NotifyFilter = NotifyFilters.FileName
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Size
                };

                watcher.Created += (s, e) => RegistrarEvento(e, logPath);
                watcher.Changed += (s, e) => RegistrarEvento(e, logPath);
                watcher.Deleted += (s, e) => RegistrarEvento(e, logPath);
                watcher.Renamed += (s, e) => RegistrarRenomeacao(e, logPath);

                Console.WriteLine($"   ↳ Monitorando: {pasta}");
                EscreverLog(logPath, $"INFO: Iniciado monitoramento em {pasta}");
            }

            // Liga o cronômetro de alerta
            cronometro.Elapsed += (_, __) => VerificarAlertas(pastaDia);
            cronometro.Start();

            Console.WriteLine("[INFO] Pressione Enter para sair…");
            Console.ReadLine();
        }

        // ---------- EVENTOS ----------
        private static void RegistrarEvento(FileSystemEventArgs e, string logPath)
        {
            string msg = $"Arquivo {e.ChangeType}: {e.FullPath}";
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
            EscreverLog(logPath, msg);
            eventTimestamps.Enqueue(DateTime.Now);
        }

        private static void RegistrarRenomeacao(RenamedEventArgs e, string logPath)
        {
            string msg = $"Arquivo renomeado de {e.OldFullPath} para {e.FullPath}";
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
            EscreverLog(logPath, msg);
            eventTimestamps.Enqueue(DateTime.Now);
        }

        // ---------- ALERTA ----------
        private static void VerificarAlertas(string pastaDia)
        {
            DateTime agora = DateTime.Now;

            while (eventTimestamps.TryPeek(out DateTime ts) &&
                   (agora - ts).TotalSeconds > IntervaloSegs)
            {
                eventTimestamps.TryDequeue(out _);
            }

            if (eventTimestamps.Count >= AlertaLimite)
            {
                string alerta = $"ALERTA: padrão de ataque detectado! {eventTimestamps.Count} eventos em {IntervaloSegs}s.";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{agora:HH:mm:ss}] {alerta}");
                Console.ResetColor();

                // Grava o alerta em todos os logs do dia
                foreach (var log in Directory.GetFiles(pastaDia, "*-log-*.txt"))
                {
                    EscreverLog(log, alerta);
                }

                eventTimestamps.Clear();
            }
        }

        // ---------- UTILITÁRIOS ----------
        private static void EscreverLog(string caminho, string linha)
        {
            string registro = $"[{DateTime.Now:HH:mm:ss}] {linha}";
            try { File.AppendAllText(caminho, registro + Environment.NewLine); }
            catch (Exception ex) { Console.WriteLine($"[ERRO] Falha ao gravar em {caminho}: {ex.Message}"); }
        }

        private static string SanitizeForFileName(string input)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                input = input.Replace(c, '_');
            return input;
        }
    }
}
