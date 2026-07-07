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
2. **+ → Add package from git URL…**
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

### Android — Google Play Games

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

### iOS — Apple Game Center

**Pré-requisito:** Você PRECISA de uma bridge nativa para chamar `GKLocalPlayer.generateIdentityVerificationSignature`. Duas opções:

**Opção A — Apple.GameKit (recomendado):**
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

**Opção B — Bridge nativa `.mm` (sem Apple.GameKit):**
Criar `Assets/Plugins/iOS/GameCenterBridge.mm` (código no README.md).

### iOS — Sign in with Apple

```csharp
// PASSO 1 — Autenticar com Apple
var credential = await AppleAuthManager.LoginWithAppleId(...);

// PASSO 2 — Vincular ao UGS
var result = await CloudAuth.LinkAppleAsync(credential.IdentityToken);
```

### Eventos de auth

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
- Disparar toasts (Synced, Error, Offline…)
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
- [ ] Auth upgrade configurado (se precisar cross-device)
- [ ] Testado via **Test Window** (Tools → Wagenheimer → Cloud Save → Open Test Window)

---

## 11. Reusable AI audit prompt

Copie e cole este prompt em qualquer IA para que ela analise o projeto e diga exatamente o que já foi feito e o que falta:

```text
Você é um auditor de integração Unity Cloud Save. 
Analise os arquivos do projeto e responda APENAS com uma tabela 
marcando ✅ ou ❌ para cada item abaixo. 
Não explique nada além da tabela.

## Itens a verificar

1. Package instalado:
   - Verificar se "com.wagenheimer.cloudsave" aparece em 
     Packages/manifest.json ou se a pasta Packages/com.wagenheimer.cloudsave existe.
   - Procurar referência ao namespace "Wagenheimer.CloudSave" em 
     qualquer arquivo .cs.

2. CloudSync.Configure:
   - Procurar chamadas a "CloudSync.Configure" em arquivos .cs.
   - Se encontrada, mostrar o argumento (ex: "meu_save").

3. CloudSync.InitAndSyncAsync:
   - Procurar chamadas a "CloudSync.InitAndSyncAsync" em .cs.

4. CloudSync.SaveAsync:
   - Procurar chamadas a "CloudSync.SaveAsync" em .cs.

5. Campo LastSaved:
   - Procurar "long LastSaved" ou "LastSaved" em classes serializáveis 
     (com [System.Serializable]) em .cs.

6. CloudSaveUI.Create:
   - Procurar "CloudSaveUI.Create" em .cs.

7. SyncStatusUI.Create:
   - Procurar "SyncStatusUI.Create" em .cs.

8. CloudAuthUI.Create:
   - Procurar "CloudAuthUI.Create" em .cs.

9. Auth upgrade:
   - Procurar "LinkGooglePlayGamesAsync", "LinkAppleGameCenterAsync", 
     "LinkAppleAsync" em .cs.

10. Projeto dashboard:
    - Procurar arquivos "ProjectSettings.asset" e verificar se contêm 
      "CloudSave" habilitado ou "Unity Services" configurado.
    - Se não conseguir confirmar, marcar como "⚠️ (manual)".

## Formato de saída

| # | Item | Status | Detalhes |
|---|------|--------|----------|
| 1 | Package instalado | ✅ ou ❌ | (opcional: info extra) |
| 2 | CloudSync.Configure | ✅ ou ❌ | "chave_usada" |
| ... | ... | ... | ... |

Se algo for parcial (ex: Configure existe mas InitAndSync não), marque ❌ 
e explique no "Detalhes".
```

---

---

## 12. Logging � o que esperar no console

O pacote loga tudo via Debug.Log / Debug.LogWarning. Procure no console por tags [CloudAuth], [CloudSync], [CloudSave].

| Tag | Quando aparece | Exemplo |
|-----|---------------|---------|
| [CloudAuth] Ready | UGS initialized + anonymous sign-in OK | [CloudAuth] Ready. PlayerId=xxx Provider=Anonymous |
| [CloudAuth] Init failed | Sem internet ou projeto n�o vinculado | [CloudAuth] Init failed: No internet connection |
| [CloudAuth] Linked | Link com GPGS/GameCenter OK | [CloudAuth] Linked: provider=GooglePlayGames PlayerId=xxx |
| [CloudAuth] SignedInExisting | Credencial j� vinculada a outra conta | [CloudAuth] SignedInExisting: provider=Apple |
| [CloudAuth] Link* failed | Link recusado (token inv�lido, etc.) | [CloudAuth] LinkGooglePlayGames failed: ... |
| [CloudSync] Saved to cloud | SaveAsync enviou dados com sucesso | [CloudSync] Saved to cloud. |
| [CloudSync] Save failed | Upload falhou | [CloudSync] Save failed: ... |
| [CloudSync] No cloud save found | InitAndSync � primeiro save (n�o existe nada na nuvem) | [CloudSync] No cloud save found yet. |
| [CloudSync] InitAndSync error | Sync falhou completamente | [CloudSync] InitAndSync error: ... |
| (Debug geral) | UIs, prefabs, etc. | [CloudSave] Prefab generated at ... |

Se **n�o aparecer nenhum log** com [CloudAuth] ou [CloudSync], o c�digo n�o est� chamando as APIs do pacote � rode o **Audit** para confirmar.
