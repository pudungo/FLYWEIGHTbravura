using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "RangeDetector", story: "Update [RangeDetecter] and assign [Target]", category: "Action", id: "8fc925b5af4d67fc38edaa2cfc25f397")]
public partial class RangeDetectorAction : Action
{
    [SerializeReference] public BlackboardVariable<RangeDetector> RangeDetecter;
    [SerializeReference] public BlackboardVariable<GameObject> Target;


    protected override Status OnUpdate()
    {
        Target.Value = RangeDetecter.Value.UpdateDetector();
        return Target.Value == null ? Status.Failure : Status.Success;

    }

}

