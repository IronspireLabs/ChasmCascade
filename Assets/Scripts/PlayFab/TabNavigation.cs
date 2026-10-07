using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TabNavigation : MonoBehaviour
{
    [Header("UI Input Fields (In Navigation Order)")]
    [SerializeField] private InputField[] inputFields;

    private EventSystem system;
    private PlayFabAuthManager playFab;

    public int input;

    private void Start()
    {
        system = EventSystem.current;
        playFab = GetComponent<PlayFabAuthManager>();
        SelectField(inputFields[0]);
    }

    private void Update()
    {
        // Check if the Tab key was pressed
        if (Input.GetKeyDown(KeyCode.Tab))
            HandleTabNavigation();

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            // Check if the user is currently on the password field or last field
            if (input == 1)
                playFab.LoginAccount();
            if (input == 2)
                playFab.RegisterAccount();
        }

        if (inputFields[0].isFocused)
            input = 0;

        if (inputFields[1].isFocused)
            input = 1;

        if (inputFields[2].isFocused)
            input = 2;
    }

    private void HandleTabNavigation()
    {
        if (inputFields == null || inputFields.Length == 0) return;

        /* Find which input field currently has focus
        input = -1;
        for (int i = 0; i < inputFields.Length; i++)
        {
            if (inputFields[i].isFocused)
            {
                input = i;
                break;
            }
        }

        // If no field is currently focused, focus the first one
        if (input == 0)
        {
            SelectField(inputFields[0]);
            return;
        }
                */

        // Check if Shift is held down for backwards tabbing
        bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        int nextIndex;
        if (isShiftHeld)
        {
            // Move backward (wrap around to the end if at index 0)
            nextIndex = input - 1;
            if (nextIndex < 0) nextIndex = inputFields.Length - 1;
        }
        else
        {
            // Move forward (wrap around to the start if at the end)
            nextIndex = (input + 1) % inputFields.Length;
        }

        SelectField(inputFields[nextIndex]);
    }

    private void SelectField(InputField targetField)
    {
        targetField.Select();
        targetField.ActivateInputField(); // Shows the typing cursor immediately
    }
}
