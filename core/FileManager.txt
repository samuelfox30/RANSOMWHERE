using System;
using System.IO;
using System.Collections.Concurrent;
using Timer = System.Timers.Timer;

namespace Monitoramento
{
    public class FileManager
    {
        private readonly string[] PastasParaMonitorar = new[]
        {
            $@"C:\Users\{Environment.UserName}\Documents",
            $@"C:\Users\{Environment.UserName}\OneDrive\Área de Trabalho",
        };

        private readonly string PastaLogsBase = "Logs";
        private const int AlertaLimite  = 10;
        private const int IntervaloSegs = 5;

        private readonly ConcurrentQueue<DateTime> eventTimestamps = new();
        private readonly Timer cronometro = new(1000);

        // Instância da classe Detection
        private readonly Detection detection = new(AlertaLimite, IntervaloSegs);

        public void Iniciar()
        {
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

            // Usa a classe Detection para verificar alertas
            cronometro.Elapsed += (_, __) =>
            {
                detection.VerificarAlertas(eventTimestamps, pastaDia, EscreverLog);
            };
            cronometro.Start();

            Console.WriteLine("[INFO] Pressione Enter para sair…");
            Console.ReadLine();
        }

        private void RegistrarEvento(FileSystemEventArgs e, string logPath)
        {
            string msg = $"Arquivo {e.ChangeType}: {e.FullPath}";
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
            EscreverLog(logPath, msg);
            eventTimestamps.Enqueue(DateTime.Now);
        }

        private void RegistrarRenomeacao(RenamedEventArgs e, string logPath)
        {
            string msg = $"Arquivo renomeado de {e.OldFullPath} para {e.FullPath}";
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
            EscreverLog(logPath, msg);
            eventTimestamps.Enqueue(DateTime.Now);
        }

        private void EscreverLog(string caminho, string linha)
        {
            string registro = $"[{DateTime.Now:HH:mm:ss}] {linha}";
            try { File.AppendAllText(caminho, registro + Environment.NewLine); }
            catch (Exception ex) { Console.WriteLine($"[ERRO] Falha ao gravar em {caminho}: {ex.Message}"); }
        }

        private string SanitizeForFileName(string input)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                input = input.Replace(c, '_');
            return input;
        }
    }
}
