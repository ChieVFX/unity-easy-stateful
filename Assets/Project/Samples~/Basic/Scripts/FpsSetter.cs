using UnityEngine;

namespace DefaultNamespace
{
    public class FpsSetter : MonoBehaviour
    {
        [SerializeField] private int targetFrameRate = 60;
        
        private void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
        }
    }
}