using UnityEngine;

public class Block3DSwipeHandler : MonoBehaviour
{
    private GameObject selectedBlock;
    private Vector2 pressPosition;
    private Vector2 releasePosition;

    [Header("Swipe Settings")]
    [Tooltip("Minimum pixel distance required to register a swipe.")]
    public float swipeThreshold = 50f;

    [Tooltip("Layer mask to filter raycasts only to 3D blocks.")]
    public LayerMask blockLayer;
    public AudioSource sfxAudio;
    public AudioClip moveBlockClip;
    void Update()
    {
        DetectBlockSwipe();
    }

    void DetectBlockSwipe()
    {
        // 1. Mouse/Touch Down: Raycast into the 3D scene to select a specific block
        if (Input.GetMouseButtonDown(0))
        {
            pressPosition = Input.mousePosition;
            selectedBlock = GetBlockFromRaycast(pressPosition);
        }

        // 2. Mouse/Touch Up: If we have a selected block, calculate the swipe direction
        if (Input.GetMouseButtonUp(0) && selectedBlock != null)
        {
            releasePosition = Input.mousePosition;
            EvaluateSwipeDirection(selectedBlock);

            // Reset selection
            selectedBlock = null;
        }
    }

    GameObject GetBlockFromRaycast(Vector2 screenPosition)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        RaycastHit hit;

        // Cast ray into the 3D scene checking only the block layer
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, blockLayer))
        {
            return hit.collider.gameObject;
        }

        return null;
    }

    void EvaluateSwipeDirection(GameObject block)
    {
        Vector2 distanceVector = releasePosition - pressPosition;

        // Ensure the drag exceeds the minimum pixel threshold to count as a swipe
        if (distanceVector.magnitude > swipeThreshold)
        {
            // Determine if horizontal or vertical swipe is dominant
            if (Mathf.Abs(distanceVector.x) > Mathf.Abs(distanceVector.y))
            {
                if (distanceVector.x > 0)
                {
                    OnSwipeRight(block);
                }
                else
                {
                    OnSwipeLeft(block);
                }
            }
            else
            {
                if (distanceVector.y > 0)
                {
                    OnSwipeUp(block);
                }
                else
                {
                    OnSwipeDown(block);
                }
            }
        }
        else
        {
            // Optional: Handle as a simple tap if desired
            OnBlockTapped(block);
        }
    }

    // --- Action Handlers (Connect these to your grid/matching script) ---

    void OnSwipeRight(GameObject block)
    {
        GridManager.Instance.SwapBlock(block, Vector2Int.right);   // (1, 0)
        sfxAudio.PlayOneShot(moveBlockClip);
    }

    void OnSwipeLeft(GameObject block)
    {
        GridManager.Instance.SwapBlock(block, Vector2Int.left);    // (-1, 0)
        sfxAudio.PlayOneShot(moveBlockClip);
    }

    void OnSwipeUp(GameObject block)
    {
        GridManager.Instance.SwapBlock(block, Vector2Int.up);      // (0, 1)
        sfxAudio.PlayOneShot(moveBlockClip);
    }

    void OnSwipeDown(GameObject block)
    {
        GridManager.Instance.SwapBlock(block, Vector2Int.down);    // (0, -1)
        sfxAudio.PlayOneShot(moveBlockClip);
    }

    void OnBlockTapped(GameObject block)
    {
        Debug.Log($"Tapped 3D Block: {block.name}");
    }
}
