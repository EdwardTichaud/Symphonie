using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.InventoryEngine
{
    /// <summary>
    /// This module displays the total attributes of an InventoryHolder. 
    /// Attach this script to a UI panel, assign the necessary UI elements,
    /// and call Initialize (InventoryHolder _equipHolder, Attribute[] _baseAttributes) to display the stats. 
    /// _baseAttributes represents the player's attributes without any equipment.
    /// </summary>
    public class StatsUi : MonoBehaviour
    {
        #region Variables
        public GameObject StatPrefab;
        private InventoryHolder Holder;
        private Dictionary<string, ListItem> StatList=new Dictionary<string, ListItem>();
        private Color StatColor;
        private bool Inited = false;
        #endregion

        #region MonoBehaviour
        private void Update()
        {
            if (Holder == null || !Inited) return;

            for (int i = 0; i < ItemManager.instance.itemAttributes.Count; i++)
            {
                if (ItemManager.instance.itemAttributes[i].visibleInStatsPanel && ItemManager.instance.itemAttributes[i].isNumber())
                {
                    float _currentValue = GetValue(ItemManager.instance.itemAttributes[i].key);
                    if (StatList[ItemManager.instance.itemAttributes[i].key].mValue != _currentValue)
                    {
                        if (StatList[ItemManager.instance.itemAttributes[i].key].mValue < _currentValue)
                        {
                            StartCoroutine(PopStat(StatList[ItemManager.instance.itemAttributes[i].key].mTexts[1],Color.green));
                        }
                        else
                        {
                            StartCoroutine(PopStat(StatList[ItemManager.instance.itemAttributes[i].key].mTexts[1], Color.red));
                        }
                        StatList[ItemManager.instance.itemAttributes[i].key].mValue = _currentValue;
                        StatList[ItemManager.instance.itemAttributes[i].key].mTexts[1].text = (Mathf.RoundToInt( _currentValue*10)*0.1F).ToString()+ ItemManager.instance.itemAttributes[i].suffixes;
                    }
                }
            }
        }
        #endregion

        #region Internal Methods

        IEnumerator PopStat(Text _stat, Color _popColor)
        {
            float t = 0F;
            while (t<1F) {
                t += Time.unscaledDeltaTime * 10F;
                _stat.color = Color.Lerp(StatColor, _popColor,t);
                _stat.transform.localScale = Vector3.one * (1F + t * 0.3F);
                yield return 1;
            }
            yield return new WaitForSecondsRealtime(0.5F);
            while (t > 0F)
            {
                t -= Time.unscaledDeltaTime*3F;
                _stat.color = Color.Lerp(StatColor, _popColor, t);
                _stat.transform.localScale = Vector3.one * (1F + t * 0.3F);
                yield return 1;
            }
            _stat.color = StatColor;
            _stat.transform.localScale = Vector3.one;
        }
        private float GetValue(string _key)
        {
            return Holder.GetAttributeValue(_key,true);
        }
        #endregion

        /// <summary>
        /// Initialize to display the stats._baseAttributes represents the player's attributes without any equipment.
        /// </summary>
        /// <param name="_equipHolder"></param>
        /// <param name="_baseAttributes"></param>
        public void Init(InventoryHolder _equipHolder)
        {
            StatColor = StatPrefab.GetComponent<ListItem>().mTexts[1].color;
            Holder = _equipHolder;
            for(int i=0;i<ItemManager.instance.itemAttributes.Count;i++) {
                if (ItemManager.instance.itemAttributes[i].visibleInStatsPanel && ItemManager.instance.itemAttributes[i].isNumber() && !StatList.ContainsKey(ItemManager.instance.itemAttributes[i].key))
                {
                    GameObject _newStat = Instantiate(StatPrefab, StatPrefab.transform.parent);
                    _newStat.transform.localScale = Vector3.one;
                    _newStat.GetComponent<ListItem>().mTexts[0].text = ItemManager.instance.itemAttributes[i].name+" : ";
                    _newStat.SetActive(true);
                    _newStat.GetComponent<ListItem>().mValue = GetValue(ItemManager.instance.itemAttributes[i].key);
                    _newStat.GetComponent<ListItem>().mTexts[1].text = (Mathf.RoundToInt(GetValue(ItemManager.instance.itemAttributes[i].key) * 10) * 0.1F).ToString()+ ItemManager.instance.itemAttributes[i].suffixes;
                    StatList.Add(ItemManager.instance.itemAttributes[i].key,_newStat.GetComponent<ListItem>());
                }
            }
                Inited = true;
        }
       


    }
}
