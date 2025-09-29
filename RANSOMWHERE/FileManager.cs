using System;
using System.IO;
using System.Collections.Generic;

namespace Monitoramento
{
    public class FileManager
    {
        // Pastas que serão monitoradas
        private readonly string[] PastasParaMonitorar = new[]
        {
            $@"C:\Users\{Environment.UserName}\Documents",
            $@"C:\Users\{Environment.UserName}\OneDrive\Área de Trabalho",
        };

        // Lista para segurar watchers (evita que sejam coletados pelo GC)
        private readonly List<FileSystemWatcher> _watchers = new();

        // Dependências externas (classes auxiliares)
        private readonly Notification log = new();
        private readonly Detection detect = new();

        public void Iniciar()
        {
            Console.WriteLine("[INFO] Iniciando monitoramento…");

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

                // Eventos monitorados
                watcher.Created += (s, e) => log.SaveChanges($"Criado:    {e.FullPath}");
                watcher.Deleted += (s, e) => log.SaveChanges($"Deletado:  {e.FullPath}");
                watcher.Changed += (s, e) => RegistrarEAnalisar($"Alterado:  {e.FullPath}", e.FullPath);
                watcher.Renamed += (s, e) => log.SaveChanges($"Renomeado: {e.OldFullPath} -> {e.FullPath}");

                _watchers.Add(watcher);

                Console.WriteLine($"   ↳ Monitorando: {pasta}");
                log.SaveChanges($"INFO: Iniciado monitoramento em {pasta}");
            }

            Console.WriteLine("[INFO] Pressione Enter para sair…");
            Console.ReadLine();
        }

        private void RegistrarEAnalisar(string mensagem, string caminho)
        {
            log.SaveChanges(mensagem);
            detect.Detector(caminho);
        }
    }
}
