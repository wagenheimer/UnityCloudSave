# CloudSave Integration Guide

Guia completo para ativar `com.wagenheimer.cloudsave` no seu projeto Unity.

---

## Índice

1. [Pré-requisitos](#1-pré-requisitos)
2. [Instalação do Package](#2-instalação-do-package)
3. [Unity Dashboard — Cloud Save habilitado](#3-unity-dashboard--cloud-save-habilitado)
4. [Projeto vinculado aos Unity Services](#4-projeto-vinculado-aos-unity-services)
5. [Código — Setup mínimo](#5-código--setup-mínimo)
6. [UIs (recomendado)](#6-uis-recomendado)
7. [Auth upgrade (Android/iOS)](#7-auth-upgrade-androidios)
8. [Localização](#8-localização)
9. [Testar sem UGS](#9-testar-sem-ugs)
10. [Checklist final](#10-checklist-final)
11. [Reusable AI audit prompt](#11-reusable-ai-audit-prompt)
12. [Logging](#12-logging--o-que-esperar-no-console)

---

## 1. Pré-requisitos

- Unity **2021.3+**
- Um projeto no [Unity Dashboard](https://dashboard.unity3d.com/)
- **Opcional (Android):** Google Play Games Plugin for Unity — `com.google.play.games`
- **Opcional (iOS):** Apple.GameKit ou bridge nativa para GKLocalPlayer

---

## 2. Instalação do Package

### Via Package Manager
1. **Window → Package Manager**
2. **+ → Add package from git URL...**
3. Colar: `https://github.com/wagenheimer/UnityCloudSave.git`

### Via `manifest.json`
```json
{
  "dependencies": {
    "com.wagenheimer.cloudsave": "https://github.com/wagenheimer/UnityCloudSave.git"
  }
}
```

Dependências resolvem automaticamente:
- `com.unity.services.core` 1.12+
- `com.unity.services.authentication` 2.7+
- `com.unity.services.cloudsave` 3.0+
- `com.unity.textmeshpro` 3.0.6+

---

## 3. Unity Dashboard — Cloud Save habilitado

1. Acessar [dashboard.unity3d.com](https://dashboard.unity3d.com/)
2. Selecionar o projeto
3. **Cloud Save** → **Enable**

---

## 4. Projeto vinculado aos Unity Services

1. No Unity: **Edit → Project Settings → Services**
2. Fazer login com a conta Unity
3. Selecionar o projeto (mesmo do Dashboard)
4. Verificar se aparece **Linked** com o projeto correto

> Se não aparecer, usar **Window → Unity Gaming Services** e seguir o fluxo de link.

---

## 5. Código — Setup mínimo

Tudo gira em torno de **duas classes estáticas**: `CloudSync` e `CloudAuth`.

### 5.1 — Seu SaveData precisa de um timestamp

```csharp
[System.Serializable]
public class MeuSaveData
{
    public long LastSaved;  // ← OBRIGATÓRIO — usado pra decidir versão mais nova
    public int Moedas;
    public int Fase;
    // ... seus outros campos
}
```

### 5.2 — Startup (uma vez, no primeiro loading)

```csharp
using Wagenheimer.CloudSave;

public class GameManager : MonoBehaviour
{
    private MeuSaveData _saveData;

    void Start()
    {
        // 1. Configurar a chave do cloud (uma vez)
        CloudSync.Configure("meu_jogo_save");

        // 2. Iniciar sync (fire-and-forget)
        _ = CloudSync.InitAndSyncAsync(_saveData.LastSaved, AplicarCloudSave);

        // 3. (Opcional) Criar UIs
        CloudSaveUI.Create();
        SyncStatusUI.Create();
    }

    private void AplicarCloudSave(byte[] cloudBytes)
    {
        // Chamado quando o cloud save é mais novo que o local
        var json = System.Text.Encoding.UTF8.GetString(cloudBytes);
        _saveData = JsonUtility.FromJson<MeuSaveData>(json);
        // Aplicar ao jogo...
    }
}
```

### 5.3 — Salvar (toda vez que gravar localmente)

```csharp
public void SalvarJogo()
{
    _saveData.LastSaved = System.DateTime.UtcNow.Ticks;
    string json = JsonUtility.ToJson(_saveData);
    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
    System.IO.File.WriteAllBytes(Application.persistentDataPath + "/save.json", bytes);

    // Enviar pro cloud (fire-and-forget)
    _ = CloudSync.SaveAsync(bytes, _saveData.LastSaved);
}
```

### 5.4 — Carregar (antes de iniciar sync)

```csharp
public void CarregarJogo()
{
    string path = Application.persistentDataPath + "/save.json";
    if (System.IO.File.Exists(path))
    {
        byte[] bytes = System.IO.File.ReadAllBytes(path);
        string json = System.Text.Encoding.UTF8.GetString(bytes);
        _saveData = JsonUtility.FromJson<MeuSaveData>(json);
    }
    else
    {
        _saveData = new MeuSaveData();
    }
}
```

### Exemplo completo (Startup)

```csharp
using UnityEngine;
using Wagenheimer.CloudSave;

public class GameManager : MonoBehaviour
{
    [SerializeField] private MeuSaveData _saveData = new();

    void Awake()
    {
        CarregarLocal();
        CloudSync.Configure("meu_jogo");
    }

    void Start()
    {
        CloudSaveUI.Create();
        SyncStatusUI.Create();
        _ = CloudSync.InitAndSyncAsync(_saveData.LastSaved, AplicarCloudSave);
    }

    void CarregarLocal()
    {
        var path = Application.persistentDataPath + "/save.json";
        if (System.IO.File.Exists(path))
        {
            var json = System.IO.File.ReadAllText(path);
            _saveData = JsonUtility.FromJson<MeuSaveData>(json);
        }
    }

    void AplicarCloudSave(byte[] bytes)
    {
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        _saveData = JsonUtility.FromJson<MeuSaveData>(json);
        Debug.Log("Cloud save aplicado!");
    }

    public void Salvar()
    {
        _saveData.LastSaved = System.DateTime.UtcNow.Ticks;
        var json = JsonUtility.ToJson(_saveData);
        System.IO.File.WriteAllText(Application.persistentDataPath + "/save.json", json);
        _ = CloudSync.SaveAsync(
            System.Text.Encoding.UTF8.GetBytes(json),
            _saveData.LastSaved);
    }
}
```

---

## 6. UIs (recomendado)

### CloudSaveUI

Mostra loading overlay, toasts (synced/offline/error), diálogo de conflito.

```csharp
CloudSaveUI.Create();  // idempotente — retorna a singleton
```

### SyncStatusUI

Indicador persistente no canto inferior direito.

```csharp
SyncStatusUI.Create();  // idempotente
```

Estados: `Synced` (verde), `Syncing` (azul), `Offline` (amarelo), `Error` (vermelho).

### CloudAuthUI

Diálogo modal para link de conta (GPGS/Game Center).

```csharp
var auth = CloudAuthUI.Create();
auth.OnLinkRequested += async () =>
{
    var result = await CloudAuth.LinkGooglePlayGamesAsync(serverAuthCode);
    auth.SetLinkResult(result.IsSuccess);
};
auth.Show();
```

### Regenerar prefabs (se mexer no layout)

**Tools → Wagenheimer → Cloud Save → Setup UI Prefabs → All**

---

## 7. Auth upgrade (Android/iOS)

> **IMPORTANTE:** `CloudAuth.Link*` NÃO faz a autenticação com a plataforma.
> Você precisa autenticar no GPGS / Game Center / Apple **primeiro** e só depois passar os tokens.

### 7.1 — Unity Dashboard: configurar os Sign-In Methods

Antes de qualquer código de auth, você precisa avisar o UGS Authentication que vai usar Google Play Games / Apple como provedores de login.

**Passo a passo:**

1. Acessar [dashboard.unity3d.com](https://dashboard.unity3d.com/) → seu projeto
2. **Authentication** (no menu lateral) → **Sign-In Methods**
3. **Anonymous** — já vem ativado por padrão ✔️
4. **Google Play Games** — clicar para ativar
   - Campo **Web client ID**: colar o OAuth 2.0 Web client ID do Google Play Console (explicado na [seção 7.2](#72-google-play-console-android))
5. **Apple Game Center** — ativar (iOS, sem precisar de configuração extra no Dashboard)
6. **Apple** — ativar (Sign in with Apple, iOS)
   - Campos: **Service ID** + **Redirect URL** — obter no Apple Developer

> ⚠️ Se você **não ativar** o provedor no Dashboard, a chamada `AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(code)` vai lançar um erro do tipo "provider not configured". O Audit do pacote inclui essa verificação.

### 7.2 — Google Cloud Console: criar a credencial do tipo certo

⚠️ **Atenção:** O tipo **Android** não funciona aqui. O Client ID do tipo Android não tem Client Secret e não serve para a validação no servidor (UGS). Você precisa criar uma credencial do tipo **Web Application**.

Passo a passo no Google Cloud Console:

1. Acessar [console.cloud.google.com](https://console.cloud.google.com/) → APIs & Services → **Credentials**
2. Clicar em **Create Credentials** → **OAuth client ID**
3. Em **Application type**, selecionar **Web application**
4. Dar um nome (ex: "Unity Play Games Auth Bridge")
5. **Authorized redirect URIs** — deixar em branco por enquanto
6. Clicar em **Create**
7. Copiar o **Client ID** gerado (e opcionalmente o **Client Secret**)

Esse Web Client ID é o que vai no Dashboard da Unity.

> **Por que Web e não Android?** O `AuthenticationService.Instance.LinkWithGooglePlayGamesAsync()` faz a validação do lado do servidor (Unity backend → Google). O tipo Android é só para autenticação direta no dispositivo. O tipo Web tem as chaves necessárias para a troca OAuth 2.0 server-to-server.

### 7.3 — Unity Dashboard: colar o Client ID

1. Acessar [dashboard.unity3d.com](https://dashboard.unity3d.com/) → seu projeto
2. **Authentication** → **Sign-In Methods** → **Google Play Games**
3. Ativar e colar o **Web client ID** (o que você criou na etapa anterior)

### 7.4 — Google Play Console (opcional, para linked apps)

Se o app já estiver cadastrado no Google Play:

1. Acessar [play.google.com/console](https://play.google.com/console/) → seu app
2. **Play Games Services** → **Setup & Management** → **Configuration**
3. Vincular a credencial Web criada acima
4. Adicionar contas de teste se o app não estiver publicado

### 7.5 — Unity: instalar o plugin GPGS

```json
// Packages/manifest.json
{
  "dependencies": {
    "com.google.play.games": "https://github.com/playgameservices/play-games-plugin-for-unity.git"
  }
}
```

Ou via **Window → Package Manager → + → Add package from git URL**.

### 7.6 — Apple Developer (iOS)

#### Game Center

1. [developer.apple.com](https://developer.apple.com/) → **Certificates, Identifiers & Profiles**
2. Selecionar o **App ID** do seu app
3. Ativar **Game Center** capability
4. **Save** e gerar novo perfil de provisionamento
5. No **Unity Dashboard** → Authentication → Sign-In Methods → **Apple Game Center** — ativar (não precisa colar nada)

#### Sign in with Apple (opcional, se for usar `LinkAppleAsync`)

1. No mesmo App ID, ativar **Sign in with Apple**
2. Criar um **Service ID** (identificador para o login)
3. Configurar **Redirect URL** (geralmente `https://{seu-projeto}.unitygameservices.com`)
4. No **Unity Dashboard** → Authentication → Sign-In Methods → **Apple**
   - Colar o **Service ID** e **Redirect URL**

### 7.7 — Android: Google Play Games (código)

Depois de configurar tudo acima, o código é o mesmo — mas agora funciona:

**3 passos obrigatórios:**

1. **Autenticar no GPGS** (`PlayGamesPlatform.Authenticate`)
2. **Solicitar o server auth code** (`RequestServerSideAccess`)
3. **Passar o código pro CloudAuth** (`LinkGooglePlayGamesAsync`)

```csharp
using GooglePlayGames;
using GooglePlayGames.BasicApi;

// PASSO 1 — Autenticar no Google Play Games
PlayGamesPlatform.Instance.Authenticate(status =>
{
    if (status != SignInStatus.Success)
    {
        Debug.LogWarning("GPGS auth failed.");
        return;
    }

    // PASSO 2 — Pegar o server auth code
    PlayGamesPlatform.Instance.RequestServerSideAccess(
        forceRefreshToken: false,
        serverAuthCode =>
        {
            if (string.IsNullOrEmpty(serverAuthCode))
            {
                Debug.LogWarning("Server auth code is null.");
                return;
            }

            // PASSO 3 — Vincular ao Unity Cloud Save (UGS)
            _ = CloudAuth.LinkGooglePlayGamesAsync(serverAuthCode);
        });
});
```

**Logs que você vai ver no console:**
```
[CloudAuth] Ready. PlayerId=xxx Provider=Anonymous       ← antes do link
[CloudAuth] Linked: provider=GooglePlayGames PlayerId=xxx ← depois do link
```

Ver README para exemplo completo com `SignedInExisting`.

### 7.8 — iOS: Apple Game Center (código)

**Pré-requisito:** Instalar o pacote oficial Apple.GameKit (disponível no Package Manager).

```csharp
using Apple.GameKit;

// PASSO 1 — Autenticar no Game Center
var player = GKLocalPlayer.Local;

// PASSO 2 — Pegar a identidade
var (pubKeyUrl, signature, salt, timestamp) =
    await player.FetchItemsForIdentityVerificationSignatureAsync();

// PASSO 3 — Vincular ao Unity Cloud Save (UGS)
var result = await CloudAuth.LinkAppleGameCenterAsync(
    publicKeyUrl : pubKeyUrl,
    signature    : Convert.ToBase64String(signature),
    salt         : Convert.ToBase64String(salt),
    timestamp    : timestamp,
    teamPlayerId : player.TeamPlayerId);

Debug.Log($"Link result: {result.Status}");
```

### 7.9 — iOS: Sign in with Apple (código)

```csharp
// PASSO 1 — Autenticar com Apple
var credential = await AppleAuthManager.LoginWithAppleId(...);

// PASSO 2 — Vincular ao UGS
var result = await CloudAuth.LinkAppleAsync(credential.IdentityToken);
```

### 7.10 — Eventos de auth

```csharp
CloudAuth.OnLinked += provider => Debug.Log($"Linked: {provider}");
CloudAuth.OnAccountSwitched += provider => {
    Debug.Log($"Conta trocada! PlayerId novo: {CloudAuth.PlayerId}");
    _ = CloudSync.InitAndSyncAsync(_saveData.LastSaved, AplicarCloudSave);
};
```

---

## 8. Localização

```csharp
CloudSaveLocale.Translate = key => LocalizationManager.GetTermTranslation(key);
```

Todas as strings usam chaves como `"cloudsave.synced"`. Quando `Translate` é `null`, usa inglês padrão.

---

## 9. Testar sem UGS

**Tools → Wagenheimer → Cloud Save → Open Test Window**

Simula sem precisar de internet ou credenciais:
- Criar/destruir UIs
- Disparar toasts (Synced, Error, Offline...)
- Mostrar diálogo de conflito
- Simular eventos: `OnSyncStarted`, `OnSyncCompleted`, `OnLinked`
- Ver estado de todas as propriedades

---

## 10. Checklist final

- [ ] Package `com.wagenheimer.cloudsave` instalado
- [ ] Cloud Save habilitado no Unity Dashboard
- [ ] Projeto vinculado nos Services (Edit → Project Settings → Services)
- [ ] `CloudSync.Configure("chave")` chamado no startup
- [ ] `CloudSync.InitAndSyncAsync(timestamp, callback)` chamado no startup
- [ ] `CloudSync.SaveAsync(bytes, timestamp)` chamado após cada save local
- [ ] Classe de save tem campo `long LastSaved`
- [ ] `CloudSaveUI.Create()` chamado (se quiser UI)
- [ ] `SyncStatusUI.Create()` chamado (se quiser indicador)
- [ ] Sign-In Methods configurados no Dashboard (Authentication → Sign-In Methods)
- [ ] Google Play Console configurado (OAuth 2.0 Web client ID) + GPGS plugin instalado (se Android)
- [ ] Apple Developer: Game Center ativado no App ID + bridge nativa (se iOS)
- [ ] `CloudAuth.LinkGooglePlayGamesAsync()` / `LinkAppleGameCenterAsync()` chamado (se cross-device)
- [ ] Testado via **Test Window** (Tools → Wagenheimer → Cloud Save → Open Test Window)

---

## 11. Reusable AI audit prompt

Copie e cole este prompt em qualquer IA para que ela analise o projeto e diga exatamente o que já foi feito e o que falta:

```text
You are a Unity Cloud Save integration auditor.
Analyze the project files and output ONLY a markdown table.
Do NOT add explanations beyond the table.

## Checks

1. **Package installed**
   - Check if "com.wagenheimer.cloudsave" in Packages/manifest.json
   - OR package directory exists at Packages/com.wagenheimer.cloudsave/
   - OR any .cs file references "Wagenheimer.CloudSave"

2. **CloudSync.Configure()**
   - Search .cs files for "CloudSync.Configure"
   - If found, note the key used

3. **CloudSync.InitAndSyncAsync()**
   - Search .cs files for "CloudSync.InitAndSyncAsync"

4. **CloudSync.SaveAsync()**
   - Search .cs files for "CloudSync.SaveAsync"

5. **long LastSaved field**
   - Search .cs files for "long LastSaved" in [System.Serializable] classes

6. **[System.Serializable] class**
   - Search .cs files for a [System.Serializable] class for save data

7. **CloudSaveUI.Create()** — Search .cs for "CloudSaveUI.Create()"

8. **SyncStatusUI.Create()** — Search .cs for "SyncStatusUI.Create()"

9. **CloudAuthUI.Create()** — Search .cs for "CloudAuthUI.Create()"
   - Also check if "OnLinkRequested" is wired

10. **Auth upgrade (UGS link calls)**
    - Search .cs for "LinkGooglePlayGamesAsync", "LinkAppleGameCenterAsync", "LinkAppleAsync"
    - Note which platform(s) are configured

11. **Android GPGS plugin**
    - Check Packages/manifest.json for "com.google.play.games"
    - Search Assets/ for GooglePlayGames DLLs or .cs references
    - Search .cs for "PlayGamesPlatform" or "GooglePlayGames"

12. **iOS native bridge**
    - Search Assets/Plugins/iOS/ for .mm files containing "GameCenter" or "GKLocalPlayer"
    - Search .cs for "Apple.GameKit", "GKLocalPlayer", "FetchItemsForIdentityVerification"
    - Search for "LinkAppleGameCenterAsync" or "LinkAppleAsync"

13. **Unity Services**
    - Check ProjectSettings/ProjectSettings.asset for "CloudSave" or "Unity Gaming Services"

## Output format

| # | Item | Status | Files Found | Details/Action Needed |
|---|------|--------|-------------|----------------------|
| 1 | Package installed | ✅ / ❌ / ⚠️ | (paths) | (what to do) |
| ... | ... | ... | ... | ... |
```

---

## 12. Logging — o que esperar no console

O pacote loga tudo via `Debug.Log` / `Debug.LogWarning`. Procure no console por tags `[CloudAuth]`, `[CloudSync]`, `[CloudSave]`.

| Tag | Quando aparece | Exemplo |
|-----|---------------|---------|
| `[CloudAuth] Ready` | UGS initialized + anonymous sign-in OK | `[CloudAuth] Ready. PlayerId=xxx Provider=Anonymous` |
| `[CloudAuth] Init failed` | Sem internet ou projeto não vinculado | `[CloudAuth] Init failed: No internet connection` |
| `[CloudAuth] Linked` | Link com GPGS/GameCenter OK | `[CloudAuth] Linked: provider=GooglePlayGames PlayerId=xxx` |
| `[CloudAuth] SignedInExisting` | Credencial já vinculada a outra conta | `[CloudAuth] SignedInExisting: provider=Apple` |
| `[CloudAuth] Link* failed` | Link recusado (token inválido, etc.) | `[CloudAuth] LinkGooglePlayGames failed: ...` |
| `[CloudSync] Saved to cloud` | SaveAsync enviou dados com sucesso | `[CloudSync] Saved to cloud.` |
| `[CloudSync] Save failed` | Upload falhou | `[CloudSync] Save failed: ...` |
| `[CloudSync] No cloud save found` | InitAndSync — primeiro save (não existe nada na nuvem) | `[CloudSync] No cloud save found yet.` |
| `[CloudSync] InitAndSync error` | Sync falhou completamente | `[CloudSync] InitAndSync error: ...` |
| (Debug geral) | UIs, prefabs, etc. | `[CloudSave] Prefab generated at ...` |

Se **não aparecer nenhum log** com `[CloudAuth]` ou `[CloudSync]`, o código não está chamando as APIs do pacote — rode o **Audit** (Tools → Wagenheimer → Cloud Save → Audit Integration) para confirmar.
