using UnityEngine;

namespace MyGame.Adapters.Unity
{
    public class RoomTrigger : MonoBehaviour
    {
        public LevelDirector levelDirector;
        private bool m_HasBeenTriggered = false;

        private void OnTriggerEnter(Collider other)
        {
            if (m_HasBeenTriggered) return;

            if (other.GetComponent<PlayerMovement>() != null)
            {
                if (levelDirector != null)
                {
                    levelDirector.OnRoomEntered(this.gameObject);
                }
                m_HasBeenTriggered = true;
            }
        }
    }
}
