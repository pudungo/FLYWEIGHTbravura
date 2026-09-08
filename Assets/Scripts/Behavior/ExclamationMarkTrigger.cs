using Unity.Behavior;
using UnityEngine;

public class ExclamationMarkTrigger : MonoBehaviour
{
    [SerializeField]
    private GameObject m_agent;
    [SerializeField]
    private ExclamationMark m_exclamationMark;

    private BlackboardVariable m_targetDetected;
    private void Start()
    {
        BehaviorGraphAgent behaviorAgent = m_agent.GetComponent<BehaviorGraphAgent>();
        if (behaviorAgent == null)
        {
            return;
        }
        behaviorAgent.BlackboardReference.GetVariable("TargetDetected", out m_targetDetected);
    }
    void Update()
    {
        if (m_targetDetected == null)
            return;
        bool targetDetected = (bool)m_targetDetected.ObjectValue;
        if (targetDetected)
        {
            if (m_exclamationMark.gameObject.activeSelf == false)
            {
                m_exclamationMark.gameObject.SetActive(true);
            }
        }
        else
        {
            if (m_exclamationMark.gameObject.activeSelf == true)
            {
                m_exclamationMark.Hide();
            }
        }
    }
}
