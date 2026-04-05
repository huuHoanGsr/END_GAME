using UnityEngine;

namespace Minigame
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;
        [Header("Time Limits (seconds)")]
        [SerializeField, Min(1f)] private float timeLimitSeconds = 180f;
        [Header("Miss Limit")]
        [SerializeField, Min(1)] private int maxMisses = 3;

        public float timeLimit = 180f;
        public int score = 0;
        public bool isPlaying = false;
        public ItemSpawner spawner;
        public UIManager uiManager;

        private float timer = 0f;
        private int missCount = 0;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        void Start()
        {
            uiManager.ShowMenu();
        }

        public void StartGame()
        {
            score = 0;
            timer = 0f;
            missCount = 0;
            isPlaying = true;
            timeLimit = Mathf.Max(1f, timeLimitSeconds);
            spawner.StartSpawning();
            uiManager.ShowGame();
            uiManager.UpdateHp(maxMisses, maxMisses);
        }

        void Update()
        {
            if (!isPlaying) return;

            timer += Time.deltaTime;
            float remainingTime = Mathf.Max(0f, timeLimit - timer);
            uiManager.UpdateTime(remainingTime);

            if (timer >= timeLimit)
            {
                EndGame();
                return;
            }
        }

        public void CollectItem(Item item)
        {
            score += item.pointValue;
            uiManager.UpdateScore(score);
        }

        public void RegisterMiss()
        {
            if (!isPlaying)
            {
                return;
            }

            missCount++;
            uiManager.UpdateHp(maxMisses - missCount, maxMisses);
            if (missCount >= maxMisses)
            {
                EndGame();
            }
        }

        public void EndGame()
        {
            isPlaying = false;
            spawner.StopSpawning();
            uiManager.ShowResult(score);
        }
    }
}