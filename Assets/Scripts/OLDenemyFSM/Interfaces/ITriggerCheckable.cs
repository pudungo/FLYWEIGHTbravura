using UnityEngine;

public interface ITriggerCheckable
{
    bool IsAggroed { get; set; }

    bool IsWithStrikingDistance { get; set; }

    void SetAggroStatus(bool isAggroed);

    void SetStrikingDistanceStatus(bool isWithStrikingDistance);
}
