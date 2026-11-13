
using System.Collections;
using UnityEngine;

public class UIFeelGeral : MonoBehaviour
{
    [Header("Stretch and Squash")]
    [SerializeField] private float squashScale = 0.85f;
    [SerializeField] private float stretchScale = 1.1f;
    [SerializeField] private float animationDuration = 0.15f;

    private Vector3 originalScale;
    private Coroutine animationCoroutine;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void PlayStretchAndSquash()
    {
        // Botao de fechar: o popup ja foi desativado na mesma chamada do
        // onClick (ClosePopup roda antes), entao nao da pra iniciar corrotina
        // — sem esse guarda o Unity loga "Coroutine couldn't be started".
        if (!gameObject.activeInHierarchy)
            return;

        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        animationCoroutine = StartCoroutine(
            StretchAndSquashAnimation()
        );
    }

    private IEnumerator StretchAndSquashAnimation()
    {
        float halfDuration = animationDuration * 0.5f;

        Vector3 squashTarget = new Vector3(
            originalScale.x * squashScale,
            originalScale.y * squashScale,
            originalScale.z
        );

        Vector3 stretchTarget = new Vector3(
            originalScale.x * stretchScale,
            originalScale.y * stretchScale,
            originalScale.z
        );

        yield return AnimateScale(
            originalScale,
            squashTarget,
            halfDuration
        );

        yield return AnimateScale(
            squashTarget,
            stretchTarget,
            halfDuration
        );

        yield return AnimateScale(
            stretchTarget,
            originalScale,
            halfDuration
        );

        transform.localScale = originalScale;
        animationCoroutine = null;
    }

    private IEnumerator AnimateScale(
        Vector3 startScale,
        Vector3 targetScale,
        float duration
    )
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = duration > 0f
                ? Mathf.Clamp01(elapsed / duration)
                : 1f;

            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            transform.localScale = Vector3.Lerp(
                startScale,
                targetScale,
                smoothT
            );

            yield return null;
        }

        transform.localScale = targetScale;
    }
}
