using System.Collections;
using UnityEngine;

public class ExclamationMark : MonoBehaviour
{
    [SerializeField]
    private float m_targetScale = 1f;        // The final scale we want to reach
    [SerializeField]
    private float m_duration = 0.5f;         // How long the animation takes
    [SerializeField]
    private float m_overshootAmount = 1.2f;  // How much to overshoot (1.2 = 20% over target)
    [SerializeField]
    private float m_springiness = 6f;        // Controls bounce frequency


    private void OnEnable()
    {
        // Reset scale to 0 and start animation
        StopAllCoroutines();
        transform.localScale = Vector3.zero;
        StartCoroutine(ScaleUpCoroutine());
    }

    public void Hide()
    {
        StopAllCoroutines();
        StartCoroutine(ScaleDownCoroutine());
    }

    private IEnumerator ScaleDownCoroutine()
    {
        float elapsed = 0f;
        float currentScale = transform.localScale.x;
        while (elapsed < m_duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / m_duration);

            // Apply scale
            transform.localScale = Vector3.one * ((1 - progress) * currentScale);

            yield return null;
        }

        // Ensure we end exactly at target scale
        transform.localScale = Vector3.zero;
        gameObject.SetActive(false);
    }

    private IEnumerator ScaleUpCoroutine()
    {
        float elapsed = 0f;

        while (elapsed < m_duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / m_duration;

            // Elastic easing with overshoot
            float scale = ElasticEaseOut(progress, m_overshootAmount, m_springiness);

            // Apply scale
            transform.localScale = Vector3.one * (scale * m_targetScale);

            yield return null;
        }

        // Ensure we end exactly at target scale
        transform.localScale = Vector3.one * m_targetScale;
    }

    private float ElasticEaseOut(float progress, float overshoot, float bounces)
    {
        if (progress == 0f) return 0f;
        if (progress == 1f) return 1f;

        float p = 1f - progress;
        return 1f + (overshoot - 1f) * Mathf.Pow(2f, -bounces * progress) * Mathf.Sin((progress * Mathf.PI * 2f * bounces));
    }
}
