using System;
using System.Threading;
using System.Threading.Tasks;

namespace Monitoramento
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // CancellationTokenSource para controlar o shutdown
            using var cts = new CancellationTokenSource();

            // 1) inicia FileManager em thread separada (Iniciar bloqueia até ReadLine)
            var fm = new FileManager();
            var fmTask = Task.Run(() => fm.Iniciar());

            // 2) inicia Protection (assíncrono)
            var prot = new Protection();
            var protTask = prot.StartAsync(null, cts.Token); // null -> Documents por padrão

            Console.WriteLine("[MAIN] FileManager e Protection iniciados. Pressione ENTER para encerrar.");

            // espera ENTER do usuário para encerrar
            Console.ReadLine();

            Console.WriteLine("[MAIN] Parando serviços...");
            // pede para proteção parar
            cts.Cancel();
            // se FileManager.Iniciar depende de Console.ReadLine para sair, podemos simular o fim
            // Se FileManager estiver preso em ReadLine, chamar Environment.Exit pode ser necessário, mas
            // ideal é adaptar FileManager para aceitar cancelamento também.
            try
            {
                // se Protection for cancelável, espera terminar
                await protTask.ConfigureAwait(false);
            }
            catch { }

            // opcional: tentar finalizar o FileManager task (se ele terminar com ReadLine, talvez precise de outra abordagem)
            Console.WriteLine("[MAIN] Encerramento solicitado. Saindo.");
        }
    }
}
