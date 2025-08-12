using System;

namespace Monitoramento
{
    internal class Program
    {
        static void Main()
        {
            FileManagerTest fm = new();
            fm.Iniciar();
            //Notification nf = new();
            //nf.Iniciar();
        }
    }
}