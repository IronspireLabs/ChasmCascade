using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum GameState { Playing, Win, Lose }
public class GridManager : MonoBehaviour
{
    public static GridManager Instance;
    [Header("Scoring")]
    public int score = 0;
    public GameState gameState;
    public float timeTilGameOVer = 90;
    [Header("Game Timer")]
    public float gameTime = 0f;
    public bool isTimerRunning = false;
    public string timerToString;

    public GameObject winUi;
    public float gameOverTimer;
    [SerializeField] Text timeText;
    [SerializeField] Text scoreText;
    [Header("Animation References")]
    [Tooltip("The Animator component on the guy character model.")]
    public Animator guyAnimator;
    [Header("Grid Dimensions")]
    public int width = 5;
    public int height = 8;
    public float spacing = 1.1f; // Distance between blocks in the 3D world

    [Header("Spawn Buffer Settings")]
    [Tooltip("Number of rows left completely empty at the top of the grid at game start.")]
    public int topBufferRows = 2;

    [Header("Spawner Setup")]
    public PipeController pipeController;
    // 2D array storing the blocks on the grid
    private Block[,] grid;
    [HideInInspector]
    public bool isBusy = false; // Locks player input during cascades and movement
    [Header("Game Rules")]
    [Tooltip("If true, players can swap blocks freely even if no match is made. If false, non-matching swaps revert back.")]
    public bool allowNonPairSwaps = false;
    [Tooltip("If true, only horizontal (left/right) matches are allowed. If false, standard multi-directional matching applies.")]
    public bool horizontalMatchesOnly = false;
    [Tooltip("Minimum number of connected blocks required to form a match (e.g., 4 for Match-4).")]
    public int minMatchLength = 4;

    [Header("Guy Settings")]
    [Tooltip("Prefab for the character (can be a simple cube for now).")]
    public GameObject guyPrefab;
    [Tooltip("Time (in seconds) it takes for the guy to travel from one grid tile to the next.")]
    public float guyMoveDuration = 0.25f; // Adjust this in the Inspector
    [Tooltip("If checked, the guy cannot climb up blocks (only moves left, right, and down).")]
    public bool disallowClimbing = false; // <--- ADD THIS TOGGLE
    [Tooltip("The column where the guy will start.")]
    public int guyStartColumn = 0;

    [HideInInspector]
    public Vector2Int guyGridPosition;
    private GameObject guyInstance;
    [Header("Pity Spawning Settings")]
    [Tooltip("If checked, the spawner will favor colors currently on the board when few blocks remain.")]
    public bool enablePitySpawning = true;
    [Tooltip("Total number of blocks on the grid required to trigger general pity spawning.")]
    public int pityBlockThreshold = 10;
    [Tooltip("Total number of blocks on the grid required to force-spawn the matching color for blocks near the door.")]
    public int doorTargetThreshold = 20; // <--- Exposes the threshold to the Inspector (defaulting to 20)
    [Header("Combo Tracking")]
    [Tooltip("Tracks the number of consecutive matches in the current chain/turn.")]
    public int consecutiveMatches = 0;
    public int comboReached = 3;
    public int comboBonus = 50;


    public AudioSource sfxAudio;
    public AudioClip matchClip;
    public GameObject restartButton;

    public Transform iceBlockTransform; // Drag your ice object here in the Inspector
    private Vector3 initialIceScale;
    private Vector3 initialIceLocalPos;
    private float initialHeight;

    [Header("Idle Fidget Settings")]
    private float idleTimer = 0f;
    private float nextFidgetTime = 6f; // Time in seconds before attempting a fidget

    public Text bestTimeText;
    public Text highscoreText;
    public Text levelText;

    [System.Serializable]
    public struct BlockPrefabData
    {
        public GameObject prefab;
        [Tooltip("Base points awarded per block for this specific type.")]
        public int scoreValue;
    }

    [Header("Block Prefabs")]
    [Tooltip("List of unique block prefabs and their corresponding ID names.")]
    public List<BlockPrefabData> blockPrefabs;
    [Header("Block Variety Settings")]
    [Tooltip("How many different types of blocks to include in this game session.")]
    [Range(1, 10)]
    public int activeBlockTypesCount = 3;

