using UnityEngine;

public class TurnAnimEvent : MonoBehaviour
{
    public Transform model; // your character mesh parent

    public void OnQuickTurnEnd()
    {
        // Apply the model's world rotation to the player root
        transform.rotation = model.rotation;

        // Reset model rotation so idle/walk doesn't snap it back
        model.localRotation = Quaternion.identity;


    }

}
