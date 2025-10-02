using System;
using System.IO;
using Monitoramento;

class Program
{
    static void Main(string[] args)
    {
        string user = Environment.UserName;
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        // Downloads não tem SpecialFolder constante universal — montar manual
        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        var folders = new[] { documents, downloads, pictures, desktop };
        Console.WriteLine("Pastas que serão monitoradas:");
        foreach (var f in folders) Console.WriteLine($" - {f}");

        using var det = new RealtimeDetection(folders, windowSeconds: 1, threshold: 8);
        det.Start();

        Console.WriteLine("Pressione ENTER para encerrar o monitor...");
        Console.ReadLine();

        det.Stop();
    }
}