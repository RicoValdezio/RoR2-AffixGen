﻿using RoR2;
using System.Collections.Generic;
using UnityEngine;

namespace AffixGen
{
    class AffixEquipBehaviour : MonoBehaviour
    {
        private class AffixTracker
        {
            internal EliteIndex eliteIndex;
            internal BuffIndex buffIndex;
            internal EquipmentIndex equipmentIndex;
            internal bool isStageLock, isCurseLock, isHeld, isVultured;
            internal float vultureTimeLeft;
            internal int loopsRequired;
        }

        private List<AffixTracker> affixTrackers;
        internal int curseCount;
        internal static float curseMultiplier;
        private CharacterMaster trackerMaster;
        private CharacterBody trackerBody;
        private BuffIndex mostRecentAttackIndex;

        private void OnEnable()
        {
            affixTrackers = new List<AffixTracker>()
            {
                new AffixTracker //Fire
                {
                    eliteIndex = EliteIndex.Fire,
                    buffIndex = BuffIndex.AffixRed,
                    equipmentIndex = EquipmentIndex.AffixRed
                },
                new AffixTracker //Lightning
                {
                    eliteIndex = EliteIndex.Lightning,
                    buffIndex = BuffIndex.AffixBlue,
                    equipmentIndex = EquipmentIndex.AffixBlue
                },
                new AffixTracker //Ice
                {
                    eliteIndex = EliteIndex.Ice,
                    buffIndex = BuffIndex.AffixWhite,
                    equipmentIndex = EquipmentIndex.AffixWhite
                },
                new AffixTracker //Poison
                {
                    eliteIndex = EliteIndex.Poison,
                    buffIndex = BuffIndex.AffixPoison,
                    equipmentIndex = EquipmentIndex.AffixPoison,
                    loopsRequired = 1
                },
                new AffixTracker //Ghost
                {
                    eliteIndex = EliteIndex.Haunted,
                    buffIndex = BuffIndex.AffixHaunted,
                    equipmentIndex = EquipmentIndex.AffixHaunted,
                    loopsRequired = 1
                }
            };
            ShuffleTrackers();
            trackerMaster = gameObject.GetComponent<CharacterMaster>();
            curseCount = 0;

            On.RoR2.HealthComponent.TakeDamage += HealthComponent_TakeDamage;
            On.RoR2.EquipmentSlot.PerformEquipmentAction += EquipmentSlot_PerformEquipmentAction;
            On.RoR2.CharacterBody.AddTimedBuff += CharacterBody_AddTimedBuff;
            On.RoR2.Run.BeginStage += Run_BeginStage;
            On.RoR2.CharacterBody.OnEquipmentGained += CharacterBody_OnEquipmentGained;
            On.RoR2.CharacterBody.OnEquipmentLost += CharacterBody_OnEquipmentLost;
        }

        private void Update()
        {
            trackerMaster = gameObject.GetComponent<CharacterMaster>();
            trackerBody = trackerMaster.GetBody();
            int tempCurseCount = 0;

            foreach (AffixTracker tracker in affixTrackers)
            {
                //Check is the buff is currently from Wake of Vultures
                if (tracker.isVultured)
                {
                    //Reduce the amount of time and remove flag if no time left
                    tracker.vultureTimeLeft -= Time.deltaTime;
                    if (tracker.vultureTimeLeft <= 0)
                    {
                        tracker.isVultured = false;
                    }
                }

                //Calculate the current curse count
                if (tracker.isCurseLock)
                {
                    //If its cursed and neither staged nor held nor vultured, add a curse
                    if (!tracker.isHeld && !tracker.isStageLock && !tracker.isVultured)
                    {
                        tempCurseCount++;
                    }
                }
            }
            //Update curse count if its changed
            if (tempCurseCount != curseCount)
            {
                curseCount = tempCurseCount;
                //Post the curse level to chat (will be removed/replaced with a item/buff)
                Chat.AddMessage("Current Curse Level is: " + curseCount.ToString());
            }
        }

        private void ShuffleTrackers()
        {
            for (int i = 0; i < affixTrackers.Count; i++)
            {
                AffixTracker temp = affixTrackers[i];
                int randomIndex = Random.Range(i, affixTrackers.Count);
                affixTrackers[i] = affixTrackers[randomIndex];
                affixTrackers[randomIndex] = temp;
            }
        }

