using UnityEngine;

public enum GameState
{
    Waiting,
    Playing,
    GameOver,
    Clear
}

namespace Game
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState CurrentState { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            CurrentState = GameState.Playing;
        }

        public void GameOver()
        {
            if (CurrentState == GameState.GameOver)
                return;

            CurrentState = GameState.GameOver;

            Debug.Log("Game Over");
        }

        public void Clear()
        {
            if (CurrentState == GameState.Clear)
                return;

            CurrentState = GameState.Clear;

            Debug.Log("Game Clear");
        }
    }
}