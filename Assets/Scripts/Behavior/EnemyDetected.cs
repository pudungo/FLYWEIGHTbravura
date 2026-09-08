using System;
using Unity.Behavior;
using UnityEngine;
using Unity.Properties;

#if UNITY_EDITOR
[CreateAssetMenu(menuName = "Behavior/Event Channels/EnemyDetected")]
#endif
[Serializable, GeneratePropertyBag]
[EventChannelDescription(name: "EnemyDetected", message: "[Agent] has spotted [Enemy]", category: "Events", id: "ec4ef6e8218a196910f3c007723217f3")]
public sealed partial class EnemyDetected : EventChannel<GameObject, GameObject> { }

