using UnityEngine;
using UnityEngine.EventSystems;

public class PipeInputHandler : MonoBehaviour
{
    private PipeController pipeController;
    private bool isDragging = false;

    public AudioSource sfxAudio;
    public AudioClip movePipeClip;
    private int lastColumn = -1;

    [Header("Raycast Settings")]
    public LayerMask blockLayerMask; // Assign your Block layer in the Inspector so we can ignore pipes dragging over blocks

    void Start()
    {
        pipeController = GetComponent<PipeController>();
        if (pipeController == null)
        {
            Debug.LogError("PipeInputHandler requires a PipeController component on the same GameObject!");
        }
    }

    void Update()
    {
        if (pipeController == null || Camera.main == null) return;

        // 1. Keyboard Controls (Still active for editor debugging)
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            pipeController.MovePipe(-1);
            PlayMoveSound();
        }
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            pipeController.MovePipe(1);
            PlayMoveSound();
        }

        // 2. Continuous Drag Controls for Mouse & Touch
#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseDrag();
#else
        HandleTouchDrag();
#endif
    }

    void HandleMouseDrag()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // If clicking on UI (like buttons, menus), do not start dragging the pipe
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            // If clicking directly on a block, do not start dragging the pipe
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, blockLayerMask))
            {
                return;
            }

            // Safe to start dragging the pipe from anywhere else on screen!
            isDragging = true;
            lastColumn = pipeController.CurrentColumn;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            UpdatePipeDragPosition(Input.mousePosition);
        }
    }

    void HandleTouchDrag()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                // If touching UI elements, ignore
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                    return;

                // If touching a block, ignore
                Ray ray = Camera.main.ScreenPointToRay(touch.position);
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, blockLayerMask))
                {
                    return;
                }

                // Safe to start dragging the pipe
                isDragging = true;
                lastColumn = pipeController.CurrentColumn;
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isDragging = false;
            }

            if (isDragging && (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary))
            {
                UpdatePipeDragPosition(touch.position);
            }
        }
    }

    void UpdatePipeDragPosition(Vector2 screenPos)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPos);

        // Create a plane facing the camera at the pipe/board's Z depth
        Plane boardPlane = new Plane(Vector3.back, transform.position);

        if (boardPlane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);

            // Tell the controller to move to the grid column corresponding to this world X position
            int targetColumn = pipeController.GetColumnFromWorldX(worldPoint.x);

            if (targetColumn != pipeController.CurrentColumn)
            {
                pipeController.SetPipeColumn(targetColumn);
                PlayMoveSound();
            }
        }
    }

    void PlayMoveSound()
    {
        if (sfxAudio != null && movePipeClip != null)
        {
            sfxAudio.PlayOneShot(movePipeClip);
        }
    }
}