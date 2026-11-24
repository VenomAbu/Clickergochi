# Anúncio recompensado → Golden Pack

## O que já está pronto na MainScene

`AD_Btt` → `AdConfirmationButton` abre o `PopUpConfirmTV`. Ao confirmar, o
`AdRewardGoldenPackFlow` guarda a sala horizontal atual, pede um anúncio
recompensado ao `RewardedAdBridge` e, se o anúncio for assistido até o fim,
abre o PackUI e mostra um **Pacote Golden** pulsando. A abertura **só começa
quando o jogador tocar na tela**, igual ao fluxo normal. Ao sair do sticker, a
cena volta para a sala guardada (Room / Room (1) / Room (2)), não para o PackUI.

Não há SDK de anúncios instalado neste projeto. Por isso o bridge usa um
**anúncio simulado no Editor** (só um log + espera de 2,5s). Em builds ele não
finge recompensa: retorna `Unavailable` a menos que
`SimulatedRewardedAdProvider.Instance.allowInBuilds` seja marcado.

## Testar sem SDK

1. Abrir `MainScene` e dar Play.
2. Clicar em `AD_Btt` → confirmar no `PopUpConfirmTV`.
3. Aparece o log `[Ads] Anuncio simulado ...` e, depois de ~2,5s, o Golden Pack
   aparece pulsando/levitando — ainda **não** abre.
4. Tocar na tela: o pacote abre e revela o sticker.
5. Clicar na área/botão de sair do sticker: volta à sala onde estava.

Para acelerar o teste, no Console/Editor troque
`SimulatedRewardedAdProvider.Instance.durationSeconds` para `0.1f`. Para testar
os caminhos sem recompensa, mude `.result` para `Skipped` ou `Failed`.

## Plugar um SDK real (3 passos)

### 1. Instale o SDK

Exemplos: Unity LevelPlay (IronSource), Google AdMob, Unity Ads/Mediation,
AppLovin MAX. Use o Package Manager ou o pacote oficial do provedor. Confira
também os IDs de Android/iOS no painel do provedor.

### 2. Crie um adaptador implementando `IRewardedAdProvider`

Crie um script em `Assets/_Project/Scripts/Ads/` (ex.:
`LevelPlayRewardedProvider.cs`) seguindo este molde:

```csharp
using System;
using Clickergochi.Ads;
// using com.unity.ads; // namespace real do SDK escolhido

public sealed class MeuRewardedProvider : IRewardedAdProvider
{
    public string DisplayName => "Meu SDK";
    public bool IsReady => /* true quando o anúncio estiver carregado */;

    public void Show(string placementId, Action<RewardedAdResult> onComplete)
    {
        // Chame o Show do SDK e, no callback dele, traduza o resultado:
        //   assistiu até o fim  -> onComplete?.Invoke(RewardedAdResult.Watched);
        //   fechou antes        -> onComplete?.Invoke(RewardedAdResult.Skipped);
        //   erro de rede/carga  -> onComplete?.Invoke(RewardedAdResult.Failed);
        //
        // Garanta que onComplete seja chamado UMA ÚNICA vez.
    }
}
```

Exemplo conceitual com Unity LevelPlay (a API exata depende da versão do SDK):

```csharp
public void Show(string placementId, Action<RewardedAdResult> onComplete)
{
    bool done = false;
    void Finish(RewardedAdResult r)
    {
        if (done) return;
        done = true;
        onComplete?.Invoke(r);
    }

    // RewardedAd.Show() com listeners de fechamento:
    // onAdRewarded  -> Finish(RewardedAdResult.Watched)
    // onAdClosed    -> Finish(RewardedAdResult.Skipped)  // se ainda não deu reward
    // onAdShowFail  -> Finish(RewardedAdResult.Failed)
}
```

O importante é **só devolver `Watched` quando o SDK confirmar a recompensa**.
O `AdRewardGoldenPackFlow` ignora `Skipped`, `Failed` e `Unavailable`.

### 3. Registre o adaptador no começo do jogo

Em qualquer `Awake`/`Start` que rode na `MainScene`:

```csharp
RewardedAdBridge.RegisterProvider(new MeuRewardedProvider());
```

Depois disso, o `AD_Btt` continua exatamente como está: não é preciso mudar
o popup, a navegação nem o PackUI. O `placementId` usado hoje é
`rewarded_golden_pack` e pode ser alterado no campo `Placement Id` do
componente `AdRewardGoldenPackFlow` no `AD_Btt`.

## Arquivos envolvidos

- `RewardedAdBridge.cs` — abstração, resultado e anúncio simulado.
- `AdRewardGoldenPackFlow.cs` — liga confirmação → anúncio → Golden Pack → sala.
- `AdConfirmationButton.cs` — expôs o evento de confirmação.
- `PackShopUGUIController.cs` — `ShowGoldenPackFromAd()` (só mostra o pacote;
  o clique na tela inicia a abertura) e saída com callback.
- `HorizontalNavegationController.cs` / `VerticalNavegationController.cs` —
  captura e restauração da sala horizontal.
