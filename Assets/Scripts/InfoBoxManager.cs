using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

[System.Serializable]
public class InfoBox
{
    public string infoTitle;
    public string infoMessage;
    public Sprite infoSprite;
}

public class InfoBoxManager : MonoBehaviour
{
    [Header("UI Elements")]
    public bool isOpen;
    public Image infoImage;
    public TextMeshProUGUI infoTitleText;
    public TextMeshProUGUI infoMessageText;
    public Animator animator;

    public bool? choix = null; // null = pas encore choisi, true = oui, false = non

    public static InfoBoxManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void OpenInfoBox(string infoTitle, string infoMessage, Sprite infoSprite)
    {
        isOpen = true;
        choix = null;

        animator?.SetBool("isOpen", true);

        // Configure UI
        infoTitleText.text = infoTitle ?? "";
        infoMessageText.text = infoMessage ?? "";

        if (infoImage != null && infoSprite != null)
        {
            infoImage.sprite = infoSprite;
            infoImage.gameObject.SetActive(true);
        }
        else if (infoImage != null)
        {
            infoImage.gameObject.SetActive(false);
        }

        InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.Player.Get(), InputsManager.Instance.playerInputs.InfoBox.Get());

        var infoBox = InputsManager.Instance.playerInputs.InfoBox;
        infoBox.Confirm.canceled += OnConfirm;
        infoBox.Cancel.canceled += OnCancel;
    }

    public void CloseInfoBox()
    {
        isOpen = false;
        animator?.SetBool("isOpen", false);

        InputsManager.Instance.ActivateOnly(InputsManager.Instance.playerInputs.Player.Get());

        var infoBox = InputsManager.Instance.playerInputs.InfoBox;
        infoBox.Confirm.canceled -= OnConfirm;
        infoBox.Cancel.canceled -= OnCancel;
    }

    private void OnConfirm(InputAction.CallbackContext context)
    {
        if (!isOpen || choix.HasValue) return;

        choix = true;
        CloseInfoBox();
        Debug.Log("Action confirmée : " + choix.Value);
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        if (!isOpen || choix.HasValue) return;

        choix = false;
        CloseInfoBox();
        Debug.Log("Action annulée : " + choix.Value);
    }
}
