using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [System.Serializable]
    public struct LevelConfiguration
    {
        public int levelNumber;
        public int activeBlockTypesCount;
        public int topBufferRows;
        public float timeTilGameOver;
    }

    [Header("Current Progress")]
    public int currentLevel = 1;

    [Header("Level Configurations")]
    public List<LevelConfiguration> levelSettings;

    private void Awake()
    {
        // Singleton pattern with cross-scene persistence
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keeps this GameObject alive across scene loads!
        }
        else
        {
            Destroy(gameObject); // Destroys any duplicate LevelManager if you return to this scene
        }
    }

    public LevelConfiguration GetCurrentLevelConfig()
    {
        foreach (var config in levelSettings)
        {
            if (config.levelNumber == currentLevel)
            {
                return config;
            }
        }

        Debug.LogWarning($"Level {currentLevel} not found in LevelManager settings! Falling back to defaults.");
        return new LevelConfiguration
        {
            levelNumber = currentLevel,
            activeBlockTypesCount = 3,
            topBufferRows = 4,
            timeTilGameOver = 60f
        };
    }
}