        private void HealthComponent_TakeDamage(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            //Inject the extra damage if there's curses
            if (self.body == trackerBody)
            {
                damageInfo.damage *= 1 + (curseMultiplier * curseCount);
            }

            //Capture the most recent affix if it was an elite (only the first, just in case)
            if (damageInfo.attacker && damageInfo.attacker.GetComponent<CharacterBody>())
            {
                CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                if (attackerBody.isElite)
                {
                    foreach (AffixTracker tracker in affixTrackers)
                    {
                        if (attackerBody.HasBuff(tracker.buffIndex))
                        {
                            mostRecentAttackIndex = tracker.buffIndex;
                            break;
                        }
                    }
                }
            }
            orig(self, damageInfo);
        }

        private bool EquipmentSlot_PerformEquipmentAction(On.RoR2.EquipmentSlot.orig_PerformEquipmentAction orig, EquipmentSlot self, EquipmentIndex equipmentIndex)
        {
            if (self.characterBody == trackerBody)
            {
                //BaseEquip
                if (equipmentIndex == BaseAffixEquip.index)
                {
                    //If there's an affix left to give this stage, give it and destroy the equipment
                    foreach (AffixTracker tracker in affixTrackers)
                    {
                        if (!tracker.isStageLock && tracker.loopsRequired <= Run.instance.loopClearCount)
                        {
                            tracker.isStageLock = true;
                            trackerBody.inventory.SetEquipmentIndex(EquipmentIndex.None);
                            trackerBody.AddBuff(tracker.buffIndex);
                            return false;
                        }
                    }
                    ShuffleTrackers();
                    //Else do nothing and keep the equipment
                    return true;
                }
                //LunarEquip
                if (equipmentIndex == LunarAffixEquip.index)
                {
                    //If the most recent affix was one you don't have yet, add it
                    foreach (AffixTracker tracker in affixTrackers)
                    {
                        if (tracker.buffIndex == mostRecentAttackIndex)
                        {
                            tracker.isCurseLock = true;
                            trackerBody.AddBuff(tracker.buffIndex);
                            return true;
                        }
                    }
                    //Else do nothing and keep the equipment
                    return true;
                }
            }
            return orig(self, equipmentIndex);
        }

        private void CharacterBody_AddTimedBuff(On.RoR2.CharacterBody.orig_AddTimedBuff orig, CharacterBody self, BuffIndex buffType, float duration)
        {
            if (self == trackerBody)
            {
                foreach (AffixTracker tracker in affixTrackers)
                {
                    //If the timed buff is an affix, add to the vultureTimeLeft
                    if (buffType == tracker.buffIndex)
                    {
                        tracker.vultureTimeLeft += duration;
                        tracker.isVultured = true;
                    }
                }
            }
            orig(self, buffType, duration);
        }

        private void Run_BeginStage(On.RoR2.Run.orig_BeginStage orig, Run self)
        {
            orig(self);
            //Clear the isStageLock and isVultured values
            trackerMaster = gameObject.GetComponent<CharacterMaster>();
            trackerBody = trackerMaster.GetBody();
            foreach (AffixTracker tracker in affixTrackers)
            {
                tracker.isStageLock = false;
                tracker.isVultured = false;
                tracker.vultureTimeLeft = 0f;
                if (tracker.isCurseLock)
                {
                    trackerBody.AddBuff(tracker.buffIndex);
                }
            }
        }

        private void CharacterBody_OnEquipmentGained(On.RoR2.CharacterBody.orig_OnEquipmentGained orig, CharacterBody self, EquipmentDef equipmentDef)
        {
            if (self == trackerBody)
            {
                foreach (AffixTracker tracker in affixTrackers)
                {
                    //If I have the elite equip, set isHeld to true
                    if (equipmentDef.equipmentIndex == tracker.equipmentIndex)
                    {
                        tracker.isHeld = true;
                    }
                }
            }
            orig(self, equipmentDef);
        }

        private void CharacterBody_OnEquipmentLost(On.RoR2.CharacterBody.orig_OnEquipmentLost orig, CharacterBody self, EquipmentDef equipmentDef)
        {
            if (self == trackerBody)
            {
                foreach (AffixTracker tracker in affixTrackers)
                {
                    //If I had the elite equip, set isHeld to false
                    if (equipmentDef.equipmentIndex == tracker.equipmentIndex)
                    {
                        tracker.isHeld = false;
                    }
                }
            }
            orig(self, equipmentDef);
        }
    }
}
