using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class NPCInteractable : MonoBehaviour
{
    private DialogUIManager dialogManager;
    private NPCCharacter npc;

    void Start()
    {
        dialogManager = FindFirstObjectByType<DialogUIManager>();
        npc = GetComponent<NPCCharacter>();

        XRBaseInteractable interactable = GetComponent<XRBaseInteractable>();
        interactable.selectEntered.AddListener(OnSelect);
    }

    void OnSelect(SelectEnterEventArgs args)
    {
        if (dialogManager != null && npc != null)
        {
            Debug.Log("selected");
            dialogManager.SelectNPC(npc);
        }
    }
}