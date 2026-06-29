# Lineage II Elysian Launcher

Launcher oficial do servidor Lineage II Elysian, feito em Windows Forms para abrir a interface web do servidor, validar o cliente local, baixar o cliente full, aplicar patches incrementais, reparar arquivos quebrados e iniciar o jogo por `system/l2.exe`.

## Visao geral

O launcher trabalha em duas frentes:

- Interface web: abre a URL configurada em `LauncherUrl` dentro do Microsoft WebView2.
- Atualizador: consulta o manifest PHP do servidor, baixa pacotes ZIP, valida hash SHA-256, extrai arquivos e salva a versao local em `version.dat`.

Principais recursos:

- Janela WinForms com WebView2 embutido.
- Comandos do site para `play`, `repair`, `minimize` e `close`.
- Painel injetado na pagina com status do launcher e botoes Jogar/Reparar.
- Verificacao de versao local por `version.dat`.
- Download do cliente full quando o cliente nao existe ou esta muito incompleto.
- Cadeia de patches incrementais.
- Repair inteligente por arquivo usando a lista `files` do manifest.
- Validacao de tamanho e SHA-256.
- Download retomavel quando o servidor aceita HTTP Range.
- Ate 6 tentativas em falhas transientes de rede.
- Cache temporario em `.launcher-cache`.
- Atalho na area de trabalho apos validacao.
- Instancia unica do launcher por mutex.
- Minimizar para a area de notificacao.

## Ambiente

Projeto informado como desenvolvido com Microsoft Visual Studio 2026 e .NET 10.0.

Observacao tecnica importante: o arquivo `LineageII.csproj` deste repositorio esta configurado hoje como projeto WinForms classico com:

```xml
<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
```

Ou seja, a compilacao atual do projeto e .NET Framework 4.8. Existem pacotes 10.0.x no diretorio `packages`, mas isso nao muda o framework alvo do executavel. Se a intencao for migrar de fato para .NET 10.0, o projeto precisa ser convertido para SDK-style e usar algo como `net10.0-windows`, alem de revisar referencias WinForms/WebView2/Fody.

Requisitos praticos para compilar como o projeto esta agora:

- Windows 10/11.
- Microsoft Visual Studio com workload de desenvolvimento desktop .NET.
- .NET Framework 4.8 Developer Pack.
- NuGet Restore habilitado.
- Microsoft WebView2 Runtime instalado no computador do usuario. Se estiver ausente, o launcher tenta baixar e instalar com permissao do usuario.

## Estrutura

```text
App.config                     Configuracoes do launcher, incluindo LauncherUrl.
LineageII.csproj               Projeto principal WinForms.
LineageII.slnx                 Solucao do Visual Studio.
Program.cs                     Entrada do aplicativo e controle de instancia unica.
update.cs                      Tela principal, WebView2, comandos, update, download e repair.
update.Designer.cs             Layout da tela principal.
Controls/                      Controles customizados, como barra de progresso.
Services/LauncherConfig.cs     Leitura das configuracoes do App.config.
Services/Theme.cs              Cores e tema visual.
UI/                            Dialogos auxiliares.
Properties/AssemblyInfo.cs     Nome, produto, versao e metadados do assembly.
Properties/app.manifest        Manifest do Windows.
FodyWeavers.xml                Costura.Fody para empacotar dependencias gerenciadas.
packages/                      Pacotes NuGet restaurados/localizados.
```

## Onde mudar o IP ou dominio do site

O ponto principal e o `LauncherUrl`.

No codigo fonte, altere em `App.config`:

```xml
<add key="LauncherUrl" value="http://localhost" />
```

Exemplos:

```xml
<add key="LauncherUrl" value="http://192.168.0.10" />
```

```xml
<add key="LauncherUrl" value="https://seudominio.com" />
```

```xml
<add key="LauncherUrl" value="https://seudominio.com/index.php" />
```

Quando o projeto e compilado, o `App.config` vira `LineageII.exe.config` na pasta de saida. Em uma pasta ja publicada, voce tambem pode alterar o arquivo ao lado do `.exe`:

```text
LineageII.exe.config
```

Importante:

- Nao coloque `/L2UpdaterWeb/api/manifest.php` dentro de `LauncherUrl`.
- Use somente a base do site ou a pagina inicial do launcher.
- Em producao, prefira `https://`.
- Se usar `https://seudominio.com/index.php`, o WebView abre essa pagina, mas o updater remove `/index.php` para montar a base dos downloads.

