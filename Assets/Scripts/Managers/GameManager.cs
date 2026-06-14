// Assets/Scripts/Managers/GameManager.cs
using UnityEngine;

namespace NuclearReMind
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState { Playing, Paused, GameOver, Victory }
        public GameState CurrentState { get; private set; } = GameState.Playing;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnEnable()
        {
            EventManager.Instance.OnTowerComplete += HandleTowerComplete;
        }

        void OnDisable()
        {
            if (EventManager.Instance == null) return;
            EventManager.Instance.OnTowerComplete -= HandleTowerComplete;
        }

        void Start()
        {
            Debug.Log("[GameManager] Initialized");
        }

        void HandleTowerComplete()
        {
            SetState(GameState.Victory);
            EventManager.Instance.RaiseGameOver(GameEndType.Win);
        }

        public void SetState(GameState newState)
        {
            CurrentState = newState;
            Debug.Log($"[GameManager] State → {newState}");

            if (newState == GameState.Paused)
                Time.timeScale = 0f;
            else
                Time.timeScale = 1f;

            EventManager.Instance.RaiseGameStateChanged(newState);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SetState(CurrentState == GameState.Paused
                    ? GameState.Playing
                    : GameState.Paused);
            }
        }
    }
}
