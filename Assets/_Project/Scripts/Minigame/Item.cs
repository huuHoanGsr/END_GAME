using UnityEngine;

namespace Minigame
{
    public class Item : MonoBehaviour
    {
        public ItemType itemType;
        public int pointValue = 1; // AddPoint: +1, SubtractPoint: -1
        // Khi chạm đất thì biến mất
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Ground")) // Đặt tag "Ground" cho object mặt đất
            {
                if (itemType == ItemType.AddPoint && GameManager.Instance != null)
                {
                    GameManager.Instance.RegisterMiss();
                }
                Destroy(gameObject);
            }
        }

        void Reset()
        {
            // Gợi ý tự động set pointValue theo loại item khi tạo prefab
            if (itemType == ItemType.AddPoint) pointValue = 1;
            else if (itemType == ItemType.SubtractPoint) pointValue = -1;
        }
    }
}