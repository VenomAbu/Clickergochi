using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Encaminha o pointer-down na slime para o MainHUDUGUIController e faz o
/// pop de escala local. Anexado em runtime pelo HUD: prefere o botao
/// invisivel ClickButton (area menor que a imagem da slime); sem botao no
/// painel, cai para o RawImage.
///
/// Conta na pressao (PointerDown), nao no soltar: em rajada rapida o UP
/// podia colapsar cliques. Toque (Android) vem como Left e conta normal.
/// </summary>
public class SlimeClickRelay : MonoBehaviour, IPointerDownHandler
{
    [Tooltip("Objeto que recebe o pop de escala (a imagem da slime). " +
             "Vazio = este objeto.")]
    public Transform popTarget;

    private MainHUDUGUIController _hud;
    private float _burstTimer = -1f;
    private Vector3 _baseScale = Vector3.one;
    private bool _baseCaptured;

    public void Init(MainHUDUGUIController hud) => _hud = hud;

    private Transform Pop => popTarget != null ? popTarget : transform;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!_baseCaptured)
        {
            _baseScale = Pop.localScale;
            _baseCaptured = true;
        }

        _hud?.OnSlimePointerDown();
        _burstTimer = 0f;
    }

    private void Update()
    {
        if (_burstTimer < 0f)
            return;

        Transform pop = Pop;
        _burstTimer += Time.unscaledDeltaTime / 0.14f;
        if (_burstTimer >= 1f)
        {
            _burstTimer = -1f;
            pop.localScale = _baseScale;
        }
        else
        {
            pop.localScale =
                _baseScale * (1f - 0.08f * Mathf.Sin(_burstTimer * Mathf.PI));
        }
    }
}
