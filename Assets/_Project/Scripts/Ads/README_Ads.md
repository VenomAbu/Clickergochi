# Anúncio recompensado → Golden Pack

## Fluxo da MainScene

`AD_Btt` → `AdConfirmationButton` abre o `PopUpConfirmTV`. Ao confirmar, o
`AdRewardGoldenPackFlow` guarda a sala horizontal atual, pede um anúncio
recompensado ao `RewardedAdBridge` e, somente se o anúncio for assistido até o
fim, abre o PackUI com um **Golden Pack** pulsando. A abertura só começa quando
o jogador toca na tela. Ao sair do sticker, volta para a sala guardada.

## AdsManager / LevelPlay

O `AdsManager` (Unity LevelPlay 9.6) é o provedor real. Ele é registrado no
`RewardedAdBridge` automaticamente no `Awake()`:

- `AdsManager.MostrarRecompensado()` chama `ShowAd()` **sem placement**, usando
  o rewarded padrão do ad unit (`q443hhgcvppc88fb` na cena).
- O reward configurado no painel (`Recompensa`, amount `1`) dispara
  `OnAdRewarded`, que só libera o pacote quando o SDK confirma.
- `OnAdClosed` despausa o jogo e devolve `Watched` (se houve reward) ou
  `Skipped`.
- `OnAdDisplayFailed` devolve `Failed`.
- Anúncio não carregado devolve `Unavailable`; se `ShowAd()` for aceito mas o
  SDK não emitir close/display failed em até 60 s, o adaptador cancela o anúncio,
  despausa e devolve `Failed`. O fluxo nunca fica preso nem recompensa sem
  confirmação.

O campo `Placement Id` do `AdRewardGoldenPackFlow` (`rewarded_golden_pack`) é
mantido apenas como rótulo/diagnóstico. Ele **não** é enviado ao LevelPlay em
build até o placement ser confirmado no painel. Se o placement existir no painel,
basta voltar a passá-lo em `AdsManager.MostrarRecompensado()`/`ShowAd(placement)`.

> No Editor o LevelPlay não exibe anúncios nem dispara `OnAdClosed`. Por isso o
> adaptador mostra uma propaganda de teste em tela cheia (`EditorRewardedAdOverlay`)
> que exige assistir até o fim. Em build Android/iOS o caminho é o do AdsManager.

## Testar no Editor

1. Play na `MainScene`.
2. Clicar em `AD_Btt` → confirmar no `PopUpConfirmTV`.
3. Aparece uma **propaganda de teste em tela cheia** com barra de progresso.
   Ela dura `EditorRewardedAdOverlay.DurationSeconds` (10s por padrão).
4. `Pular` encerra sem recompensa. Assistir até o fim habilita `Continuar`
   (ou libera automaticamente) e só então aparece o Golden Pack pulsando.
5. Tocar na tela abre o pacote; sair do sticker volta à sala de origem.

## Arquivos envolvidos

- `AdsManager.cs` — singleton do LevelPlay, IDs e eventos do rewarded.
- `LevelPlayRewardedProvider.cs` — adaptador AdsManager → `RewardedAdBridge`.
- `RewardedAdBridge.cs` — abstração, resultado e anúncio simulado do Editor.
- `AdRewardGoldenPackFlow.cs` — confirmação → anúncio → Golden Pack → sala.
- `AdConfirmationButton.cs` — expõe o evento de confirmação da TV.
- `PackShopUGUIController.cs` — `ShowGoldenPackFromAd()` e saída com callback.
- `HorizontalNavegationController.cs` / `VerticalNavegationController.cs` —
  captura e restauração da sala horizontal.