Tambem existem fallbacks hardcoded:

```csharp
// update.cs
public static string defaultUrl = "http://localhost";
```

```csharp
// Services/LauncherConfig.cs
string url = ConfigurationManager.AppSettings["LauncherUrl"] ?? "https://l2elysian.com.br/index.php";
```

Na operacao normal, `defaultUrl` e sobrescrito pelo valor do `App.config` durante a criacao da janela principal. Mesmo assim, se voce for trocar definitivamente o dominio do projeto, mantenha esses fallbacks coerentes para evitar surpresa quando o `.config` estiver ausente ou vazio.

## URLs esperadas no servidor

Se o `LauncherUrl` for:

```text
https://seudominio.com
```

O launcher vai buscar o manifest em:

```text
https://seudominio.com/L2UpdaterWeb/api/manifest.php?t=TIMESTAMP
```

O `?t=TIMESTAMP` e adicionado automaticamente para evitar cache.

Se o manifest nao informar `base_url`, os pacotes serao baixados de:

```text
https://seudominio.com/L2UpdaterWeb/api/build
```

Se o manifest informar `base_url`, o launcher usa esse endereco para os pacotes. Isso permite usar outro host ou CDN:

```json
{
  "base_url": "https://cdn.seudominio.com/L2UpdaterWeb/api/build"
}
```

## Hook do site com o launcher

O launcher abre a pagina configurada em `LauncherUrl` dentro do WebView2 e injeta um bridge JavaScript. Esse bridge permite que botoes do site enviem comandos para o aplicativo.

Use atributos HTML:

```html
<button data-launcher-action="repair">Reparar</button>
<button data-launcher-action="play">Jogar</button>
<button data-launcher-action="minimize">Minimizar</button>
<button data-launcher-action="close">Fechar</button>
```

Ou envie manualmente por JavaScript:

```html
<script>
  function launcherAction(action) {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage({ action: action });
    }
  }
</script>

<button onclick="launcherAction('repair')">Reparar</button>
<button onclick="launcherAction('play')">Jogar</button>
```

Comandos aceitos hoje:

```text
play      Inicia o jogo se o cliente estiver validado.
repair    Executa verificacao, update, repair e instalacao do cliente.
minimize  Minimiza a janela.
close     Fecha ou abre o fluxo de fechamento do launcher.
```

O launcher tambem envia estado para a pagina chamando:

```js
window.onLauncherState({
  launcher: true,
  busy: false,
  verified: true,
  status: "Atualizacao concluida.",
  download: "Cliente validado com sucesso.",
  localVersion: "1.0.0"
});
```

O bridge atual ja usa `window.onLauncherState` para manter o painel injetado. Para uma pagina 100% customizada, existem dois caminhos:

- Usar apenas os botoes com `data-launcher-action`, mantendo o painel automatico.
- Editar `GetLauncherBridgeScript()` em `update.cs` para repassar o estado para uma funcao propria do site.

## Manifest do updater

Endpoint padrao:

```text
/L2UpdaterWeb/api/manifest.php
```

Modelo esperado:

```json
{
  "product": "Lineage II Elysian",
  "latest_version": "1.0.1",
  "generated_at": "2026-06-29T12:00:00Z",
  "base_url": "https://seudominio.com/L2UpdaterWeb/api/build",
  "full_package": {
    "version": "1.0.0",
    "file": "full/client_full_1.0.0.zip",
    "size_bytes": 1234567890,
    "hash": "SHA256_DO_ZIP_FULL",
    "file_count": 10000
  },
  "patches": [
    {
      "patch_type": "incremental",
      "from_version": "1.0.0",
      "to_version": "1.0.1",
      "file": "patches/patch_1.0.0_to_1.0.1.zip",
      "size_bytes": 12345678,
      "hash": "SHA256_DO_ZIP_PATCH",
      "file_count": 50,
      "created_at": "2026-06-29T12:00:00Z"
    }
  ],
  "files": [
    {
      "path": "system/l2.exe",
      "hash": "SHA256_DO_ARQUIVO_FINAL",
      "size": 123456,
      "package": "files/system/l2.exe.zip",
      "package_size": 456789,
      "package_hash": "SHA256_DO_ZIP_DO_ARQUIVO",
      "last_version": "1.0.1"
    }
  ]
}
```

Campos principais:

- `latest_version`: versao final desejada.
- `base_url`: base HTTP dos ZIPs. Se vazio, usa `/L2UpdaterWeb/api/build`.
- `full_package`: ZIP completo do cliente.
- `patches`: lista de patches incrementais.
- `files`: lista de arquivos finais para verificacao e repair.

