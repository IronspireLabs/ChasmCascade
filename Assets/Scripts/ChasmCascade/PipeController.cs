using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PipeController : MonoBehaviour
{
    [HideInInspector]
    public int currentColumn = 0;

    [Header("Visual Preview")]
    public GameObject currentPreviewBlock;
    private GameObject nextPrefab;

    [Header("Positioning Adjustments")]
    [Tooltip("How high above the top of the grid the pipe sits.")]
    public float pipeHeightOffset = 2.5f;

    [Tooltip("How far down from the pipe the preview block hangs.")]
    public float previewVerticalOffset = -1.2f;


    IEnumerator Start()
    {
        // Wait until GridManager exists and has finished setting up
        while (GridManager.Instance == null)
        {
            yield return null;
        }

        // Wait one extra frame to ensure GridManager's setup code has fully executed
        yield return null;

        currentColumn = GridManager.Instance.width / 2;

        UpdatePipePosition();
        AdvanceToNextBlock();
    }

    public void MovePipe(int direction)
    {
        if (GridManager.Instance == null) return;

        int maxCols = GridManager.Instance.width;
        currentColumn = Mathf.Clamp(currentColumn + direction, 0, maxCols - 1);
        UpdatePipePosition();
    }

    public void UpdatePipePosition()
    {
        if (GridManager.Instance == null) return;

        // Fetch the exact world position for this column from the grid manager
        Vector3 basePos = GridManager.Instance.GetWorldPosition(currentColumn, GridManager.Instance.height);

        // Position the pipe cleanly above the top row
        transform.position = new Vector3(basePos.x, basePos.y + pipeHeightOffset, basePos.z);
    }

    public void AdvanceToNextBlock()
    {
        if (GridManager.Instance == null) return;

        // FETCH FROM THE ACTIVE SUBSET INSTEAD OF THE MASTER LIST
        var prefabs = GridManager.Instance.GetActiveBlockPrefabs();
        if (prefabs == null || prefabs.Count == 0) return;

        // --- PITY SPAWNING INTEGRATION ---
        string chosenId = GridManager.Instance.GetSmartSpawnId();
        GameObject selectedPrefab = null;

        // Search through the active prefabs list to find the one matching the smart spawn ID
        foreach (var data in prefabs)
        {
            if (data.prefab != null)
            {
                Block b = data.prefab.GetComponent<Block>();
                if (b != null && b.idName == chosenId)
                {
                    selectedPrefab = data.prefab;
                    break;
                }
            }
        }

        // Fallback to purely random from the active subset if anything goes wrong or no ID matched
        if (selectedPrefab == null)
        {
            selectedPrefab = prefabs[Random.Range(0, prefabs.Count)].prefab;
        }

        nextPrefab = selectedPrefab;
        // ---------------------------------

        if (currentPreviewBlock != null)
        {
            Destroy(currentPreviewBlock);
        }

        // Instantiate as a child of the pipe so it tracks movement
        currentPreviewBlock = Instantiate(nextPrefab, transform.position, Quaternion.identity, transform);

        // Push it down locally so it hangs out of the pipe nozzle
        currentPreviewBlock.transform.localPosition = new Vector3(0f, previewVerticalOffset, 0f);

        // --- FIX: Prevent the Block script from hijacking the preview's position ---
        Block blockScript = currentPreviewBlock.GetComponent<Block>();
        if (blockScript != null)
        {
            Destroy(blockScript); // Removes the gameplay logic so it stays purely visual in the pipe
        }
    }

    public GameObject ConsumeCurrentPreview()
    {
        GameObject prefabToDrop = nextPrefab;
        AdvanceToNextBlock();
        return prefabToDrop;
    }


    // Expose current column safely
    public int CurrentColumn => currentColumn;

    /// <summary>
    /// Finds the closest grid column by checking the actual world X position of each column from the GridManager.
    /// </summary>
    public int GetColumnFromWorldX(float worldX)
    {
        int bestColumn = 0;
        float closestDistance = float.MaxValue;

        // Loop through all columns and find which one is closest to the mouse/touch world position
        for (int x = 0; x < GridManager.Instance.width; x++)
        {
            // Ask GridManager where this column is located
            Vector3 colPos = GridManager.Instance.GetWorldPosition(x, GridManager.Instance.height - 1);
            float distance = Mathf.Abs(colPos.x - worldX);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                bestColumn = x;
            }
        }

        return Mathf.Clamp(bestColumn, 0, GridManager.Instance.width - 1);
    }

    /// <summary>
    /// Snaps the pipe to the correct column's X coordinate while keeping its current Y and Z height.
    /// </summary>
    public void SetPipeColumn(int targetColumn)
    {
        currentColumn = Mathf.Clamp(targetColumn, 0, GridManager.Instance.width - 1);

        // Get the exact world position from GridManager for this column
        Vector3 targetWorldPos = GridManager.Instance.GetWorldPosition(currentColumn, GridManager.Instance.height - 1);

        // Update only the X coordinate, keeping the pipe's vertical placement intact
        transform.position = new Vector3(targetWorldPos.x, transform.position.y, transform.position.z);
    }
}