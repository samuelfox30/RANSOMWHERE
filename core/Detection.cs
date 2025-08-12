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

    }
}
