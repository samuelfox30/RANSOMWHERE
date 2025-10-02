using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Monitoramento
{
    public class Detection
    {
        private static readonly ConcurrentDictionary<string, int> contadorEventos = new();
        private readonly Protection protector = new();

        public async Task Detector(string fileWay)
        {
            if (string.IsNullOrWhiteSpace(fileWay))
            {
                Console.WriteLine("[WARN] Caminho inválido.");
                return;
            }

            Console.WriteLine($"[Detection] Verificando arquivo {fileWay}...");

            // VERIFICAR EXTENSÃO SUSPEITA
            if (VerificarExtensaoSuspeita(fileWay))
            {
                Console.WriteLine("🚨 Extensão suspeita detectada.");
                await AcionarProtecaoAsync(fileWay);
            }

            // VERIFICAR ENTROPIA
            double entropia = CalcularEntropia(fileWay);
            Console.WriteLine($"[Detection] Entropia do arquivo: {entropia:F4}");
            if (entropia > 5.35)
            {
                Console.WriteLine("⚠️ Alta entropia detectada.");
                await AcionarProtecaoAsync(fileWay);
            }

            // VERIFICAR MODIFICAÇÃO EM MASSA
            string? pasta = Path.GetDirectoryName(fileWay);
            if (!string.IsNullOrEmpty(pasta) && DetectarModificacaoEmMassa(pasta))
            {
                Console.WriteLine("🚨 Modificações em massa detectadas.");
                await AcionarProtecaoAsync(fileWay);
            }
        }

        private double CalcularEntropia(string caminho)
        {
            try
            {
                if (!File.Exists(caminho)) return 0;
                byte[] dados = File.ReadAllBytes(caminho);
                if (dados.Length == 0) return 0;

                int[] freq = new int[256];
                foreach (byte b in dados) freq[b]++;

                double entropia = 0;
                foreach (int f in freq)
                {
                    if (f == 0) continue;
                    double p = (double)f / dados.Length;
                    entropia -= p * Math.Log2(p);
                }
                return entropia;
            }
            catch
            {
                return 0;
            }
        }

        private bool VerificarExtensaoSuspeita(string caminho)
        {
            string[] extensoes = { ".wncry", ".wcry", ".locky", ".crypt", ".enc" };
            string ext = Path.GetExtension(caminho)?.ToLower() ?? "";
            return Array.Exists(extensoes, e => e == ext);
        }

        private bool DetectarModificacaoEmMassa(string diretorio)
        {
            contadorEventos.AddOrUpdate(diretorio, 1, (_, v) => v + 1);
            return contadorEventos[diretorio] > 10;
        }

        private Task AcionarProtecaoAsync(string fileWay)
        {
            return Task.Run(() =>
            {
                try
                {
                    string nomeArquivo = Path.GetFileNameWithoutExtension(fileWay);

                    foreach (var proc in Process.GetProcesses())
                    {
                        try
                        {
                            string? caminhoProc = null;
                            try { caminhoProc = proc.MainModule?.FileName; } catch { }

                            if (!string.IsNullOrEmpty(caminhoProc) &&
                                caminhoProc.EndsWith(nomeArquivo + ".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                protector.Protecao(proc.Id, caminhoProc, proc.ProcessName);
                                return;
                            }
                        }
                        catch { }
                    }

                    Console.WriteLine("[Detection] Nenhum processo suspeito encontrado para encerrar.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Detection] Erro ao acionar proteção: {ex.Message}");
                }
            });
        }
    }
}