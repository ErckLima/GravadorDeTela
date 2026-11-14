# Gravador de Tela

Gravador de tela simples para Windows 10/11, sem limite de duração ou de tamanho de vídeo.
É um único `GravadorDeTela.exe` de cerca de 1 MB: não precisa instalar.

## Como usar

1. Baixe o `GravadorDeTela.exe` na página de [Releases](../../releases) e abra.
2. Escolha o que gravar:
   - **Tela inteira**, com a opção de não mostrar a barra de tarefas;
   - **Área selecionada**: clique em *Selecionar área...* e arraste na tela, como em um print.
3. Marque ou desmarque **Microfone** e **Som do computador**.
4. Clique em **Gravar**. Use **Pausar / Continuar** e **Parar** quando quiser.

Os vídeos são salvos em MP4 na pasta `Vídeos\Gravações de Tela` (dá para trocar a pasta).

| Atalho | Ação |
|---|---|
| `Ctrl+Alt+F9` | Iniciar / parar a gravação |
| `Ctrl+Alt+F10` | Pausar / continuar |

A janela do gravador não aparece no vídeo, mesmo gravando a tela inteira.

## Requisitos

- Windows 10 (versão 2004 ou mais nova) ou Windows 11, 64 bits.
- Microsoft Visual C++ Redistributable x64. Quase todo computador já tem; se faltar, o
  programa avisa e oferece o [link de download](https://aka.ms/vs/17/release/vc_redist.x64.exe).

## Compilar

Não precisa de Visual Studio nem de SDK: o script usa o compilador C# que já vem no Windows.

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

O executável sai em `dist\GravadorDeTela.exe`.

Para publicar uma versão nova, compile e envie o executável para a página de Releases:

```powershell
gh release create v1.1.0 dist\GravadorDeTela.exe --title "v1.1.0" --notes "O que mudou"
```

## Créditos

A captura e a codificação usam a [ScreenRecorderLib](https://github.com/sskodje/ScreenRecorderLib)
(licença MIT, em `lib/LICENSE-ScreenRecorderLib.txt`), que vai embutida no executável.