Regras importantes:

- Caminhos devem ser relativos, por exemplo `system/l2.exe`.
- Nao use caminho absoluto.
- Nao use `..`.
- ZIPs devem conter entradas relativas ao diretorio do launcher.
- Hashes devem ser SHA-256.
- Se o hash estiver vazio, aquela validacao de hash e ignorada.
- O servidor deve entregar os ZIPs por HTTP/HTTPS com tamanho correto.
- Para retomada de download, configure o servidor/CDN para aceitar HTTP Range.

## Operacao do download e repair

O botao Reparar executa `RunFullCheck()` em `update.cs`.

Fluxo resumido:

1. Busca o manifest em `defaultUrl + "/L2UpdaterWeb/api/manifest.php?t=" + DateTime.UtcNow.Ticks`.
2. Normaliza manifest, caminhos e hashes.
3. Le a versao local em `version.dat`.
4. Decide se precisa baixar o cliente full.
5. Se nao precisar de full, tenta montar cadeia de patches incrementais de `version.dat` ate `latest_version`.
6. Baixa e extrai cada patch.
7. Verifica os arquivos listados em `files`.
8. Para arquivos ausentes, com tamanho errado ou hash errado, baixa o pacote individual informado em `package`.
9. Faz uma verificacao final.
10. Salva `version.dat` com `latest_version`.
11. Marca o cliente como verificado e libera o botao Jogar.

O launcher baixa para arquivo temporario `.download`. Se a conexao cair, ele tenta continuar de onde parou usando `Range`. Se o servidor nao aceitar retomada, ele reinicia o arquivo temporario.

Configuracoes atuais no codigo:

```csharp
private const int DownloadBufferSize = 1024 * 1024;
private const int MaxDownloadAttempts = 6;
```

O pacote so e considerado valido quando:

- O HTTP retorna sucesso.
- O tamanho final bate com `size_bytes`, quando informado.
- O SHA-256 bate com `hash` ou `package_hash`, quando informado.

Depois da extracao, o ZIP temporario e apagado.

## Como iniciar o jogo

O launcher procura:

```text
system/l2.exe
```

Esse caminho e relativo a pasta onde o launcher esta rodando. Ao clicar Jogar:

- Se o launcher estiver baixando/verificando, o jogo nao inicia.
- Se o cliente nao estiver verificado, o usuario precisa clicar Reparar.
- Se `system/l2.exe` existir e o cliente estiver validado, o launcher executa o jogo e se esconde.

## Build no Visual Studio

1. Abra `LineageII.slnx` no Visual Studio.
2. Restaure os pacotes NuGet.
3. Confira `App.config` e troque `LauncherUrl` para o dominio correto.
4. Selecione `Release` e `AnyCPU`.
5. Compile o projeto.

Saida esperada:

```text
bin/Release/LineageII.exe
bin/Release/LineageII.exe.config
```

Distribua sempre o `.exe` junto com o `.config` gerado e os demais arquivos que o build copiar para a pasta de saida.

## Publicacao recomendada

Antes de entregar para jogadores:

1. Configure `LauncherUrl` para `https://seudominio.com` ou `https://seudominio.com/index.php`.
2. Publique o PHP em `/L2UpdaterWeb/api/manifest.php`.
3. Publique os ZIPs em `/L2UpdaterWeb/api/build` ou no `base_url` do manifest.
4. Teste o manifest no navegador.
5. Teste download full em uma pasta limpa.
6. Teste patch de uma versao antiga para a atual.
7. Teste repair apagando um arquivo do cliente.
8. Assine digitalmente o launcher e o instalador antes de distribuir.

## Aviso do Windows/Microsoft SmartScreen

O launcher ainda nao esta registrado/assinado como aplicativo confiavel para o Windows. Por isso, o Microsoft Defender SmartScreen ou o Windows podem avisar que o aplicativo e desconhecido.

Isso normalmente nao e resolvido por "registrar o nome do app" dentro do codigo. O caminho correto de publicacao e:

1. Comprar/emitir um certificado de assinatura de codigo de uma autoridade certificadora confiavel.
2. Assinar o executavel e o instalador com Authenticode.
3. Usar timestamp na assinatura.
4. Distribuir sempre o mesmo produto assinado pelo mesmo publicador.
5. Publicar em HTTPS.
6. Manter o binario limpo de antivirus e evitar alteracoes suspeitas.

