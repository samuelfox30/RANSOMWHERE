using System;
using System.Collections.Concurrent;
using System.IO;

namespace Monitoramento
{
    public class Detection
    {
        private readonly int alertaLimite;
        private readonly int intervaloSegs;

        public Detection(int alertaLimite, int intervaloSegs)
        {
            this.alertaLimite = alertaLimite;
            this.intervaloSegs = intervaloSegs;
        }

        public void VerificarAlertas(ConcurrentQueue<DateTime> eventTimestamps, string pastaDia, Action<string, string> escreverLog)
        {
            DateTime agora = DateTime.Now;

            while (eventTimestamps.TryPeek(out DateTime ts) &&
                   (agora - ts).TotalSeconds > intervaloSegs)
            {
                eventTimestamps.TryDequeue(out _);
            }

            if (eventTimestamps.Count >= alertaLimite)
            {
                string alerta = $"ALERTA: padrão de ataque detectado! {eventTimestamps.Count} eventos em {intervaloSegs}s.";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{agora:HH:mm:ss}] {alerta}");
                Console.ResetColor();

                foreach (var log in Directory.GetFiles(pastaDia, "*-log-*.txt"))
                {
                    escreverLog(log, alerta);
                }

                eventTimestamps.Clear();
            }
        }
    }
}
