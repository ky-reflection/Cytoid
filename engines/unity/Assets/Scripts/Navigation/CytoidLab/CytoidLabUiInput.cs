using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Cytoid Lab UI input setup. Project uses Input System Package only; legacy
/// <see cref="StandaloneInputModule"/> does not receive keyboard events.
/// </summary>
public static class CytoidLabUiInput
{
    public static void EnsureEventSystem()
    {
        var eventSystem = Object.FindObjectOfType<EventSystem>();
        if (eventSystem == null)
        {
            var go = new GameObject("EventSystem");
            eventSystem = go.AddComponent<EventSystem>();
        }

        var legacy = eventSystem.GetComponent<StandaloneInputModule>();
        if (legacy != null)
        {
            Object.Destroy(legacy);
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    public static void ClearUiSelection()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    /// <summary>Prevent Space/Enter from activating focused buttons; Lab shortcuts own the keyboard.</summary>
    public static void DisableKeyboardNavigation(Selectable selectable)
    {
        if (selectable == null) return;
        selectable.navigation = new Navigation { mode = Navigation.Mode.None };
    }
}
