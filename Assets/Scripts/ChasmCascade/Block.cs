using System.Collections;
using UnityEngine;

public class Block : MonoBehaviour
{
    public Vector2Int gridPosition;

    public string idName = "Red"; // e.g., "Circle", "Square", "Triangle", or an enum/int ID

    public bool blockInitialised;

    private void Update()
    {
        if(GridManager.Instance != null && !blockInitialised)
        {
            GridManager.Instance.RegisterBlock(this, gridPosition.x, gridPosition.y);
            blockInitialised = true;
        }
    }

    // Smoothly animate the block sliding to its new world position
    private Coroutine moveCoroutine;

    public IEnumerator MoveToPosition(Vector3 targetPosition, float duration)
    {
        // If this block is already moving, stop the previous movement immediately
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
        }

        moveCoroutine = StartCoroutine(MoveRoutine(targetPosition, duration));
        yield return moveCoroutine;
    }

    private IEnumerator MoveRoutine(Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(startPosition, targetPosition, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPosition;
        moveCoroutine = null;
    }
}
