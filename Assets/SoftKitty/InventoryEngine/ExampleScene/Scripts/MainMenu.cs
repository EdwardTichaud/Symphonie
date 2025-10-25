using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.InventoryEngine
{
    /// <summary>
    /// This script is an example to show how to use InventoryEngine.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {

        #region References
        public GameObject LootPackPrefab;
        public InventoryHolder MerchantNpc;
        public InventoryHolder StorageContainer;
        public InventoryHolder NpcEquipment;
        public InventoryHolder NpcWorker;
        public ListItem DebugItem;
        public AdvAnimation EnemyAnimation;
        public Image EnemyHpBar;
        public Text EnemyHpBarText;
        public Text EnemyNameText;
        public GameObject EnemyDynamicText;
        public AdvAnimation EnemyAttackAnimation;
        public AdvAnimation EnemyStunAnimation;
        public AdvAnimation PlayerAnimation;
        public Image PlayerHpBar;
        public Image PlayerSpBar;
        public Text PlayerHpText;
        public Text PlayerSpText;
        public GameObject PlayerDynamicText;
        private float SkyBoxRot = 21F;
        private int EnemyLevel = 1;
        private float EnemyHp = 500F;
        private bool EnemyAttacking = false;
        #endregion

        #region Visual Effects
        private void Update()
        {
            //Make the background looks more alive.
            RenderSettings.skybox.SetFloat("_Exposure", 1.15F + Mathf.Sin(Time.time * 0.7F) * 0.15F);
            RenderSettings.skybox.SetFloat("_Rotation", SkyBoxRot);
            SkyBoxRot = Mathf.Lerp(SkyBoxRot, 21F + (Screen.width * 0.5F - InputProxy.mousePosition.x) * 0.003F, Time.unscaledDeltaTime);
            PlayerStateUpdate();
            EnemyStateUpdate();
        }
        #endregion

        /// Callback examples ======================================================================
        IEnumerator Start()
        {
            ItemManager.PlayerInventoryHolder.RegisterItemUseCallback(OnItemUse);//Register callback to trigger when player's item being use.
            ItemManager.PlayerEquipmentHolder.RegisterItemChangeCallback(OnEuqipmentItemChange);//Register callback for player's equipment
            ItemManager.PlayerInventoryHolder.RegisterItemDropCallback(OnItemDrop);//Register callback for player drop item from inventory
            ItemManager.PlayerEquipmentHolder.RegisterItemDropCallback(OnItemDrop);//Register callback for player drop item from equipment
            yield return 1;
            float _chp = ItemManager.PlayerEquipmentHolder.GetBaseStatsValue("chp"); //Get the current health value of player.
            float _mhp = ItemManager.PlayerEquipmentHolder.GetAttributeValue("hp",true);//Get the maximum health value of player.
            PlayerHpBar.rectTransform.sizeDelta = new Vector2(390F * _chp / _mhp, 40F);
            PlayerHpText.text = Mathf.RoundToInt(_chp).ToString() + "/" + Mathf.RoundToInt(_mhp).ToString();
            SetMonsterLevel(1);
        }

        private void OnDestroy() // If you ever called RegisterXXXXCallback(), don't forget to call UnRegisterXXXXCallback() when the gameobject going to be destroyed.
        {
            ItemManager.PlayerInventoryHolder.UnRegisterItemUseCallback(OnItemUse);
            ItemManager.PlayerEquipmentHolder.UnRegisterItemChangeCallback(OnEuqipmentItemChange);
            ItemManager.PlayerInventoryHolder.UnRegisterItemDropCallback(OnItemDrop);
            ItemManager.PlayerEquipmentHolder.UnRegisterItemDropCallback(OnItemDrop);
        }

        public void OnItemDrop(Item _droppedItem, int _number)
        {
            //Add your code here to Instantiate the item model on the ground.
            DynamicMsg.PopMsg("Player dropped" + " [" + _droppedItem.nameWithAffixing + "] x "+_number.ToString());
        }
       

#if MASTER_CHARACTER_CREATOR
        public MasterCharacterCreator.CharacterEntity Player; //If you have Master Character Creator installed, drag the player entity to this slot.
#endif
        public void OnEuqipmentItemChange(Dictionary<Item, int> _changedItems)
        {
            foreach (var _item in _changedItems.Keys) {
#if MASTER_CHARACTER_CREATOR
                if (Player != null)
                {
                    if (_changedItems[_item] > 0)
                    {
                        Player.Equip(_item.equipAppearance); // Master Character Creator Equip
                    }
                    else
                    {
                        Player.Unequip(_item.equipAppearance.Type); // Master Character Creator Unequip
                    }
                }
#endif
                DynamicMsg.PopMsg("Player "+ (_changedItems[_item]>0?"equipped":"unequipped")+" ["+ _item.nameWithAffixing + "]");
            }
        }

        public void OnItemUse(string _action, int _id, int _index) //When player using an item, this callback will be called.
        {
            if (_action == "equip" && WindowsManager.getOpenedWindow(ItemManager.PlayerEquipmentHolder)==null)
            {
                InventoryHolder.EquipItem(ItemManager.PlayerInventoryHolder, ItemManager.PlayerEquipmentHolder, _id, _index);//Equip item when click though action bar.
                return;
            }

            bool _displayIcon = false;
            if (_action.Contains("AddHp"))//change hp
            {
                PlayerHealthChange(ItemManager.TryGetItem(_id).GetAttributeFloat("chp"));
                SoundManager.Play2D("UseItem");
                _displayIcon = true;
            }
            else if (_action.Contains("AddSp"))//change sp
            {
                ItemManager.PlayerEquipmentHolder.AddBaseStatsValue("sp",ItemManager.TryGetItem(_id).GetAttributeFloat("sp"));
                SoundManager.Play2D("UseItem");
                _displayIcon = true;
            }
            else if (_action.Contains("ChangeBaseStats"))//change other stats
            {
                foreach (var obj in ItemManager.TryGetItem(_id).attributes)
                {
                    float _final= ItemManager.PlayerEquipmentHolder.AddBaseStatsValue(obj.key, obj.GetFloat());
                    Debug.Log("Added "+ obj.key+": "+ obj.GetFloat()+" > "+ _final);
                }
                SoundManager.Play2D("UseItem");
                _displayIcon = true;
            }
            else if (_action.Contains("Skill"))//Use Skill
            {
                ItemManager.PlayerEquipmentHolder.AddBaseStatsValue("sp", -ItemManager.TryGetItem(_id).GetAttributeFloat("spcost"));//Reduce sp with "Sp Cost" arrtibute value of the skill item.
                float _damage = ItemManager.TryGetItem(_id).GetAttributeFloat("dmg");//Get the "Damage" attribute value from the skill item
                float _attack = ItemManager.PlayerEquipmentHolder.GetAttributeValue("atk", true); //Get player's "Attack" attribute value.
                bool _critical = (Random.Range(0, 100) < ItemManager.PlayerEquipmentHolder.GetAttributeValue("crit", true));//Use player's "Critical Chance" attribute value to check if this should be a critical strike.
                bool _stun = (Random.Range(0, 100) < ItemManager.PlayerEquipmentHolder.GetAttributeValue("stun", true));//Use player's "Stun Chance" attribute value to check if this should stun the enemy.
                DamageEnemy(_attack + _damage, _critical,_stun);
                SoundManager.Play2D("Attack");
                _displayIcon = true;
            } 
           


            //Create item icon in the center of the screen to show how the callback works.
            if (_displayIcon)
            {
                DebugItem.transform.parent.SetAsLastSibling();
                GameObject _newItem = Instantiate(DebugItem.gameObject, DebugItem.transform.parent);
                _newItem.GetComponent<ListItem>().mImages[0].color = ItemManager.TryGetItem(_id).GetTypeColor();
                _newItem.GetComponent<ListItem>().mRawimages[0].texture = ItemManager.TryGetItem(_id).GetIcon();
                _newItem.GetComponent<ListItem>().mTexts[0].text = _action;
                _newItem.gameObject.SetActive(true);
            }
        }
        /// End of the Callback examples ======================================================================


        ///Game Play examples =======================================================================

        private void PlayerStateUpdate()//Called by Update()
        {
            float _playerSp = ItemManager.PlayerEquipmentHolder.AddBaseStatsValue("sp", Time.unscaledDeltaTime * 2F,false);//Recorver 2 sp per second; We do not want to save this value, so we mark '_save' as false.
            _playerSp = Mathf.Clamp(_playerSp, 0F, 100F);
            ItemManager.PlayerEquipmentHolder.SetBaseStatsValue("sp", _playerSp,false);//Override the sp value after clamp.We do not want to save this value, so we mark '_save' as false.
            PlayerSpBar.rectTransform.sizeDelta = new Vector2(390F * _playerSp / 100F, 25F);
            PlayerSpText.text = Mathf.RoundToInt(_playerSp).ToString();
            float _chp = ItemManager.PlayerEquipmentHolder.GetBaseStatsValue("chp");//Get the value of player's "Current HP" attribute.
            float _mhp = ItemManager.PlayerEquipmentHolder.GetAttributeValue("hp", true);//Get the value of player's "Health" attribute, this will be the maximum health value of the player.
            _chp = Mathf.Clamp(_chp, 0F, _mhp);
            PlayerHpBar.rectTransform.sizeDelta = new Vector2(390F * _chp / _mhp, 40F);
            PlayerHpText.text = Mathf.RoundToInt(_chp).ToString() + "/" + Mathf.RoundToInt(_mhp).ToString();
        }

        private void EnemyStateUpdate()//Called by Update()
        {
            EnemyHpBar.rectTransform.sizeDelta = new Vector2(300F * EnemyHp / (300 + 200 * EnemyLevel), 25F);
            EnemyHpBarText.text = Mathf.RoundToInt(EnemyHp).ToString() + "/" + (300 + 200 * EnemyLevel).ToString();
        }

        public void AttackEnemy()//When click on the enemy
        {
            float _damage = ItemManager.PlayerEquipmentHolder.GetAttributeValue("atk", true); //Use player's "Attack" attribute value as the damage value.
            bool _critical = (Random.Range(0, 100) < ItemManager.PlayerEquipmentHolder.GetAttributeValue("crit", true));//Use player's "Critical Chance" attribute value to check if this should be a critical strike.
            bool _stun= (Random.Range(0, 100) < ItemManager.PlayerEquipmentHolder.GetAttributeValue("stun", true));//Use player's "Stun Chance" attribute value to check if this should stun the enemy.
            DamageEnemy(_damage, _critical, _stun);
        }

        private void DamageEnemy(float _damage, bool _critical, bool _stun)
        {
            if (_critical) _damage *= 2;
            EnemyHp = Mathf.Clamp(EnemyHp - Mathf.CeilToInt(_damage), 0, 300 + 200 * EnemyLevel);

            //Visual Effects
            SoundManager.Play2D("Kill");
            EnemyAnimation.Stop();
            EnemyAnimation.Play("DamageEffect");
            if (_stun)
            {
                EnemyStunAnimation.Stop();
                EnemyStunAnimation.Play();
                if (EnemyAttacking)
                {
                    StopAllCoroutines();
                    EnemyAttacking = false;
                }
            }
            GameObject _newDamagePop = Instantiate(EnemyDynamicText, EnemyDynamicText.transform.parent);//Damage text pop
            _newDamagePop.GetComponent<Text>().text = Mathf.CeilToInt(_damage).ToString();
            if (_critical)//Critical stike visual effect
            {
                _newDamagePop.GetComponent<Text>().fontSize = 80;
                _newDamagePop.GetComponent<Text>().fontStyle = FontStyle.Bold;
                _newDamagePop.GetComponent<Text>().color = new Color(1F, 0.7F, 0F, 1F);
                _newDamagePop.GetComponent<Text>().text += " !";
            }
            _newDamagePop.gameObject.SetActive(true);
            //End of Visual Effects

            if (EnemyHp <= 0) //If enemy gets killed
            {
                if (EnemyAttacking)
                {
                    StopAllCoroutines();
                    EnemyAttacking = false;
                }
                Instantiate(LootPackPrefab).GetComponent<LootPack>().OpenPack(); //Create a loot pack and open it, don't forget to Instantiate the prefab, never directly manipulate the prefab.
                EnemyAnimation.Play("EnemySpawn");//Respawn the enemy.
                SetMonsterLevel(EnemyLevel+1);//Level up the enemy
                int xp = ItemManager.PlayerEquipmentHolder.AddBaseStatsValue("xp", 20+10*EnemyLevel);//Add xp to the "XP" attrubite of player.
                if (xp >= ItemManager.PlayerEquipmentHolder.GetBaseStatsValue("mxp"))//If the "XP" attribute value is larger than "Max XP", then add 1 to the "Level" attribute value.
                { //Level up!
                    float _level = ItemManager.PlayerEquipmentHolder.GetBaseStatsValue("lvl");
                    SoundManager.Play2D("CraftSuccess");
                    SetPlayerLevel(Mathf.FloorToInt(_level)+1);//Add 1 to the "Level"
                }
            }
            else //Enemy fight back
            {
                if (!EnemyAttacking)
                {
                    StartCoroutine(EnemyAttackCo(Random.Range(0.2F,1F)+(_stun?3F:0F)));
                }
            }
        }

        IEnumerator EnemyAttackCo(float _wait)
        {
            EnemyAttacking = true;
            yield return new WaitForSecondsRealtime(_wait);
            EnemyAttackAnimation.Stop();
            EnemyAttackAnimation.Play();
            yield return new WaitForSecondsRealtime(0.5F);
            PlayerAnimation.Play();
            SoundManager.Play2D("Attack");
            float _damage = Random.Range(5, 10) * EnemyLevel + 10;
            float _def = ItemManager.PlayerEquipmentHolder.GetAttributeValue("def", true);//Player's Defence attribute value
            float _agi= ItemManager.PlayerEquipmentHolder.GetAttributeValue("agi", true);//Player's Agility attribute value
            float _finalDamage= _damage * (1F-_def/(200+_def))*(Random.Range(0,100)<_agi?0F:1F);//Final damage formular
            if (_finalDamage > 0F)
            {
                PlayerHealthChange(-_finalDamage);//Change player's health value
            }
            else //parry
            {
                GameObject _newDamagePop = Instantiate(PlayerDynamicText, PlayerDynamicText.transform.parent);//Damage text pop
                _newDamagePop.GetComponent<Text>().text = "Parry!";
                _newDamagePop.gameObject.SetActive(true);
            }
            yield return new WaitForSecondsRealtime(0.5F);
            EnemyAttacking = false;
        }


        private void PlayerHealthChange(float _add)
        {
            float _chp= ItemManager.PlayerEquipmentHolder.AddBaseStatsValue("chp", _add);//Add  value to player's "Current HP" attribute. Negative value will be damage, postive value will be heal.
            float _mhp = ItemManager.PlayerEquipmentHolder.GetAttributeValue("hp",true);//Get the value of player's "Health" attribute, this will be the maximum health value of the player.
            _chp = Mathf.Clamp(_chp, 0F, _mhp);
            ItemManager.PlayerEquipmentHolder.SetBaseStatsValue("chp", _chp);//Override the hp value after clamp.


            GameObject _newDamagePop = Instantiate(PlayerDynamicText, PlayerDynamicText.transform.parent);//Damage text pop
            if (_add < 0F)//Damage
            {
                _newDamagePop.GetComponent<Text>().text = Mathf.CeilToInt(-_add).ToString();
            }
            else//Heal
            {
                _newDamagePop.GetComponent<Text>().text ="+"+ Mathf.CeilToInt(_add).ToString();
                _newDamagePop.GetComponent<Text>().color = new Color(0.1F,1F,0.4F,1F);
            }
            _newDamagePop.gameObject.SetActive(true);

            if (_chp <= 0F)//Player dead
            {
                SoundManager.Play2D("CraftFail");
                PlayerAnimation.Stop();
                PlayerAnimation.Play("PlayerDead");//Respawn the player.
                EnemyAnimation.Stop();
                EnemyAnimation.Play("EnemySpawn");//Respawn the enemy.
                //Reset player and monster's level to 1
                SetPlayerLevel(1);
                SetMonsterLevel(1);
            }
        }

        private void SetPlayerLevel(int _level)
        {
            ItemManager.PlayerEquipmentHolder.SetBaseStatsValue("xp", 0); //Reset XP to 0;
            ItemManager.PlayerEquipmentHolder.SetBaseStatsValue("mxp", 100 + 30 * _level); //Calculate new max xp required to level up.
            ItemManager.PlayerEquipmentHolder.SetBaseStatsValue("lvl", _level);//Set the "Level" attribute.
            ItemManager.PlayerEquipmentHolder.SetBaseStatsValue("hp", 80+20*_level);//Set the maximum health value based on level;
            float _mhp = ItemManager.PlayerEquipmentHolder.GetAttributeValue("hp", true);
            ItemManager.PlayerEquipmentHolder.SetBaseStatsValue("chp", _mhp);//refill the current health value.
        }

        private void SetMonsterLevel(int _level)
        {
            EnemyLevel= _level;
            EnemyHp = 300 + 200 * EnemyLevel;
            EnemyNameText.text = "Monster Lvl." + EnemyLevel.ToString();
        }


        ///End of Game Play examples =======================================================================

        /// Open window examples ===================================================================
        public void OpenPlayerInventory()
        {
            if (ItemManager.instance != null && ItemManager.PlayerInventoryHolder != null) ItemManager.PlayerInventoryHolder.OpenWindow();//Open Inventory window for player.
        }

        public void OpenNpcWorker()
        {
            NpcWorker.OpenForgeWindow(true,false,false,false,2F); //Open the crafting window to assign jobs to NPC or Crafting Table.
        }

        public void OpenPlayerEquipment()
        {
            ItemManager.PlayerEquipmentHolder.OpenWindow(); //Open Equipment window for player.
        }

        public void OpenNpcTeamEquipment()
        {
            NpcEquipment.OpenWindow();//Open the equipment window of your ally NPC.
        }

        public void OpenCraft()
        {
            ItemManager.PlayerInventoryHolder.OpenForgeWindow(true, true, true, true, 1F,"Forge"); //Open crafting window
        }

        public void KillMonster()
        {
            SoundManager.Play2D("Kill");
            Instantiate(LootPackPrefab).GetComponent<LootPack>().OpenPack(); //Create a loot pack and open it, don't forget to Instantiate the prefab, never directly manipulate the prefab.
        }

        public void OpenSkills()
        {
            ItemManager.PlayerInventoryHolder.OpenWindowByName("Skills","Skills"); //An example to use "Hidden Items" to achive "Skills" management.
        }
        public void TalkToMerchant()
        {
            MerchantNpc.OpenWindow();//Open the merchant trade window.
            SoundManager.Play2D("Merchant");
        }

        public void OpenStorage()
        {
            StorageContainer.OpenWindow();//Open the crate window.
        }
        /// End of the Open window examples ===================================================================
    }
}