Certificado recomendado:

- OV Code Signing Certificate: comum para empresas/organizacoes.
- EV Code Signing Certificate: costuma ter validacao mais forte e pode ajudar na reputacao inicial, dependendo das politicas atuais do Windows/SmartScreen.

Importante:

- `LineageII_TemporaryKey.pfx` nao substitui um certificado publico de assinatura de codigo.
- Strong name/assinatura de assembly nao e a mesma coisa que assinatura Authenticode do `.exe`.
- `SignAssembly` e `SignManifests` estao `false` no `.csproj`.
- A referencia `Build\Sign-Launcher.ps1` existe no `.csproj`, mas a pasta `Build` e o script nao existem neste repositorio no momento.

Exemplo com SignTool usando certificado instalado no Windows:

```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a "bin\Release\LineageII.exe"
```

Exemplo com arquivo PFX privado:

```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f "C:\Certificados\publisher.pfx" /p "SENHA_DO_PFX" "bin\Release\LineageII.exe"
```

Nao coloque certificado PFX real, senha, token ou chave privada dentro do repositorio.

Se voce gerar um instalador, assine tambem o instalador:

```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a "dist\LineageIIElysianSetup.exe"
```

Links oficiais uteis:

- Microsoft SignTool: https://learn.microsoft.com/en-us/dotnet/framework/tools/signtool-exe
- Microsoft Defender SmartScreen: https://learn.microsoft.com/en-us/windows/security/operating-system-security/virus-and-threat-protection/microsoft-defender-smartscreen/
- Assinatura de manifests no Visual Studio: https://learn.microsoft.com/en-us/visualstudio/ide/how-to-sign-application-and-deployment-manifests

## Licenca do projeto

A licenca do codigo e diferente da assinatura digital do Windows.

Para definir a licenca do projeto, crie um arquivo `LICENSE.md` na raiz. Se o launcher for fechado/proprietario, use termos parecidos com:

```text
Copyright (c) 2026 L2 Elysian.
All rights reserved.

This software is proprietary and may not be copied, modified,
distributed, sold, sublicensed, or used outside the L2 Elysian
project without written permission from the owner.
```

Se quiser abrir o codigo, escolha uma licenca conhecida, como MIT, Apache-2.0 ou GPL. Para um launcher privado de servidor, a opcao proprietaria costuma ser a mais coerente.

Atencao: "Lineage II" e marca da NCSOFT Corporation. O `AssemblyInfo.cs` ja declara essa marca. A licenca do seu launcher nao concede direitos sobre a marca, cliente ou assets da NCSOFT.

## Troubleshooting

Erro no manifest:

- Confirme se `LauncherUrl` esta certo.
- Abra `/L2UpdaterWeb/api/manifest.php` no navegador.
- Verifique se o JSON e valido.
- Confirme se os nomes dos campos batem com o modelo acima.

Download incompleto:

- Confira `size_bytes`.
- Veja se o servidor/CDN esta cortando conexoes longas.
- Habilite HTTP Range para retomada.
- Evite cache quebrado no CDN.

Hash invalido:

- Recalcule o SHA-256 do ZIP publicado.
- Reenvie o arquivo para o servidor.
- Garanta que o PHP/CDN nao esta servindo HTML de erro no lugar do ZIP.

Jogar desabilitado:

- Rode Reparar.
- Confirme se existe `system/l2.exe`.
- Confirme se `version.dat` foi salvo com a versao do manifest.

WebView2 ausente:

- O launcher tenta baixar o instalador oficial da Microsoft.
- Se o usuario recusar permissao, a interface web nao abre.
- Em instaladores profissionais, inclua o WebView2 Runtime como pre-requisito.

Aviso do Windows:

- Assine o `.exe` e o instalador com certificado de assinatura de codigo.
- Use timestamp.
- Distribua por HTTPS.
- Evite publicar builds unsigned para usuarios finais.

## Checklist de producao

- `LauncherUrl` aponta para o dominio final.
- Manifest esta online e retorna JSON valido.
- `base_url` aponta para onde os ZIPs realmente estao.
- Cliente full baixa e extrai em pasta limpa.
- Patch incremental funciona de versao antiga ate `latest_version`.
- Repair baixa pacotes individuais.
- Hashes SHA-256 conferem.
- `system/l2.exe` existe apos update.
- `version.dat` recebe a versao correta.
- WebView2 funciona no computador do usuario.
- Executavel e instalador foram assinados.
- Licenca do projeto foi definida em `LICENSE.md`.
