using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace Monitoramento
{
    public class Detection
    {

        public void Detector(string fileWay){
            Console.WriteLine($"Verificando arquivo {fileWay}...");
        }

        public async Task<bool> stability(string caminho, string delay = 100){
            if (!File.Exists(caminho)){
                return false;
            }

            while (true){
                await (Task.Delay(delay)
                try{
                    using var fs = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.None);
                    long tamanho1 = fs.Length;
                    await Task.Delay(50)
                    long tamanho2 = fs.Length;
                    if tamanho1 == tamanho2{
                        return true;
                    }
                }
                catch (IOException){
                    // deu erro → arquivo ainda em uso (instável)
                }
            }
            
        }

    }
}
