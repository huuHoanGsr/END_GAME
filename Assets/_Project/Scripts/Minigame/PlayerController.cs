using UnityEngine;
using UnityEngine.InputSystem;

namespace Minigame
{
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 10f;
        public float limitX = 8f;
        [Header("Gizmos")]
        public float limitLineHeight = 10f;
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Vector3 left = new Vector3(-limitX, transform.position.y, 0);
            Vector3 right = new Vector3(limitX, transform.position.y, 0);
            Gizmos.DrawLine(left + Vector3.down * limitLineHeight / 2, left + Vector3.up * limitLineHeight / 2);
            Gizmos.DrawLine(right + Vector3.down * limitLineHeight / 2, right + Vector3.up * limitLineHeight / 2);
        }

        [Header("Sprite")]
        public SpriteRenderer spriteRenderer;

        private float moveDir = 0f; // -1: trái, 1: phải, 0: đứng yên

        void Update()
        {
            // Ưu tiên input UI, chỉ nhận input bàn phím nếu không có input UI
#if UNITY_STANDALONE || UNITY_EDITOR
            if (!isMovingByUI)
            {
                moveDir = 0f;
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.leftArrowKey.isPressed) moveDir = -1f;
                    if (Keyboard.current.rightArrowKey.isPressed) moveDir = 1f;
                }
            }
#endif
            Vector3 pos = transform.position;
            pos.x += moveDir * moveSpeed * Time.deltaTime;
            pos.x = Mathf.Clamp(pos.x, -limitX, limitX);
            transform.position = pos;

            // Flip sprite nếu có SpriteRenderer
            if (spriteRenderer != null)
            {
                if (moveDir > 0.01f)
                    spriteRenderer.flipX = false;
                else if (moveDir < -0.01f)
                    spriteRenderer.flipX = true;
            }
        }

        // Các hàm này sẽ gán cho nút UI trên mobile
        private bool isMovingByUI = false;
        public void MoveLeft() { moveDir = -1f; isMovingByUI = true; }
        public void MoveRight() { moveDir = 1f; isMovingByUI = true; }
        public void StopMove() { moveDir = 0f; isMovingByUI = false; }

        void OnTriggerEnter2D(Collider2D other)
        {
            Item item = other.GetComponent<Item>();
            if (item != null)
            {
                GameManager.Instance.CollectItem(item);
                Destroy(other.gameObject);
            }
        }
    }
}
