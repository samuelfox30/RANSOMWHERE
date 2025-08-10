using System;
using System.IO;
using System.Collections.Generic;

namespace Monitoramento;

public class Notification
{
    private Dictionary<string, string> Inicializacao()
    {
        var ano = DateTime.Now.ToString("yyyy");
        var mes = DateTime.Now.ToString("MM");
        var dia = DateTime.Now.ToString("dd-MM-yyyy");
        var hora = DateTime.Now.ToString("HH-mm-ss");

        var dir = Path.Combine("Logs", ano, mes, dia);
        Directory.CreateDirectory(dir);

        return new Dictionary<string, string>
        {
            ["ano"] = ano,
            ["mes"] = mes,
            ["dia"] = dia,
            ["hora"] = hora,
            ["dir"] = dir
        };
    }

    public void SaveChanges(string dados)
    {
        var init = Inicializacao();
        var caminhoArquivo = Path.Combine(init["dir"], $"events_{init["dia"]}.log");
        File.AppendAllText(caminhoArquivo,
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [COMUM] -> {dados}{Environment.NewLine}");
    }
}
