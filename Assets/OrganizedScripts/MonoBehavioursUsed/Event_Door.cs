using UnityEngine;
using System.Collections;

public class Event_Door : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        Debug.Log("Interacting with door: " + gameObject.name);
        transform.GetChild(0).GetComponent<Animator>().SetTrigger("open");
    }

    /// IInteractable implementation
    public GameObject GameObject { get; }
    public void IncrementDialogueStage()
    {

    }
}
