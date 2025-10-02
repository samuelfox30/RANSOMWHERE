import tkinter as tk
from tkinter import ttk
from tkinter.scrolledtext import ScrolledText
import subprocess
import threading
import queue
import os

EXE_PATH = r"D:\ransomwhere\RANSOMWHERE\RANSOMWHERE\bin\Release\net9.0\win-x64\RANSOMWHERE.exe"

# ======= Função para cores de log =======
def cor_log(linha):
    if "[ERRO" in linha:
        return "red"
    elif "[INFO" in linha:
        return "blue"
    elif "[COMUM]" in linha:
        return "green"
    else:
        return "black"

class FrufruApp:
    def __init__(self, root):
        self.root = root
        root.title("🛡 RANSOMWHERE Monitor")
        root.geometry("900x500")
        root.configure(bg="#1e1e2f")  # fundo escuro

        # Label título
        titulo = tk.Label(root, text="RANSOMWHERE Monitor", font=("Consolas", 20, "bold"),
                          bg="#1e1e2f", fg="#ffcc00")
        titulo.pack(pady=10)

        # Text area com scroll
        self.logbox = ScrolledText(root, state="disabled", height=20, wrap="none",
                                   font=("Consolas", 11), bg="#2b2b3c", fg="white", insertbackground="white")
        self.logbox.pack(fill="both", expand=True, padx=12, pady=6)

        # Frame para botões
        frame = tk.Frame(root, bg="#1e1e2f")
        frame.pack(fill="x", padx=12, pady=6)

        style = ttk.Style()
        style.configure("TButton", font=("Consolas", 12), padding=6)

        self.btn_start = ttk.Button(frame, text="▶ Iniciar", command=self.start_process)
        self.btn_start.pack(side="left", padx=6)
        self.btn_stop = ttk.Button(frame, text="■ Parar", command=self.stop_process, state="disabled")
        self.btn_stop.pack(side="left", padx=6)
        self.btn_clear = ttk.Button(frame, text="🧹 Limpar", command=self.clear)
        self.btn_clear.pack(side="left", padx=6)

        # fila para comunicação
        self.q = queue.Queue()
        self.proc = None
        self.reader_thread = None
        self.poll_queue()

    def append_text(self, linha):
        cor = cor_log(linha)
        self.logbox.configure(state="normal")
        self.logbox.insert("end", linha, cor)
        self.logbox.tag_config(cor, foreground=cor)
        self.logbox.see("end")
        self.logbox.configure(state="disabled")

    def poll_queue(self):
        try:
            while True:
                linha = self.q.get_nowait()
                if linha == "__PROC_DONE__":
                    self.proc = None
                    self.btn_start.config(state="normal")
                    self.btn_stop.config(state="disabled")
                else:
                    self.append_text(linha)
        except queue.Empty:
            pass
        self.root.after(100, self.poll_queue)

    def reader(self, popen):
        try:
            for raw in iter(popen.stdout.readline, ""):
                if raw == "":
                    break
                self.q.put(raw)
        except Exception as e:
            self.q.put(f"[ERRO NA LEITURA] {e}\n")
        finally:
            popen.stdout.close()
            ret = popen.wait()
            self.q.put(f"\n[PROCESSO FINALIZADO] código: {ret}\n")
            self.q.put("__PROC_DONE__")

    def start_process(self):
        if not os.path.exists(EXE_PATH):
            self.append_text(f"[ERRO] .exe não encontrado em: {EXE_PATH}\n")
            return
        if self.proc:
            self.append_text("[INFO] Processo já está rodando.\n")
            return
        try:
            self.proc = subprocess.Popen(
                [EXE_PATH],
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                bufsize=1,
                universal_newlines=True
            )
        except Exception as e:
            self.append_text(f"[ERRO] Falha ao iniciar: {e}\n")
            self.proc = None
            return

        self.reader_thread = threading.Thread(target=self.reader, args=(self.proc,), daemon=True)
        self.reader_thread.start()
        self.btn_start.config(state="disabled")
        self.btn_stop.config(state="normal")
        self.append_text("[INFO] Processo iniciado.\n")

    def stop_process(self):
        if not self.proc:
            self.append_text("[INFO] Nenhum processo em execução.\n")
            return
        try:
            self.proc.terminate()
            self.append_text("[INFO] Solicitado encerramento do processo...\n")
            threading.Timer(5.0, self._kill_if_alive).start()
        except Exception as e:
            self.append_text(f"[ERRO] ao tentar terminar: {e}\n")

    def _kill_if_alive(self):
        if self.proc and self.proc.poll() is None:
            try:
                self.proc.kill()
                self.append_text("[INFO] Processo morto forçadamente.\n")
            except Exception as e:
                self.append_text(f"[ERRO] kill: {e}\n")

    def clear(self):
        self.logbox.configure(state="normal")
        self.logbox.delete("1.0", "end")
        self.logbox.configure(state="disabled")


if __name__ == "__main__":
    root = tk.Tk()
    app = FrufruApp(root)
    root.mainloop()
