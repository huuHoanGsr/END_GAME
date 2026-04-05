using UnityEngine;

namespace Minigame
{
    public class ItemSpawner : MonoBehaviour
    {
        public GameObject[] itemPrefabs; // Prefab các vật phẩm
        public float spawnInterval = 1.0f;
        public float spawnRangeX = 7.5f;
        public float spawnY = 6f;

        // Vẽ vùng spawn trên Scene view
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Vector3 center = new Vector3(0, spawnY, 0) + transform.position;
            Vector3 size = new Vector3(spawnRangeX * 2, 0.2f, 0.2f);
            Gizmos.DrawWireCube(center, size);
        }

        private float timer;
        private bool isSpawning = false;

        public void StartSpawning()
        {
            isSpawning = true;
            timer = 0f;
        }

        public void StopSpawning()
        {
            isSpawning = false;
        }

        void Update()
        {
            if (!isSpawning) return;
            timer += Time.deltaTime;
            if (timer >= spawnInterval)
            {
                SpawnItem();
                timer = 0f;
            }
        }

        void SpawnItem()
        {
            int idx = Random.Range(0, itemPrefabs.Length);
            float x = Random.Range(-spawnRangeX, spawnRangeX);
            Vector3 pos = new Vector3(x, spawnY, 0);
            Instantiate(itemPrefabs[idx], pos, Quaternion.identity);
        }
    }
}