    // The filtered subset of block data used during this game session
    private List<BlockPrefabData> activeBlockPrefabs = new List<BlockPrefabData>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        grid = new Block[width, height];
    }
    void Start()
    {
        GenerateInitialBoard();
        gameState = GameState.Playing;
        restartButton.SetActive(false);
        winUi.SetActive(false);

        // NOW that all blocks exist and are registered in the grid array, 
        // scan the board to clear any accidental matches the game started with!
        ScanBoardForMatches();

        // Start the timer
        gameTime = 0f;
        gameOverTimer = timeTilGameOVer;
        isTimerRunning = true;

        // Store the starting size of the ice block
        // Store the starting size and position of the ice block
        if (iceBlockTransform != null)
        {
            initialIceScale = iceBlockTransform.localScale;
            initialIceLocalPos = iceBlockTransform.localPosition;
            initialHeight = initialIceScale.y;
        }

        int bestScore = GetSavedHighScore(LevelManager.Instance.currentLevel);
        float bestTime = GetSavedFastestTime(LevelManager.Instance.currentLevel);

        // Format the time text nicely (or show "No Record" if it equals float.MaxValue)
        string timeDisplay = (bestTime == float.MaxValue) ? "--:--" : bestTime.ToString("F2") + "s";
        highscoreText.text = $"Highscore: {bestScore}";
        bestTimeText.text = $"Best time: {timeDisplay}";
        levelText.text = $"Level {LevelManager.Instance.currentLevel}";
    }
    private void Update()
    {
        if (GetRemainingBlockCount() < 15)
        {
            comboReached = 1;
        }
        else
        {
            comboReached = 3;
        }

        if (isTimerRunning && gameState == GameState.Playing)
        {
            gameTime += Time.deltaTime;
            gameOverTimer -= Time.deltaTime;
            timerToString = GetFormattedTime();

            if (gameTime >= timeTilGameOVer)
            {
                GameOverCondition();
                isTimerRunning = false;
            }

            timeText.text = GetFormattedTimeTilGameOver();
            scoreText.text = $"Score: {score}";

            // Only count idle time when the game is not busy (player is thinking/waiting)
            if (!isBusy && guyAnimator != null)
            {
                // Check if he is actually in idle (not running, jumping, or falling)
                bool isMovingOrAirborne = guyAnimator.GetBool("IsRunning") ||
                                          guyAnimator.GetBool("IsJumping") ||
                                          guyAnimator.GetBool("IsFalling");

                if (!isMovingOrAirborne)
                {
                    idleTimer += Time.deltaTime;

                    // When the timer hits the threshold, trigger the second idle
                    if (idleTimer >= nextFidgetTime)
                    {
                        guyAnimator.SetTrigger("PlayFidget");

                        // Reset timer and pick a random interval for the *next* fidget (e.g., between 6 to 14 seconds)
                        idleTimer = 0f;
                        nextFidgetTime = Random.Range(6f, 14f);
                    }
                }
                else
                {
                    // Reset the timer instantly if he is moving or acting
                    idleTimer = 0f;
                }
            }
        }

        //Ice thawing
        // If the game is won, stop processing time and ice thawing
        if (gameState != GameState.Playing) return;

        // Calculate progress from 0.0 (start) to 1.0 (time's up)
        float thawProgress = Mathf.Clamp01(gameTime / timeTilGameOVer);

        // Top-down melting logic
        if (iceBlockTransform != null)
        {
            // 1. Shrink the height (Y scale) over time
            float currentHeight = Mathf.Lerp(initialHeight, 0f, thawProgress);
            Vector3 newScale = initialIceScale;
            newScale.y = currentHeight;
            iceBlockTransform.localScale = newScale;

            // 2. Shift the position downward so the bottom stays anchored to the floor
            float heightDifference = initialHeight - currentHeight;
            Vector3 newPos = initialIceLocalPos;
            newPos.y -= heightDifference / 2f;
            iceBlockTransform.localPosition = newPos;
        }
    }
    public void InitializeActiveBlocks()
    {
        activeBlockPrefabs.Clear();

        if (blockPrefabs == null || blockPrefabs.Count == 0)
        {
            Debug.LogError("No block prefabs assigned in the master list!");
            return;
        }

        // --- PULL DIFFICULTY SETTINGS FROM LEVEL MANAGER ---
        if (LevelManager.Instance != null)
        {
            var levelConfig = LevelManager.Instance.GetCurrentLevelConfig();
            activeBlockTypesCount = levelConfig.activeBlockTypesCount;
            Debug.Log($"Loaded Level {levelConfig.levelNumber}: Setting active block types to {activeBlockTypesCount}");
        }
        // ---------------------------------------------------

        // Ensure we don't try to pick more types than are available in your master list
        int countToUse = Mathf.Clamp(activeBlockTypesCount, 1, blockPrefabs.Count);

        // Create a temporary copy to pick unique random struct entries from
        List<BlockPrefabData> tempPool = new List<BlockPrefabData>(blockPrefabs);

        for (int i = 0; i < countToUse; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, tempPool.Count);
            activeBlockPrefabs.Add(tempPool[randomIndex]);
            tempPool.RemoveAt(randomIndex); // Remove so we don't pick the same block type twice
        }

        Debug.Log($"Initialized game with {activeBlockPrefabs.Count} active block types out of {blockPrefabs.Count} total.");
    }

    public void GenerateInitialBoard()
    {
        // --- PULL LEVEL CONFIGURATION FROM LEVEL MANAGER ---
        if (LevelManager.Instance != null)
        {
            var levelConfig = LevelManager.Instance.GetCurrentLevelConfig();
            activeBlockTypesCount = levelConfig.activeBlockTypesCount;
            topBufferRows = levelConfig.topBufferRows;
            timeTilGameOVer = levelConfig.timeTilGameOver; // Sync the time limit for this level!

            Debug.Log($"Loaded Level {levelConfig.levelNumber}: Active Types = {activeBlockTypesCount}, Buffer Rows = {topBufferRows}, Time Limit = {timeTilGameOVer}s");
        }
        // ---------------------------------------------------

        InitializeActiveBlocks();

        if (activeBlockPrefabs == null || activeBlockPrefabs.Count == 0)
        {
            Debug.LogError("No active block prefabs available to generate board!");
            return;
        }

        int fillHeight = height - topBufferRows;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < fillHeight; y++)
            {
                string chosenId = GetNonMatchingId(x, y);
                GameObject prefabToSpawn = GetPrefabForId(chosenId);

                Vector3 spawnPos = GetWorldPosition(x, y);
                GameObject newBlockObj = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity, transform);
                newBlockObj.name = $"Block_{chosenId}_[{x},{y}]";

                Block blockScript = newBlockObj.GetComponent<Block>();
                if (blockScript != null)
                {
                    RegisterBlock(blockScript, x, y);
                }
            }
        }

        // Spawn the guy on top of the initial blocks in the designated column
        if (guyPrefab != null)
        {
            guyGridPosition = new Vector2Int(guyStartColumn, fillHeight);
            Vector3 guySpawnPos = GetWorldPosition(guyGridPosition.x, guyGridPosition.y);

            guyInstance = Instantiate(guyPrefab, guySpawnPos, Quaternion.identity, transform);
            guyAnimator = guyInstance.GetComponentInChildren<Animator>();
            guyInstance.name = "PlayerGuy";

            // Destroy the block directly underneath him
            int blockUnderneathY = fillHeight - 1;
            if (blockUnderneathY >= 0 && grid[guyStartColumn, blockUnderneathY] != null)
            {
                Block blockBelow = grid[guyStartColumn, blockUnderneathY];

                grid[guyStartColumn, blockUnderneathY] = null;

                if (blockBelow != null && blockBelow.gameObject != null)
                {
                    Destroy(blockBelow.gameObject);
                }

                guyGridPosition = new Vector2Int(guyStartColumn, blockUnderneathY);
                guyInstance.transform.position = GetWorldPosition(guyGridPosition.x, guyGridPosition.y);
            }
        }
    }

    // Helper to check neighbors and pick an ID that won't create an initial match
    string GetNonMatchingId(int x, int y)
    {
        List<string> validIds = new List<string>();
        foreach (var data in activeBlockPrefabs)
        {
            validIds.Add(data.prefab.GetComponent<Block>().idName);
        }

        // Check block to the LEFT
        if (x > 0 && grid[x - 1, y] != null)
        {
            string leftType = grid[x - 1, y].idName;
            validIds.Remove(leftType);
        }

        // Check block BELOW
        if (y > 0 && grid[x, y - 1] != null)
        {
            string downType = grid[x, y - 1].idName;
            validIds.Remove(downType);
        }

        // Fallback if all options are filtered out
        if (validIds.Count == 0)
        {
            return blockPrefabs[Random.Range(0, blockPrefabs.Count)].prefab.GetComponent<Block>().idName;
        }

        return validIds[Random.Range(0, validIds.Count)];
    }

    // Helper to look up the correct prefab GameObject by its string ID
    GameObject GetPrefabForId(string idName)
    {
        foreach (var data in activeBlockPrefabs)
        {
            if (data.prefab.GetComponent<Block>().idName == idName)
            {
                return data.prefab;
            }
        }
        return null;
    }

    // Call this when spawning or initializing blocks to register them
    public void RegisterBlock(Block block, int x, int y)
    {
        grid[x, y] = block;
        block.gridPosition = new Vector2Int(x, y);
        block.transform.position = GetWorldPosition(x, y);
    }

    public Vector3 GetWorldPosition(int x, int y)
    {
        // Adjust based on how your 3D grid is oriented (e.g., X and Y plane)
        return new Vector3(x * spacing, y * spacing, 0f);
    }

    public void SwapBlock(GameObject blockObj, Vector2Int direction)
    {
        // Ignore input if the game is currently busy cascading or moving the guy
        if (isBusy) return;

        Block block = blockObj.GetComponent<Block>();
        if (block == null) return;

        Vector2Int startPos = block.gridPosition;
        Vector2Int targetPos = startPos + direction;

        // 1. Check if either the moving block or the target space is occupied by the guy
        if (IsCellOccupiedByGuy(startPos) || IsCellOccupiedByGuy(targetPos))
        {
            return;
        }

        // 2. Check if target position is within grid boundaries
        if (targetPos.x < 0 || targetPos.x >= width || targetPos.y < 0 || targetPos.y >= height)
        {
            return;
        }

        Block targetBlock = grid[targetPos.x, targetPos.y];

        // 3. Check if target is empty
        if (targetBlock == null)
        {
            bool isHorizontal = (direction.x != 0);

            if (isHorizontal && allowNonPairSwaps)
            {
                // --- SAFETY CHECK: Prevent dropping blocks on the guy ---
                if (WouldBlockFallOnGuy(targetPos))
                {
                    StartCoroutine(InvalidSwipeBump(block, targetPos));
                    return;
                }
                // -------------------------------------------------------

                isBusy = true; // Lock input
                StartCoroutine(ExecuteMoveIntoEmptySpace(block, startPos, targetPos));
            }
            else
            {
                StartCoroutine(InvalidSwipeBump(block, targetPos));
            }
            return;
        }

        // 4. Normal block-to-block swap
        isBusy = true; // Lock input
        StartCoroutine(ExecuteSwapAndCheck(block, targetBlock, startPos, targetPos));
    }

    // Handles moving a single block horizontally into an empty gap, then letting gravity drop it
    System.Collections.IEnumerator ExecuteMoveIntoEmptySpace(Block block, Vector2Int startPos, Vector2Int targetPos)
    {
        try
        {
            // Update the grid array data
            grid[targetPos.x, targetPos.y] = block;
            grid[startPos.x, startPos.y] = null;

            block.gridPosition = targetPos;

            // Smoothly animate the block sliding horizontally into the empty column
            yield return StartCoroutine(block.MoveToPosition(GetWorldPosition(targetPos.x, targetPos.y), 0.15f));

            // Drop the block using gravity, then run our controlled cascade check!
            yield return StartCoroutine(ApplyGravityRoutine());

            // Yield this so isBusy stays true until the cascade & guy movement fully complete
            yield return StartCoroutine(ExecuteControlledCascadeCoroutine());
        }
        finally
        {
            isBusy = false;
        }
    }

    // Helper coroutine to handle the animation delay safely
    System.Collections.IEnumerator ExecuteSwapAndCheck(Block blockA, Block blockB, Vector2Int posA, Vector2Int posB)
    {
        // Update grid array data immediately
        grid[posA.x, posA.y] = blockB;
        grid[posB.x, posB.y] = blockA;

        blockA.gridPosition = posB;
        blockB.gridPosition = posA;

        // Animate both blocks and wait for them to finish moving
        Coroutine moveA = StartCoroutine(blockA.MoveToPosition(GetWorldPosition(posB.x, posB.y), 0.15f));
        Coroutine moveB = StartCoroutine(blockB.MoveToPosition(GetWorldPosition(posA.x, posA.y), 0.15f));

        yield return moveA;
        yield return moveB;

        // Check if the swap successfully created a match
        bool matchFound = ScanBoardForMatches();

        if (!matchFound)
        {
            consecutiveMatches = 0;

            // FIXED: Check if non-matching swaps are allowed!
            if (allowNonPairSwaps)
            {
                // Keep the swap and proceed with the turn (drop from pipe / check gravity)
                StartCoroutine(ExecuteControlledCascadeCoroutine());
            }
            else
            {
                // Swap the grid data and blocks back to their original positions
                grid[posA.x, posA.y] = blockA;
                grid[posB.x, posB.y] = blockB;

                blockA.gridPosition = posA;
                blockB.gridPosition = posB;

                Coroutine returnA = StartCoroutine(blockA.MoveToPosition(GetWorldPosition(posA.x, posA.y), 0.15f));
                Coroutine returnB = StartCoroutine(blockB.MoveToPosition(GetWorldPosition(posB.x, posB.y), 0.15f));

                yield return returnA;
                yield return returnB;

                // Unlock input because the move failed and reverted
                isBusy = false;
            }
        }
        else
        {
            // Match found! Run your cascade process. 
            StartCoroutine(ExecuteControlledCascadeCoroutine());
        }
    }
    public IEnumerator ExecuteControlledCascadeCoroutine()
    {
        // Wrap in try-finally to guarantee input is never permanently locked
        try
        {
            // Give a tiny pause right after the player's move so the action registers visually
            yield return new WaitForSeconds(0.15f);

            // Keep looping as long as matches are found anywhere on the board
            bool matchFound = ScanBoardForMatches();
            while (matchFound)
            {
                yield return new WaitForSeconds(0.2f);

                // Apply gravity to drop blocks into empty spaces left by matched blocks
                yield return StartCoroutine(ApplyGravityRoutine());
                yield return StartCoroutine(ApplyGravityToGuyRoutine());

                // Re-scan the board to check if the gravity drop created any new matches (chain reactions)
                matchFound = ScanBoardForMatches();
            }

            // --- FULLY SETTLED CASCADE & CHAIN REACTIONS ---
            // Only now that the board is completely free of active matches do we drop the next block from the pipe
            DropBlockFromPipe();
            yield return new WaitForSeconds(0.2f);

            // Let gravity settle the newly dropped block
            yield return StartCoroutine(ApplyGravityRoutine());
            yield return StartCoroutine(ApplyGravityToGuyRoutine());

            // Final scan in case the pipe block's drop caused a match chain reaction
            matchFound = ScanBoardForMatches();
            while (matchFound)
            {
                yield return new WaitForSeconds(0.2f);
                yield return StartCoroutine(ApplyGravityRoutine());
                yield return StartCoroutine(ApplyGravityToGuyRoutine());
                matchFound = ScanBoardForMatches();
            }

            TryMoveGuyTowardDoor();

            // --- SOFTLOCK CHECK ---
            // If non-matching swaps are turned off, check if the player has any moves left.
            // If not, automatically trigger a board reshuffle!
            if (!allowNonPairSwaps && !HasValidMoves())
            {
                yield return StartCoroutine(ReplenishAndReshuffleRoutine());
            }
        }
        finally
        {
            // This guarantees input is unlocked no matter what happens above
            isBusy = false;
        }
    }
    IEnumerator InvalidSwipeBump(Block block, Vector2Int targetPos)
    {
        Vector3 startWorldPos = block.transform.position;
        Vector3 targetWorldPos = GetWorldPosition(targetPos.x, targetPos.y);

        // Nudge 30% of the way towards the empty space
        Vector3 bumpPos = Vector3.Lerp(startWorldPos, targetWorldPos, 0.3f);

        float elapsed = 0f;
        float duration = 0.1f;

        // Move out slightly
        while (elapsed < duration)
        {
            block.transform.position = Vector3.Lerp(startWorldPos, bumpPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        // Snap back home
        while (elapsed < duration)
        {
            block.transform.position = Vector3.Lerp(bumpPos, startWorldPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        block.transform.position = startWorldPos;
    }
    public void ClearBlock(Vector2Int pos)
    {
        if (grid[pos.x, pos.y] != null)
        {
            Block blockToDestroy = grid[pos.x, pos.y];

            // Clear it from the array first
            grid[pos.x, pos.y] = null;

            // Stop any active movement coroutines so they don't throw errors
            blockToDestroy.StopAllCoroutines();

            // Now safely destroy the 3D game object
            Destroy(blockToDestroy.gameObject);
        }
    }
    // 1. Change this to trigger the gravity coroutine instead of running instantly
    public void ApplyGravity()
    {
        StartCoroutine(ApplyGravityRoutine());
    }

    // 2. Gravity routine that waits for blocks to land before scanning for new matches
    System.Collections.IEnumerator ApplyGravityRoutine()
    {
        float fallDuration = 0.15f;
        bool blocksMoved;

        // Keep running gravity passes until a full check finds zero blocks left to drop
        do
        {
            blocksMoved = false;

            // Loop through every column (X axis)
            for (int x = 0; x < width; x++)
            {
                int targetY = 0; // Tracks the lowest available empty slot

                for (int y = 0; y < height; y++)
                {
                    if (grid[x, y] != null)
                    {
                        if (targetY != y)
                        {
                            Block blockToMove = grid[x, y];

                            // Update grid array
                            grid[x, targetY] = blockToMove;
                            grid[x, y] = null;

                            // Update grid position data
                            blockToMove.gridPosition = new Vector2Int(x, targetY);

                            // Animate falling
                            StartCoroutine(blockToMove.MoveToPosition(GetWorldPosition(x, targetY), fallDuration));
                            blocksMoved = true;
                        }
                        targetY++;
                    }
                }
            }

            // If any blocks fell during this pass, wait for them to animate before checking again
            if (blocksMoved)
            {
                yield return new WaitForSeconds(fallDuration);
            }
        }
        while (blocksMoved);
    }

    // 3. Scans the entire grid for any matches created by falling blocks
    public bool ScanBoardForMatches()
    {
        System.Collections.Generic.HashSet<Vector2Int> blocksToClear = new System.Collections.Generic.HashSet<Vector2Int>();

        // 1. Check Horizontal Matches
        for (int y = 0; y < height; y++)
        {
            int x = 0;
            while (x < width)
            {
                if (grid[x, y] == null)
                {
                    x++;
                    continue;
                }

                string currentId = grid[x, y].idName;
                int runLength = 1;
                int nextX = x + 1;

                while (nextX < width && grid[nextX, y] != null && grid[nextX, y].idName == currentId)
                {
                    runLength++;
                    nextX++;
                }

                if (runLength >= minMatchLength)
                {
                    // --- SCORE CALCULATION (Horizontal) ---
                    int basePointsPerBlock = GetBlockPointValue(currentId);
                    int matchScore = runLength * basePointsPerBlock;

                    // Multiply by combo count if active (minimum multiplier of 1)
                    int multiplier = Mathf.Max(1, consecutiveMatches + 1);
                    score += matchScore * multiplier;
                    // -------------------------------------

                    for (int i = 0; i < runLength; i++)
                    {
                        blocksToClear.Add(new Vector2Int(x + i, y));
                    }
                }

                x = nextX;
            }
        }

        // 2. Check Vertical Matches
        if (!horizontalMatchesOnly)
        {
            for (int x = 0; x < width; x++)
            {
                int y = 0;
                while (y < height)
                {
                    if (grid[x, y] == null)
                    {
                        y++;
                        continue;
                    }

                    string currentId = grid[x, y].idName;
                    int runLength = 1;
                    int nextY = y + 1;

                    while (nextY < height && grid[x, nextY] != null && grid[x, nextY].idName == currentId)
                    {
                        runLength++;
                        nextY++;
                    }

                    if (runLength >= minMatchLength)
                    {
                        // --- SCORE CALCULATION (Vertical) ---
                        int basePointsPerBlock = GetBlockPointValue(currentId);
                        int matchScore = runLength * basePointsPerBlock;

                        int multiplier = Mathf.Max(1, consecutiveMatches + 1);
                        score += matchScore * multiplier;
                        // -------------------------------------

                        for (int i = 0; i < runLength; i++)
                        {
                            blocksToClear.Add(new Vector2Int(x, y + i));
                        }
                    }

                    y = nextY;
                }
            }
        }

        // If matches were found, clear them and increment combo
        if (blocksToClear.Count > 0)
        {
            Debug.Log($"Match found! Clearing {blocksToClear.Count} blocks. Current Score: {score}");
            consecutiveMatches++;
            sfxAudio.PlayOneShot(matchClip);

            // --- 3-COMBO SPECIAL: Clear all blocks neighbouring the guy ---
            if (consecutiveMatches >= comboReached)
            {
                Debug.Log("Combo reached 3! Clearing all blocks neighbouring the guy and resetting combo.");

                for (int xOffset = -1; xOffset <= 1; xOffset++)
                {
                    for (int yOffset = -1; yOffset <= 1; yOffset++)
                    {
                        Vector2Int neighborPos = new Vector2Int(guyGridPosition.x + xOffset, guyGridPosition.y + yOffset);

                        if (neighborPos.x >= 0 && neighborPos.x < width && neighborPos.y >= 0 && neighborPos.y < height)
                        {
                            if (grid[neighborPos.x, neighborPos.y] != null)
                            {
                                blocksToClear.Add(neighborPos);
                                // Optional: Award extra points for combo-cleared neighbor blocks
                                score += comboBonus;
                            }
                        }
                    }
                }

                consecutiveMatches = 0;
            }

            foreach (var pos in blocksToClear)
            {
                ClearBlock(pos);
            }

            return true;
        }

        return false;
    }
    public void DropBlockFromPipe()
    {
        if (pipeController == null || pipeController.currentPreviewBlock == null) return;

        int startCol = pipeController.currentColumn;
        int targetCol = -1;
        int targetY = -1;

        // Scan columns starting from the current pipe position
        for (int i = 0; i < width; i++)
        {
            int checkCol = (startCol + i) % width;

            // Completely skip the column if the guy is in it
            if (checkCol == guyGridPosition.x)
            {
                continue;
            }

            // Find the lowest empty slot in this checked column
            int foundY = -1;
            for (int y = 0; y < height; y++)
            {
                if (grid[checkCol, y] == null)
                {
                    foundY = y;
                    break;
                }
            }

            // If an empty slot was found, use this column!
            if (foundY != -1)
            {
                targetCol = checkCol;
                targetY = foundY;
                break;
            }
        }

        // If targetCol is still -1, valid drop columns are full or blocked
        if (targetCol == -1)
        {
            Debug.Log("No valid columns available to drop block! Game Over condition met.");
            GameOverCondition();
            return;
        }

        // If the pipe had to shift to a new column, update its position visually
        if (targetCol != startCol)
        {
            pipeController.currentColumn = targetCol;
            pipeController.UpdatePipePosition();
        }

        // 1. Capture the exact world position where the preview block is hovering
        Vector3 spawnPos = pipeController.currentPreviewBlock.transform.position;

        // 2. Consume the preview block and get its prefab type
        GameObject prefabToDrop = pipeController.ConsumeCurrentPreview();

        // 3. Spawn the actual falling block at the preview position
        GameObject newBlockObj = Instantiate(prefabToDrop, spawnPos, Quaternion.identity, transform);

        Block blockScript = newBlockObj.GetComponent<Block>();
        if (blockScript != null)
        {
            // Register it in the grid array
            grid[targetCol, targetY] = blockScript;
            blockScript.gridPosition = new Vector2Int(targetCol, targetY);

            // 4. Smoothly animate it falling down into its target grid slot
            StartCoroutine(blockScript.MoveToPosition(GetWorldPosition(targetCol, targetY), 0.25f));
        }

        // =========================================================================
        // CHECK IF THIS DROP COMPLETELY FILLED UP THE TARGET COLUMN
        // =========================================================================
        bool isTargetColFull = true;
        for (int y = 0; y < height; y++)
        {
            if (grid[targetCol, y] == null)
            {
                isTargetColFull = false;
                break;
            }
        }

        // If the column is now full, immediately advance the pipe to the next open column
        if (isTargetColFull)
        {
            int nextValidCol = -1;
            for (int i = 1; i <= width; i++)
            {
                int checkCol = (targetCol + i) % width;

                // Skip the guy's column
                if (checkCol == guyGridPosition.x) continue;

                // Check if this column has at least one empty slot
                bool hasSpace = false;
                for (int y = 0; y < height; y++)
                {
                    if (grid[checkCol, y] == null)
                    {
                        hasSpace = true;
                        break;
                    }
                }

                if (hasSpace)
                {
                    nextValidCol = checkCol;
                    break;
                }
            }

            // Apply the immediate shift so the pipe is waiting over the next open space
            if (nextValidCol != -1)
            {
                pipeController.currentColumn = nextValidCol;
                pipeController.UpdatePipePosition();
            }
        }
    }
    public void GameOverCondition()
    {
        gameState = GameState.Lose;
        isBusy = true;
        restartButton.SetActive(true);
    }
    public bool IsCellOccupiedByGuy(Vector2Int pos)
    {
        return pos == guyGridPosition;
    }
    [Header("Door & Navigation Settings")]
    [Tooltip("Grid position of the door the guy is trying to reach.")]
    public Vector2Int doorGridPosition = new Vector2Int(0, 0); // Change as needed

    [Tooltip("Maximum vertical step height the guy can climb (1 allows stepping up/down 1 block, higher allows climbing stacks).")]
    public int maxClimbHeight = 1;

    // Helper to find the y-coordinate where the guy stands on a given column
    int GetSurfaceHeight(int col)
    {
        for (int y = 0; y < height; y++)
        {
            if (grid[col, y] == null)
            {
                return y; // First empty slot from bottom is the walking surface
            }
        }
        return height - 1; // Column is completely full
    }
    public List<Vector2Int> FindPathToDoor()
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        Vector2Int startNode = guyGridPosition;
        queue.Enqueue(startNode);
        visited.Add(startNode);

        Vector2Int bestNode = startNode;
        float minDistanceToDoor = Vector2.Distance(startNode, doorGridPosition);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            float dist = Vector2.Distance(current, doorGridPosition);
            if (dist < minDistanceToDoor)
            {
                minDistanceToDoor = dist;
                bestNode = current;
            }

            if (current == doorGridPosition)
            {
                bestNode = current;
                break;
            }

            // Check adjacent columns (Left and Right)
            int[] neighborCols = { current.x - 1, current.x + 1 };
            foreach (int nextCol in neighborCols)
            {
                if (nextCol < 0 || nextCol >= width) continue;

                int nextY = GetSurfaceHeight(nextCol);
                Vector2Int neighbor = new Vector2Int(nextCol, nextY);

                int heightDiff = nextY - current.y;

                // Movement restrictions
                bool canClimbUp = disallowClimbing ? (heightDiff <= 0) : (heightDiff <= maxClimbHeight);
                bool canDropDownd = (heightDiff >= -1);

                bool isValidMove = canClimbUp && canDropDownd;

                if (isValidMove && !visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    cameFrom[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }
        }

        // 1. Reconstruct the raw path from startNode to bestNode
        List<Vector2Int> rawPath = new List<Vector2Int>();
        Vector2Int curr = bestNode;

        while (curr != startNode)
        {
            rawPath.Add(curr);
            if (!cameFrom.ContainsKey(curr)) break;
            curr = cameFrom[curr];
        }

        rawPath.Reverse();

        // 2. Refine path to ensure orthogonal movement (Horizontal first, then Vertical)
        List<Vector2Int> refinedPath = new List<Vector2Int>();
        Vector2Int lastPos = guyGridPosition;

        foreach (Vector2Int target in rawPath)
        {
            // If both X and Y change in a single step, break it into two orthogonal moves
            if (target.y != lastPos.y && target.x != lastPos.x)
            {
                // Move horizontally to the new column at the previous height first
                Vector2Int horizontalIntermediate = new Vector2Int(target.x, lastPos.y);
                refinedPath.Add(horizontalIntermediate);
            }

            // Then move vertically to the target surface height
            refinedPath.Add(target);
            lastPos = target;
        }

        return refinedPath;
    }
    public void TryMoveGuyTowardDoor()
    {
        List<Vector2Int> path = FindPathToDoor();

        if (path.Count > 0)
        {
            // Optional debug check to see if the path goes all the way
            bool reachesDoor = path[path.Count - 1] == doorGridPosition;
            if (reachesDoor)
            {
                Debug.Log("Path found all the way to the door!");
            }
            else
            {
                Debug.Log("Door is currently blocked, moving as close as possible.");
            }

            StartCoroutine(ExecuteGuyMovement(path));
        }
        else
        {
            Debug.Log("No valid path to the door right now!");
            isBusy = false; // Unlock input immediately if he has nowhere to walk
        }
    }

    System.Collections.IEnumerator ExecuteGuyMovement(List<Vector2Int> path)
    {
        // Save his initial rotation (facing the camera) so we can turn him back to it later
        Quaternion initialRotation = guyInstance.transform.rotation;

        foreach (Vector2Int targetPos in path)
        {
            Vector2Int oldPos = guyGridPosition;

            // Check if this specific step is moving downward (jumping/dropping down a block)
            bool isJumpingDown = targetPos.y < oldPos.y;

            guyGridPosition = targetPos;

            Vector3 worldTarget = GetWorldPosition(targetPos.x, targetPos.y);
            Vector3 startWorldPos = guyInstance.transform.position;

            // Calculate movement direction and flatten the Y axis so he only turns left/right
            Vector3 moveDirection = worldTarget - startWorldPos;
            moveDirection.y = 0f;
            moveDirection = moveDirection.normalized;

            Quaternion startRotation = guyInstance.transform.rotation;
            Quaternion targetRotation = startRotation;

            if (moveDirection != Vector3.zero)
            {
                // Using -moveDirection to match your model's forward orientation
                targetRotation = Quaternion.LookRotation(-moveDirection);
            }

            // Set the correct animation state for this step
            if (guyAnimator != null)
            {
                if (isJumpingDown)
                {
                    guyAnimator.SetBool("IsJumping", true);
                    guyAnimator.SetBool("IsRunning", false);
                }
                else
                {
                    guyAnimator.SetBool("IsJumping", false);
                    guyAnimator.SetBool("IsRunning", true);
                }
            }

            float elapsedTime = 0f;
            float duration = guyMoveDuration;

            while (elapsedTime < duration)
            {
                float t = elapsedTime / duration;

                // Move position smoothly
                guyInstance.transform.position = Vector3.Lerp(startWorldPos, worldTarget, t);

                // Turn smoothly toward the movement direction
                if (moveDirection != Vector3.zero)
                {
                    guyInstance.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
                }

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            guyInstance.transform.position = worldTarget;

            // --- REMOVED `isBusy = false;` from here so it doesn't unlock prematurely ---
        }

        // 2. Stop all movement animations once he finishes the full path
        if (guyAnimator != null)
        {
            guyAnimator.SetBool("IsRunning", false);
            guyAnimator.SetBool("IsJumping", false);
        }

        // 3. Smoothly turn back around to face the camera (initial rotation)
        float turnBackDuration = 0.2f;
        float turnElapsed = 0f;
        Quaternion finalRunRotation = guyInstance.transform.rotation;

        while (turnElapsed < turnBackDuration)
        {
            float t = turnElapsed / turnBackDuration;
            guyInstance.transform.rotation = Quaternion.Slerp(finalRunRotation, initialRotation, t);
            turnElapsed += Time.deltaTime;
            yield return null;
        }
        guyInstance.transform.rotation = initialRotation;

        // --- CHECK IF HE REACHED THE DOOR (FINAL CHECK) ---
        if (guyGridPosition == doorGridPosition)
        {
            Debug.Log("Victory! The guy reached the door!");

            // 1. Stop movement animations and play the cheering animation
            if (guyAnimator != null)
            {
                guyAnimator.SetBool("IsRunning", false);
                guyAnimator.SetBool("IsJumping", false);
                guyAnimator.SetTrigger("Cheer");
            }

            // 2. Trigger your win panel or game over/win state logic
            TriggerWinCondition();
        }
        else
        {
            // Only unlock input if he finished walking and DIDN'T win 
            // (or keep locked if win state handles it)
            isBusy = false;
        }
    }

    public void TriggerWinCondition()
    {
        if (gameState == GameState.Win) return; // Prevent multiple triggers
        isBusy = true;
        gameState = GameState.Win;
        restartButton.SetActive(true);
        winUi.SetActive(true);
        // Stop the ice from thawing
        if (iceBlockTransform != null) { /* your stop logic */ }

        // Fetch the current level safely from LevelManager
        int currentLevel = (LevelManager.Instance != null) ? LevelManager.Instance.currentLevel : 1;

        // --- YOUR CURRENT RUN'S STATS ---
        int finalScore = score;         // Replace with your actual score variable
        float finalElapsedTime = gameTime; // The time it took to win (counting up from 0)

        // Save stats for this specific level
        SaveLevelStats(currentLevel, finalScore, finalElapsedTime);

        Debug.Log($"Level {currentLevel} Won! Stats saved.");
    }

    void SaveLevelStats(int level, int newScore, float newElapsedTime)
    {
        // Generate unique keys for this specific level (e.g., HighScore_Lvl_1, HighScore_Lvl_2)
        string scoreKey = "HighScore_Lvl_" + level;
        string timeKey = "FastestTime_Lvl_" + level;

        // 1. High Score (Higher score is better)
        int savedScore = PlayerPrefs.GetInt(scoreKey, 0);
        if (newScore > savedScore)
        {
            PlayerPrefs.SetInt(scoreKey, newScore);
            Debug.Log($"New High Score for Level {level}! Previous: {savedScore}, New: {newScore}");
        }

        // 2. Fastest Time (Lower elapsed time is better)
        // Use float.MaxValue as default so the first win always sets the initial record
        float savedTime = PlayerPrefs.GetFloat(timeKey, float.MaxValue);

        if (!PlayerPrefs.HasKey(timeKey) || newElapsedTime < savedTime)
        {
            PlayerPrefs.SetFloat(timeKey, newElapsedTime);
            Debug.Log($"New Fastest Time for Level {level}! New: {newElapsedTime:F2}s");
        }

        // Force PlayerPrefs to write to disk
        PlayerPrefs.Save();

        int bestScore = GetSavedHighScore(LevelManager.Instance.currentLevel);
        float bestTime = GetSavedFastestTime(LevelManager.Instance.currentLevel);

        // Format the time text nicely (or show "No Record" if it equals float.MaxValue)
        string timeDisplay = (bestTime == float.MaxValue) ? "--:--" : bestTime.ToString("F2") + "s";
        highscoreText.text = $"Highscore: {bestScore}";
        bestTimeText.text = $"Best time: {timeDisplay}";
    }
    private bool WouldBlockFallOnGuy(Vector2Int targetPos)
    {
        int col = targetPos.x;

        // If the block is being slid into a different column than the guy, it won't fall on him
        if (col != guyGridPosition.x) return false;

        // Find the lowest empty row in this column where gravity would cause the block to land
        int landingRow = 0;
        for (int y = 0; y < height; y++)
        {
            if (grid[col, y] == null)
            {
                landingRow = y;
                break;
            }
        }

        // If the landing row is less than or equal to the guy's current row, 
        // the falling block would land directly on him or crush his position.
        if (landingRow <= guyGridPosition.y)
        {
            return true;
        }

        return false;
    }
    public IEnumerator ApplyGravityToGuyRoutine()
    {
        int col = guyGridPosition.x;
        int targetY = GetSurfaceHeight(col);

        // If the proper surface height is lower than the guy's current Y position, make him fall!
        if (targetY < guyGridPosition.y)
        {
            // 1. Turn on the falling animation
            if (guyAnimator != null)
            {
                guyAnimator.SetBool("IsFalling", true);
            }

            guyGridPosition = new Vector2Int(col, targetY);

            Vector3 startWorldPos = guyInstance.transform.position;
            Vector3 endWorldPos = GetWorldPosition(col, targetY);

            float elapsed = 0f;
            float duration = 0.15f; // Matches the block fall duration

            while (elapsed < duration)
            {
                guyInstance.transform.position = Vector3.Lerp(startWorldPos, endWorldPos, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            guyInstance.transform.position = endWorldPos;

            // 2. Turn off the falling animation the moment he hits the ground
            if (guyAnimator != null)
            {
                guyAnimator.SetBool("IsFalling", false);
            }
        }
    }
    public string GetSmartSpawnId()
    {
        List<string> allAvailableIds = new List<string>();
        foreach (var data in blockPrefabs)
        {
            if (data.prefab != null)
            {
                Block b = data.prefab.GetComponent<Block>();
                if (b != null) allAvailableIds.Add(b.idName);
            }
        }

        if (allAvailableIds.Count == 0) return "";

        int activeBlockCount = 0;
        List<string> doorNearbyBlockIds = new List<string>();
        HashSet<string> boardColors = new HashSet<string>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] != null)
                {
                    activeBlockCount++;
                    string blockId = grid[x, y].idName;
                    boardColors.Add(blockId);

                    // Check if this block is close to the door/exit path
                    float distToDoor = Vector2.Distance(new Vector2Int(x, y), doorGridPosition);
                    if (distToDoor <= 2.5f) // Within 2.5 grid units of the door
                    {
                        doorNearbyBlockIds.Add(blockId);
                    }
                }
            }
        }

        // --- 1. TARGETED ENDGAME GUARANTEE ---
        // Uses the Inspector variable instead of a hardcoded number
        if (activeBlockCount <= doorTargetThreshold && doorNearbyBlockIds.Count > 0)
        {
            return doorNearbyBlockIds[0];
        }

        // --- 2. STANDARD PITY SPAWNING ---
        if (enablePitySpawning && activeBlockCount <= pityBlockThreshold && boardColors.Count > 0)
        {
            List<string> weightedPool = new List<string>();
            foreach (string color in boardColors)
            {
                weightedPool.Add(color);
                weightedPool.Add(color);
            }
            foreach (string color in allAvailableIds)
            {
                weightedPool.Add(color);
            }
            return weightedPool[Random.Range(0, weightedPool.Count)];
        }

        // --- 3. DEFAULT RANDOM ---
        return allAvailableIds[Random.Range(0, allAvailableIds.Count)];
    }
    public int GetRemainingBlockCount()
    {
        int count = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] != null)
                {
                    count++;
                }
            }
        }
        return count;
    }
    public int GetBlockPointValue(string idName)
    {
        foreach (var data in blockPrefabs)
        {
            if (data.prefab != null)
            {
                Block b = data.prefab.GetComponent<Block>();
                if (b != null && b.idName == idName)
                {
                    return data.scoreValue;
                }
            }
        }
        return 100; // Fallback default score if not found
    }
    public string GetFormattedTime()
    {
        int minutes = Mathf.FloorToInt(gameTime / 60f);
        int seconds = Mathf.FloorToInt(gameTime % 60f);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }
    public string GetFormattedTimeTilGameOver()
    {
        int minutes = Mathf.FloorToInt(gameOverTimer / 60f);
        int seconds = Mathf.FloorToInt(gameOverTimer % 60f);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }
    public List<BlockPrefabData> GetActiveBlockPrefabs()
    {
        // Fallback to master list if active list hasn't initialized yet
        if (activeBlockPrefabs == null || activeBlockPrefabs.Count == 0)
        {
            return blockPrefabs;
        }
        return activeBlockPrefabs;
    }

    public static int GetSavedHighScore(int levelNumber)
    {
        return PlayerPrefs.GetInt("HighScore_Lvl_" + levelNumber, 0);
    }

    public static float GetSavedFastestTime(int levelNumber)
    {
        return PlayerPrefs.GetFloat("FastestTime_Lvl_" + levelNumber, float.MaxValue);
    }
    // Checks the entire grid to see if any valid match-making swap OR empty-space slide exists
    public bool HasValidMoves()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == null) continue;

                Vector2Int currentPos = new Vector2Int(x, y);

                // Check all 4 adjacent directions: Right, Left, Down, Up
                Vector2Int[] directions = new Vector2Int[]
                {
                    new Vector2Int(1, 0),  // Right
                    new Vector2Int(-1, 0), // Left
                    new Vector2Int(0, -1), // Down
                    new Vector2Int(0, 1)   // Up
                };

                foreach (var dir in directions)
                {
                    Vector2Int targetPos = currentPos + dir;

                    if (targetPos.x >= 0 && targetPos.x < width && targetPos.y >= 0 && targetPos.y < height)
                    {
                        if (TestMoveOrSwapCreatesMatch(currentPos, targetPos))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    // Tests either a standard swap or sliding into an empty space (enforcing "not up")
    private bool TestMoveOrSwapCreatesMatch(Vector2Int posA, Vector2Int posB)
    {
        Block blockA = grid[posA.x, posA.y];
        Block blockB = grid[posB.x, posB.y];

        if (blockA == null) return false;

        // RULE: If moving into an empty space (blockB == null), check if it's moving UP (posB.y > posA.y)
        if (blockB == null && posB.y > posA.y)
        {
            return false; // Disallow moving up into an empty space
        }

        // Temporarily apply the move or swap on the grid array
        grid[posA.x, posA.y] = blockB;
        grid[posB.x, posB.y] = blockA;

        // Temporarily update internal grid positions for accuracy
        if (blockA != null) blockA.gridPosition = posB;
        if (blockB != null) blockB.gridPosition = posA;

        bool matchFound = ScanBoardForMatches();

        // Revert the grid array data
        grid[posA.x, posA.y] = blockA;
        grid[posB.x, posB.y] = blockB;

        // Revert internal grid positions
        if (blockA != null) blockA.gridPosition = posA;
        if (blockB != null) blockB.gridPosition = posB;

        return matchFound;
    }

    // Shuffles all active blocks on the board until a valid matchable move exists
    public IEnumerator ReplenishAndReshuffleRoutine()
    {
        isBusy = true;
        Debug.Log("Board deadlocked! Deploying blocks and reshuffling until a valid move opens up...");

        yield return new WaitForSeconds(0.5f);

        bool validFound = HasValidMoves();
        int totalAttempts = 0;
        int maxAttempts = 30; // Safety cap to prevent infinite hanging

        // Keep looping: drop blocks, settle gravity, and shuffle until a valid move exists
        while (!validFound && totalAttempts < maxAttempts)
        {
            totalAttempts++;

            // 1. Drop fresh blocks into any columns that have an open space at the top
            bool droppedAny = false;
            for (int x = 0; x < width; x++)
            {
                if (grid[x, height - 1] == null)
                {
                    DropBlockFromPipe();
                    droppedAny = true;
                }
            }

            if (droppedAny)
            {
                yield return new WaitForSeconds(0.2f);
                // 2. Let gravity pack the newly dropped blocks down into the gaps
                yield return StartCoroutine(ApplyGravityRoutine());
                yield return StartCoroutine(ApplyGravityToGuyRoutine());
            }

            // 3. Collect all active blocks on the board (excluding the guy)
            List<Block> activeBlocks = new List<Block>();
            List<Vector2Int> positions = new List<Vector2Int>();

            for (int xGrid = 0; xGrid < width; xGrid++)
            {
                for (int yGrid = 0; yGrid < height; yGrid++)
                {
                    Vector2Int currentPos = new Vector2Int(xGrid, yGrid);
                    if (grid[xGrid, yGrid] != null && !IsCellOccupiedByGuy(currentPos))
                    {
                        activeBlocks.Add(grid[xGrid, yGrid]);
                        positions.Add(currentPos);
                    }
                }
            }

            // Fisher-Yates random shuffle
            for (int i = 0; i < activeBlocks.Count; i++)
            {
                Block tempBlock = activeBlocks[i];
                int randomIndex = UnityEngine.Random.Range(i, activeBlocks.Count);
                activeBlocks[i] = activeBlocks[randomIndex];
                activeBlocks[randomIndex] = tempBlock;
            }

            // Clear grid slots and re-assign shuffled blocks
            foreach (var pos in positions)
            {
                grid[pos.x, pos.y] = null;
            }

            for (int i = 0; i < activeBlocks.Count; i++)
            {
                Vector2Int newPos = positions[i];
                Block block = activeBlocks[i];

                grid[newPos.x, newPos.y] = block;
                block.gridPosition = newPos;
                StartCoroutine(block.MoveToPosition(GetWorldPosition(newPos.x, newPos.y), 0.2f));
            }

            yield return new WaitForSeconds(0.25f);

            // Clear any accidental matches created by the shuffle
            bool accidentalMatch = ScanBoardForMatches();
            while (accidentalMatch)
            {
                yield return StartCoroutine(ApplyGravityRoutine());
                accidentalMatch = ScanBoardForMatches();
            }

            // Check if this layout now has a valid move
            validFound = HasValidMoves();
        }

        // Once a valid move is found, unlock player input so they can play
        isBusy = false;
    }
}
