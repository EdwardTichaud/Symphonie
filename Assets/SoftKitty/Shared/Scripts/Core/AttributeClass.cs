using UnityEngine;

namespace SoftKitty
{
    [System.Serializable]
    public class Attribute
    {
        /// <summary>
        /// The key of this attribute. You will need this key to access speficied Attribute from Item class.
        /// </summary>
        public string key;
        /// <summary>
        /// The display name of this attribute.
        /// </summary>
        public string name;
        /// <summary>
        /// The value string of this attribute
        /// </summary>
        public string value = "";
        /// <summary>
        /// Whether this attribute a string. For example, an attribute can be the name of the creator, that will make this attribute a string.
        /// </summary>
        public bool stringValue = false;
        /// <summary>
        /// Increment value when this attribute leveled up.
        /// </summary>
        public float upgradeIncrement = 0F;
        /// <summary>
        /// Whether this attribute should display as bold font and sort before other stats.
        /// </summary>
        public bool coreStats = false;
        /// <summary>
        /// Whether this attribute visible in the hover information panel.
        /// </summary>
        public bool visible = true;
        /// <summary>
        /// Whether this attribute visible in the stats panel.
        /// </summary>
        public bool visibleInStatsPanel = true;
        /// <summary>
        /// The display format type in the hover information panel.
        /// </summary>
        public int displayFormat = 0;
        /// <summary>
        /// Suffixes string when display this attribute.
        /// </summary>
        public string suffixes = "";
        /// <summary>
        /// Whether display compare information for this attribute in mouse hover information panel.
        /// </summary>
        public bool compareInfo = true;
        /// <summary>
        /// Random chance to unlock this attribute when item being created.
        /// </summary>
        public int randomChange = 100;
        /// <summary>
        /// Whether this attribute is Locked, locked attribute is not valid and invisible.
        /// </summary>
        public bool locked = false;
        /// <summary>
        /// Whether this attribute has fixed value.
        /// </summary>
        public bool isFixed = true;
        /// <summary>
        /// The minimal value of this attribute if this attribute has random value.
        /// </summary>
        public float minValue = 0F;
        /// <summary>
        /// The maximum value of this attribute if this attribute has random value.
        /// </summary>
        public float maxValue = 0F;



        [HideInInspector]
        public bool fold = true;



        /// <summary>
        /// Get an instance of this attribute.
        /// </summary>
        /// <returns></returns>
        public Attribute Copy()
        {
            Attribute _newAtt = new Attribute();
            _newAtt.key = key;
            _newAtt.name = name;
            _newAtt.value = value;
            _newAtt.stringValue = stringValue;
            _newAtt.upgradeIncrement = upgradeIncrement;
            _newAtt.visible = visible;
            _newAtt.locked = locked;
            _newAtt.randomChange = randomChange;
            _newAtt.minValue = minValue;
            _newAtt.maxValue = maxValue;
            _newAtt.displayFormat = displayFormat;
            _newAtt.suffixes = suffixes;
            _newAtt.coreStats = coreStats;
            _newAtt.compareInfo = compareInfo;
            _newAtt.visibleInStatsPanel = visibleInStatsPanel;
            _newAtt.isFixed = isFixed;
            return _newAtt;
        }

        /// <summary>
        /// Whether this attribute numberical
        /// </summary>
        /// <returns></returns>
        public bool isNumber()
        {
            return !stringValue;
        }

        /// <summary>
        /// When creating an item, call this to decide wether this attribute should be locked.
        /// </summary>
        public void Init()
        {
            if (isFixed)
            {
                locked = false;
            }
            else
            {
                locked = (Random.Range(0, 100) > randomChange);
                if (!stringValue) value = Random.Range(minValue, maxValue).ToString("0.0");
            }
        }

        /// <summary>
        /// Retrieve the float value of this attribute.
        /// </summary>
        /// <param name="_upgradeLevel"></param>
        /// <returns></returns>
        public float GetFloat(int _upgradeLevel = 0)
        {
            if (stringValue || (locked && !isFixed)) return 0F;
            float _result = 0F;
            float.TryParse(value, out _result);
            _result += upgradeIncrement * _upgradeLevel;
            return _result;
        }

        /// <summary>
        /// Retrieve the int value of this attribute.
        /// </summary>
        /// <param name="_upgradeLevel"></param>
        /// <returns></returns>
        public int GetInt(int _upgradeLevel = 0)
        {
            if (stringValue || (locked && !isFixed)) return 0;
            int _result = 0;
            int.TryParse(value, out _result);
            _result += Mathf.FloorToInt(upgradeIncrement * _upgradeLevel);
            return _result;
        }

        /// <summary>
        /// Retrieve the string value of this attribute.
        /// </summary>
        /// <returns></returns>
        public string GetString()
        {
            if (locked && !isFixed) return "None";
            return value;
        }

        /// <summary>
        /// Change the value of this attribute.
        /// </summary>
        /// <param name="_value"></param>
        public void UpdateValue(float _value)
        {
            value = _value.ToString();
        }

        /// <summary>
        /// Change the value of this attribute.
        /// </summary>
        /// <param name="_value"></param>
        public void UpdateValue(int _value)
        {
            value = _value.ToString();
        }

        /// <summary>
        /// Change the value of this attribute.
        /// </summary>
        /// <param name="_value"></param>
        public void UpdateValue(string _value)
        {
            value = _value;
        }


    }

}
