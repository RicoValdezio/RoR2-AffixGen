﻿using RoR2;

namespace AffixGen
{
    internal class HookMaster
    {
        internal static void Init()
        {
            On.RoR2.Run.BeginStage += Run_BeginStage;

            On.RoR2.CharacterBody.OnEquipmentGained += CharacterBody_OnEquipmentGained;
            On.RoR2.CharacterBody.OnEquipmentLost += CharacterBody_OnEquipmentLost;
            On.RoR2.EquipmentSlot.PerformEquipmentAction += EquipmentSlot_PerformEquipmentAction;

            On.RoR2.HealthComponent.TakeDamage += HealthComponent_TakeDamage;

            On.RoR2.CharacterBody.AddTimedBuff += CharacterBody_AddTimedBuff;
        }

        private static void Run_BeginStage(On.RoR2.Run.orig_BeginStage orig, Run self)
        {
            foreach (AffixEquipBehaviour behaviour in AffixGenPlugin.activeBehaviours)
            {
                behaviour.ResetStage();
            }
            orig(self);
        }

        private static void CharacterBody_OnEquipmentGained(On.RoR2.CharacterBody.orig_OnEquipmentGained orig, CharacterBody self, EquipmentDef equipmentDef)
        {
            if (!self.masterObject.GetComponent<AffixEquipBehaviour>())
            {
                //If I don't have a tracker behaviour and I should, give me one
                if (equipmentDef.equipmentIndex == BaseAffixEquip.index || equipmentDef.equipmentIndex == LunarAffixEquip.index)
                {
                    self.masterObject.AddComponent<AffixEquipBehaviour>();
                }
            }
            else
            {
                //If I have one, run the modified pickup hook in the behaviour
                self.masterObject.GetComponent<AffixEquipBehaviour>().UpdateEquipment(equipmentDef.equipmentIndex, true);
            }
            orig(self, equipmentDef);
        }

        private static void CharacterBody_OnEquipmentLost(On.RoR2.CharacterBody.orig_OnEquipmentLost orig, CharacterBody self, EquipmentDef equipmentDef)
        {
            if (self.masterObject.GetComponent<AffixEquipBehaviour>())
            {
                //If I have one, run the modified pickup hook in the behaviour
                self.masterObject.GetComponent<AffixEquipBehaviour>().UpdateEquipment(equipmentDef.equipmentIndex, false);
            }
            orig(self, equipmentDef);
        }

        private static bool EquipmentSlot_PerformEquipmentAction(On.RoR2.EquipmentSlot.orig_PerformEquipmentAction orig, EquipmentSlot self, EquipmentIndex equipmentIndex)
        {
            if (self.characterBody.masterObject.GetComponent<AffixEquipBehaviour>())
            {
                if (equipmentIndex == BaseAffixEquip.index)
                {
                    return self.characterBody.masterObject.GetComponent<AffixEquipBehaviour>().PerformBaseAction();
                }
                else if (equipmentIndex == LunarAffixEquip.index)
                {
                    return self.characterBody.masterObject.GetComponent<AffixEquipBehaviour>().PerformLunarAction();
                }
            }
            return orig(self, equipmentIndex);
        }

        private static void HealthComponent_TakeDamage(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            if (self.body && self.body.masterObject && self.body.masterObject.GetComponent<AffixEquipBehaviour>())
            {
                damageInfo = self.body.masterObject.GetComponent<AffixEquipBehaviour>().CalculateNewDamage(damageInfo);
            }
            orig(self, damageInfo);
        }

        private static void CharacterBody_AddTimedBuff(On.RoR2.CharacterBody.orig_AddTimedBuff orig, CharacterBody self, BuffIndex buffType, float duration)
        {
            if (self.masterObject.GetComponent<AffixEquipBehaviour>())
            {
                self.masterObject.GetComponent<AffixEquipBehaviour>().UpdateVultures(buffType, duration);
            }
            orig(self, buffType, duration);
        }
    }
}