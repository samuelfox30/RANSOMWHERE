using System;
using System.Diagnostics;

namespace Monitoramento
{
    public class Protection
    {
        // Whitelist básica para não encerrar processos críticos do Windows
        private static readonly string[] systemWhiteList = new[]
        {
            "system", "idle", "lsass", "svchost", "explorer", "wininit",
            "winlogon", "services", "csrss", "smss", "taskhostw", "wuauclt"
        };

        /// <summary>
        /// Encerra um processo malicioso pelo PID e nome.
        /// Chamado pelo Detection.cs
        /// </summary>
        public void Protecao(int pid, string caminho, string nome)
        {
            try
            {
                // Não matar se for da whitelist
                if (Array.Exists(systemWhiteList, w => string.Equals(w, nome, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine($"[Protection] Processo {nome} (PID: {pid}) está na whitelist. Ignorando.");
                    return;
                }

                var proc = Process.GetProcessById(pid);
                if (proc == null || proc.HasExited)
                {
                    Console.WriteLine($"[Protection] Processo {nome} (PID: {pid}) já finalizado.");
                    return;
                }

                Console.WriteLine($"[Protection] Encerrando processo suspeito {nome} (PID: {pid})...");

                // Tenta fechar de forma limpa
                try
                {
                    proc.CloseMainWindow();
                    if (proc.WaitForExit(2000))
                    {
                        Console.WriteLine($"[Protection] Processo {nome} (PID: {pid}) fechado graciosamente.");
                        return;
                    }
                }
                catch { /* se não der, força */ }

                // Força o kill
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(3000);

                Console.WriteLine($"[Protection] Processo {nome} (PID: {pid}) encerrado à força.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Protection] Erro ao tentar encerrar {nome} (PID: {pid}): {ex.Message}");
            }
        }
    }
}