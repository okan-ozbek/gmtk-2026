using UnityEngine;

namespace Player
{
    public class PlayerCollisionTrigger : MonoBehaviour
    {
        [SerializeField] private float yOffset;
        
        public void UpdateCollisionTrigger(Vector3 position)
        {
            transform.position = new Vector3(position.x, position.y + yOffset, position.z);
        }
    }
}