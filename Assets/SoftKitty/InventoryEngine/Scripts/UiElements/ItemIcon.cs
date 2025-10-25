using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SoftKitty.InventoryEngine
{
    public class ItemIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public enum IconType
        {
            Reference,
            Link,
            Item
        }
        [Header("(Hover the attributes to read the tooltips)")]
        [Tooltip("Reference: Only for showing information about an item.\n" +
            "| Link: Shortcut to an item; it updates its information when the stats of the linked item change.\n" +
            "| Item: Represents an item carried by an <InventoryHolder>.\n")]
        public IconType Type;
        
        [Tooltip("The hover information panel will align with this RectTransform")]
        public RectTransform HoverInfoAnchorPoint;
        [Tooltip("The hover information will promo player to right click with this hint text")]
        public string RightClickActionHint="Use";

        [Tooltip("The border with color of the item quality")]
        public Image Frame;
        [Tooltip("The background with color of the item category")]
        public Image Background;
        [Tooltip("A small mark on the top-left corner to indicate if this item is in player's favorite group.")]
        public GameObject Fav;
        [Tooltip("The glowing border will show up when item is selected")]
        public Image Outline;
        [Header("[Misc references]")]
        public RawImage Icon;
        public RawImage GlowIcon;
        public Image Hover;
        public Text NumberText;
        public Text UpgradeText;
        public Text NameText;
        public Text DescriptionText;
        public Text TypeText;
        public Text QualityText;
        public RectTransform Rect;
        public CanvasGroup Group;
        [Header("[Important Settings]")]
        [Tooltip("Determines if this item slot reacts to pointer hover events.")]
        public bool CanHover = true;
        [Tooltip("Indicates whether this item slot can be dragged to the shortcut slots.")]
        public bool CanBeLinked = true;

        #region Variables
        [HideInInspector]
        public bool Enabled = true;
        [HideInInspector]
        public float PriceMultiplier = 1F;
        public bool Visible
        {
            get
            {
                return isVisible;
            }
        }
        public delegate void OnItemClick(int _index, int _button);
        protected OnItemClick ClickCallback;
        protected int ItemClickId;
        protected bool OutlineVisible = false;
        protected float OutlineTimer = 0F;
       

        protected Texture emptyTexture
        {
            get
            {
                if(_emptyTexture==null) _emptyTexture= Icon.mainTexture;
                return _emptyTexture;
            }
        }
        private Texture _emptyTexture;
        protected bool isHover = false;
        protected bool isVisible = true;
        protected int number = 0;
        protected int upgrade = -1;
        protected int itemId = -2;
        protected bool inited = false;
        protected bool Empty = false;
        #endregion

        #region Internal Methods
        void Awake()
        {
            _emptyTexture = Icon.mainTexture;
        }

        void Update()
        {
            Icon.transform.localScale = Vector3.Lerp(Icon.transform.localScale, Vector3.one * (isHover ? 1F : 0.9F), Time.unscaledDeltaTime * 8F);
            OutlineTimer = Mathf.MoveTowards(OutlineTimer,0F,Time.unscaledDeltaTime);
            if (GlowIcon) GlowIcon.transform.localScale = new Vector3(Icon.transform.localScale.x*1.1F, Icon.transform.localScale.y,1F);
            if (Outline)
            {
                Outline.gameObject.SetActive(OutlineVisible || OutlineTimer > 0F);
                Outline.color = new Color(Outline.color.r, Outline.color.g, Outline.color.b, OutlineVisible?1F: Mathf.Clamp01(OutlineTimer*2F));
            }
            DoUpdate();
        }

        public void Click()
        {
            if (Empty || itemId < 0 || !ItemManager.TryGetItem(itemId).useable) return;
            Icon.transform.localScale = Vector3.one * 0.5F;
            if(GlowIcon) GlowIcon.transform.localScale = new Vector3(Icon.transform.localScale.x * 1.1F, Icon.transform.localScale.y, 1F);
            Instantiate(Resources.Load<GameObject>("InventoryEngine/ClickEffect"), transform.position, Quaternion.identity, WindowsManager.GetMainCanvas().transform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHover = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (CanHover) isHover = true;
            OnHover();
            SoundManager.Play2D("bt_hover");
        }


        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                OnLeftClick();
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                OnRightClick();
            }
            else if (eventData.button == PointerEventData.InputButton.Middle)
            {
                OnMiddleClick();
            }
            SoundManager.Play2D("bt_down");
        }

        public virtual void ResetState()
        {
            SetItemId(-2);
            number = 0;
            upgrade = -1;
            ToggleOutline(false);
            SetFavorate(false);
        }
        public virtual void OnHover()
        {
            if (HoverInfoAnchorPoint != null && itemId >= 0) HoverInformation.ShowHoverInfo(this, ItemManager.TryGetItem(itemId).Copy(), 0, HoverInfoAnchorPoint, PriceMultiplier, RightClickActionHint,false,false,false);
        }

        public virtual void OnLeftClick()
        {
            if (ClickCallback != null) ClickCallback(ItemClickId, 0);
        }

        public virtual void OnRightClick()
        {
            if (ClickCallback != null) ClickCallback(ItemClickId, 1);
        }

        public virtual void OnMiddleClick()
        {
            if (ClickCallback != null) ClickCallback(ItemClickId, 2);
        }

        public virtual void EndDrag(int _add)
        {

        }

        public virtual void DoUpdate()
        {

        }
        #endregion

        /// <summary>
        /// Retrieves the InventoryStack data of this item.
        /// </summary>
        /// <returns></returns>
        public virtual InventoryStack GetStackData()
        {
            return null;
        }

        /// <summary>
        /// Retrieves the InventoryHolder of this slot.
        /// </summary>
        /// <returns></returns>
        public virtual InventoryHolder GetStackHolder()
        {
            return null;
        }

        /// <summary>
        /// Retrieves the Item in this slot.
        /// </summary>
        /// <returns></returns>
        public virtual Item GetItem()
        {
            return null;
        }

        /// <summary>
        /// Registers a callback for when this icon is clicked.
        /// </summary>
        /// <param name="_id"></param>
        /// <param name="_callback"></param>
        public void RegisterClickCallback(int _id, OnItemClick _callback)
        {
            ItemClickId = _id;
            ClickCallback = _callback;
        }

        /// <summary>
        /// Returns if this slot is empty.
        /// </summary>
        /// <returns></returns>
        public bool isEmpty()
        {
            return Empty;
        }

        /// <summary>
        /// Returns the number of items in the slot.
        /// </summary>
        /// <returns></returns>
        public int GetNumber()
        {
            return number;
        }

        /// <summary>
        /// Returns the item ID. Returns -1 if the slot is empty.
        /// </summary>
        /// <returns></returns>
        public int GetItemId()
        {
            return itemId;
        }

        /// <summary>
        /// Returns the item category id. Returns -1 if the slot is empty.
        /// </summary>
        /// <returns></returns>
        public int GetCategory()
        {
            if (ItemManager.TryGetItem(itemId) != null)
                return ItemManager.TryGetItem(itemId).type;
            else
                return -1;
        }

        /// <summary>
        /// Returns whether this item slot is hovered over by the mouse.
        /// </summary>
        /// <returns></returns>
        public bool GetHover()
        {
            return isHover;
        }

        /// <summary>
        /// Overrides the item ID.
        /// </summary>
        /// <param name="_id"></param>
        public void SetItemId(int _id)
        {
            itemId = _id;
        }

        /// <summary>
        /// Sets the number of items in the slot.
        /// </summary>
        /// <param name="_num"></param>
        public void SetItemNumber(int _num)
        {
            if (NumberText) NumberText.text = _num.ToString();
            number = _num;
        }

        /// <summary>
        /// Sets the upgrade level of the item.
        /// </summary>
        /// <param name="_level"></param>
        public void SetUpgradeLevel(int _level)
        {
            if (UpgradeText) UpgradeText.text = _level > 0 ? "+" + _level.ToString() : "";
            if (GlowIcon )
            {
                if (ItemManager.instance.EnableEnhancingGlow)
                {
                    GlowIcon.gameObject.SetActive(_level > 0);
                    GlowIcon.color = Color.Lerp(Color.black, new Color(1F, 0.6F, 0.04F, 1F), ItemManager.instance.EnhancingGlowCurve.Evaluate(Mathf.Clamp01(_level * 1F / ItemManager.instance.MaxiumEnhancingLevel)));
                }
                else
                {
                    GlowIcon.gameObject.SetActive(false);
                }
             }
             upgrade = _level;
        }

        /// <summary>
        /// Sets the icon and colors for the slot.
        /// </summary>
        /// <param name="_icon"></param>
        /// <param name="_backgroundColor"></param>
        /// <param name="_frameColor"></param>
        /// <param name="_numVisible"></param>
        /// <param name="_upgradeVisible"></param>
        public void SetAppearance(Texture _icon,Color _backgroundColor , Color _frameColor , bool _numVisible=false, bool _upgradeVisible=false)
        {
            Icon.texture = _icon;
            if (GlowIcon) GlowIcon.texture = _icon;
            if (Frame) Frame.color = _frameColor;
            Background.color = _backgroundColor;
            if (NumberText) NumberText.gameObject.SetActive(_numVisible);
            if (UpgradeText) UpgradeText.gameObject.SetActive(_upgradeVisible);

        }

        /// <summary>
        /// Sets the icon and colors for the slot.
        /// </summary>
        /// <param name="_item"></param>
        /// <param name="_numVisible"></param>
        /// <param name="_upgradeVisible"></param>
        public void SetAppearance(Item _item, bool _numVisible = false, bool _upgradeVisible = false)
        {
            Icon.texture = _item.GetIcon();
            if (GlowIcon) GlowIcon.texture = Icon.texture;
            if (Frame) Frame.color = _item.GetQualityColor();
            if (Background) Background.color = _item.GetTypeColor();

            if (NameText) NameText.text = _item.nameWithAffixing + (_item.upgradeLevel > 0 ? " +" + _item.upgradeLevel.ToString() : "");
            if(DescriptionText) DescriptionText.text = _item.description;
            if (TypeText) TypeText.text = "[ " + _item.GetTypeName() + " ]";
            if (TypeText) TypeText.color = _item.GetTypeColor();
            if (QualityText) QualityText.text = _item.GetQualityName();
            if (QualityText) QualityText.color = _item.GetQualityColor();

            if (NumberText) NumberText.gameObject.SetActive(_numVisible);
            if (UpgradeText) UpgradeText.gameObject.SetActive(_upgradeVisible);
        }

        /// <summary>
        /// Sets the slot to empty.
        /// </summary>
        public void SetEmpty()
        {
            ResetState();
            Empty = true;
            if (emptyTexture != null) Icon.texture = emptyTexture;
            if (Frame) Frame.color = InventorySkin.instance.EmptyItemBackColor;
            if (Background) Background.color = InventorySkin.instance.EmptyItemBackColor;
            if (NumberText)  NumberText.text = "0";
            if (UpgradeText) UpgradeText.text = "";
            if (NumberText) NumberText.gameObject.SetActive(false);
            if (UpgradeText) UpgradeText.gameObject.SetActive(false);
            if (GlowIcon) GlowIcon.gameObject.SetActive(false);
            inited = true;
        }

        /// <summary>
        /// Toggles the outline effect of this slot.
        /// </summary>
        /// <param name="_visible"></param>
        public void ToggleOutline(bool _visible)
        {
            OutlineVisible = _visible;
        }

        /// <summary>
        /// Show the outline effect for x seconds.
        /// </summary>
        /// <param name="_time"></param>
        public void ShowOutline(float _time)
        {
            OutlineTimer = _time;
        }

        /// <summary>
        /// Toggles whether this item is marked as a favorite.
        /// </summary>
        /// <param name="_favorate"></param>
        public void SetFavorate(bool _favorate)
        {
            Fav.SetActive(_favorate);
        }

        /// <summary>
        /// Toggles this slot between fully visible or half-transparent.
        /// </summary>
        /// <param name="_visible"></param>
        public void SetVisible(bool _visible)
        {
            isVisible = _visible;
            Group.alpha = isVisible ? 1F : 0.1F;
        }

        /// <summary>
        /// //Returns whether the item matches the provided tag.
        /// </summary>
        /// <param name="_tag"></param>
        /// <returns></returns>
        public bool isTagMatchText(string _tag)
        {
            if (itemId <= 0) return false;
            return ItemManager.TryGetItem(itemId).isTagMatchText(_tag);
        }

        /// <summary>
        /// Returns whether the item's tags matches the provided list.
        /// </summary>
        /// <param name="_tags"></param>
        /// <param name="_allMatch"></param>
        /// <returns></returns>
        public bool isTagsMatchList(List<string> _tags, bool _allMatch = true)
        {
            if (itemId <= 0) return false;
            return ItemManager.TryGetItem(itemId).isTagsMatchList(_tags, _allMatch);
        }

        /// <summary>
        /// Returns whether the item has any tag contains the provided text.
        /// </summary>
        /// <param name="_text"></param>
        /// <param name="_caseSensitive"></param>
        /// <returns></returns>
        public bool isTagContainText(string _text, bool _caseSensitive = true)
        {
            if (itemId <= 0) return false;
            return ItemManager.TryGetItem(itemId).isTagContainText(_text, _caseSensitive);
        }

        /// <summary>
        /// Returns the tag of the item which contains the provided text.
        /// </summary>
        /// <param name="_text"></param>
        /// <param name="_caseSensitive"></param>
        /// <returns></returns>
        public string GetTagContainText(string _text, bool _caseSensitive = true)
        {
            if (itemId <= 0) return "";
            return ItemManager.TryGetItem(itemId).GetTagContainText(_text, _caseSensitive);
        }

    }
}
