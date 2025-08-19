using System; //da acesso ao console e eventArgs
using System.IO; //da acesso ao FileSystem Whatcher + escrever em arquivos

namespace Monitoramento;

class FileManagerTest
{

    // Vetor que armazena locais de monitoramento
    private readonly string[] PastasParaMonitorar = new[]{
        $@"C:\Users\{Environment.UserName}\Documents"
    };

    // Mantém os watchers vivos (evita GC)
    private readonly List<FileSystemWatcher> _watchers = new();

    public void Iniciar()
    {

        //contador();

        Notification log = new();
        Detection detect = new();

        //  Para cada item do vetor, realizar a ativação de monitoramento
        foreach (var pasta in PastasParaMonitorar){
            if (!Directory.Exists(pasta)){
                Console.WriteLine($"[AVISO] Pasta não encontrada: {pasta}");
                continue;
            }
            var eyes = new FileSystemWatcher(pasta)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };
            //“Quando acontecer tal evento, execute tal código.”
            eyes.Created += (s, e) => log.SaveChanges($"Criado:    {e.FullPath}");
            eyes.Deleted += (s, e) => log.SaveChanges($"Deletado:  {e.FullPath}");
            eyes.Changed += (s, e) => RegistrarEAnalizar($"Alterado:  {e.FullPath}", e.FullPath);
            eyes.Renamed += (s, e) => log.SaveChanges($"Renomeado: {e.OldFullPath} -> {e.FullPath}");

            _watchers.Add(eyes);
        }

        void RegistrarEAnalizar(string mensagem, string caminho){
            log.SaveChanges(mensagem);
            detect.Detector(caminho);
        }

        Console.ReadLine();
    }

    private void contador(){
        int i = 0;
        while(i > -1){
            Console.WriteLine(i);
            Thread.Sleep(1);
            i++;   
        }
    }

}