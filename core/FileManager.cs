using System;
using System.IO;
using System.Collections.Generic;

namespace Monitoramento
{
    public class FileManager
    {
        // Pastas monitoradas
        private readonly string[] PastasParaMonitorar = new[]
        {
            $@"C:\Users\{Environment.UserName}\Documents",
            $@"C:\Users\{Environment.UserName}\Desktop",
            $@"C:\Users\{Environment.UserName}\Pictures",
            $@"C:\Users\{Environment.UserName}\Downloads",
        };

        // Lista para segurar watchers (evita GC)
        private readonly List<FileSystemWatcher> _watchers = new();

        // Classe de log
        private readonly Notification log = new();

        public void Iniciar()
        {
            Console.WriteLine("[INFO] Iniciando monitoramento de pastas (logger)…");

            foreach (var pasta in PastasParaMonitorar)
            {
                if (!Directory.Exists(pasta))
                {
                    Console.WriteLine($"[AVISO] Pasta não encontrada: {pasta}");
                    continue;
                }

                var watcher = new FileSystemWatcher(pasta)
                {
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.FileName
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Size
                };

                // Apenas registra logs, sem chamar detecção
                watcher.Created += (s, e) => log.SaveChanges($"[LOG] Criado:    {e.FullPath}");
                watcher.Deleted += (s, e) => log.SaveChanges($"[LOG] Deletado:  {e.FullPath}");
                watcher.Changed += (s, e) => log.SaveChanges($"[LOG] Alterado:  {e.FullPath}");
                watcher.Renamed += (s, e) => log.SaveChanges($"[LOG] Renomeado: {e.OldFullPath} -> {e.FullPath}");

                _watchers.Add(watcher);

                Console.WriteLine($"   ↳ Monitorando: {pasta}");
                log.SaveChanges($"[INFO] Iniciado monitoramento em {pasta}");
            }

            Console.WriteLine("[INFO] Pressione Enter para sair…");
            Console.ReadLine();
        }
    }
}