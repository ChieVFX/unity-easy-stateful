using UnityEngine;
    
namespace EasyStateful.Runtime {
    [CreateAssetMenu(fileName = "NewStatefulData", menuName = "Stateful Animation/Stateful Data Asset")]
    public class StatefulDataAsset : ScriptableObject
    {
        public UIStateMachine stateMachine;
    }
}