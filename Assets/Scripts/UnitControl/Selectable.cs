using UnityEngine;

public class Selectable : MonoBehaviour
{
    [Header("Selection Visual")]
    public GameObject selectionRing;

    public void SetSelected(bool on)
    {
        if (selectionRing != null) selectionRing.SetActive(on);
    }
}
