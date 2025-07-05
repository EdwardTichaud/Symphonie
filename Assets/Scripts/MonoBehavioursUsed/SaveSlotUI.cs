using UnityEngine;
using UnityEngine.EventSystems;

public class SaveSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    private SaveInfo info;
    private SaveLoadMenu menu;

    public void Init(SaveInfo info, SaveLoadMenu menu)
    {
        this.info = info;
        this.menu = menu;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        menu.DisplayInfo(info);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        menu.LoadSave(info.saveName);
    }
}
