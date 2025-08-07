using System;
using System.IO;
using System.Collections.Concurrent;
using Timer = System.Timers.Timer;

/*Apenas uma nomeclatura que identifica o programa, a identificação em c# não é feita pelo nome do arquivo como no python, mas som por esse namespace */
namespace Monitoramento
{

    /*O c# é uma linguagem orientada a objeto, o que siginfica que todo programa deve estar dentro de uma classe, essa é a classe desse programa*/
    public class FileManager
    {

        /*
        > Variavel do tipo array que armazena os locais de monitoramento <
        private -> Essa variável só pode ser acessada dentro da classe FileManager.
        readonly -> Quer dizer que essa variável só pode ser atribuída uma vez, no momento da criação.
        string[] -> Define que é um array de strings.
        new[] -> define um array
        */
        private readonly string[] PastasParaMonitorar = new[]
        {
            $@"C:\Users\{Environment.UserName}\Documents",
            $@"C:\Users\{Environment.UserName}\OneDrive\Área de Trabalho",
        };

        /*
        Caminho de onde os Logs serão salvos
        O readonly é usado porque o valor não muda depois que for definido.
        Isso ajuda a proteger a estrutura do programa contra mudanças indevidas.
        */
        private readonly string PastaLogsBase = "Logs";
        /*
        Define que, se houver 10 eventos ou mais, em um certo intervalo de tempo, isso será considerado um possível ataque.
        */
        private const int AlertaLimite  = 10;
        /*
        Define o intervalo de tempo (em segundos) onde os eventos são contados.
        Ex: se tiver 10 ou mais eventos em até 5 segundos, dispara um alerta.
        */
        private const int IntervaloSegs = 5;
        /*
        Cria uma fila segura para múltiplas threads (ConcurrentQueue).
        Guarda o horário de cada evento detectado (criado, modificado, etc.).
        Essa fila é usada pelo sistema de alerta pra detectar padrões suspeitos.
        Por que ConcurrentQueue?
        Porque o FileSystemWatcher pode acionar eventos em threads diferentes, e você precisa garantir que a fila não bugue por causa disso.
        */
        private readonly ConcurrentQueue<DateTime> eventTimestamps = new();
        
        private readonly Timer cronometro = new(1000);

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

            cronometro.Elapsed += (_, __) => VerificarAlertas(pastaDia);
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

        private void VerificarAlertas(string pastaDia)
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

                foreach (var log in Directory.GetFiles(pastaDia, "*-log-*.txt"))
                {
                    EscreverLog(log, alerta);
                }

                eventTimestamps.Clear();
            }
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
