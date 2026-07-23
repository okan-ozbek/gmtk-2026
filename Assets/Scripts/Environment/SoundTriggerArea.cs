using UnityEngine;

namespace Environment
{
    public class SoundTriggerArea : MonoBehaviour
    {
        [SerializeField] private AudioClip clip;
        [SerializeField] private bool repeatable = false;
        [SerializeField] private Collider triggerArea;
        [SerializeField] private Transform clipPosition;
        [SerializeField] private string triggerTag = "Player";

        private bool _triggered = false;

        private void Awake()
        {
            triggerArea = GetComponent<Collider>();
            triggerArea.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;
            
            if (other.CompareTag(triggerTag))
            {
                Debug.Log("TRIGGERED");
                
                var position = (clipPosition) ? clipPosition.position : transform.position;
                AudioSource.PlayClipAtPoint(clip, position);
                
                _triggered = true;
            }
        }
    }
}