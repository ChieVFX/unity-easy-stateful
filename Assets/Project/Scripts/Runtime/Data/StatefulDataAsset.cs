using UnityEngine;

[CreateAssetMenu(fileName = "NewStatefulData", menuName = "Stateful Animation/Stateful Data Asset")]
public class StatefulDataAsset : ScriptableObject
{
    public UIStateMachine stateMachine;
}