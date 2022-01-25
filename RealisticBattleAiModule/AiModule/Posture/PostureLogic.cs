﻿using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace RealisticBattleAiModule.AiModule.Posture
{
    class PostureLogic
    {
        [HarmonyPatch(typeof(Agent))]
        [HarmonyPatch("EquipItemsFromSpawnEquipment")]
        class EquipItemsFromSpawnEquipmentPatch
        {
            static void Prefix(ref Agent __instance)
            {
                AgentPostures.values[__instance] = new Posture();
            }
        }

        [HarmonyPatch(typeof(Agent))]
        [HarmonyPatch("OnWieldedItemIndexChange")]
        class OnWieldedItemIndexChangePatch
        {
            static void Postfix(ref Agent __instance, bool isOffHand, bool isWieldedInstantly, bool isWieldedOnSpawn)
            {
                if (XmlConfig.dict["Global.PostureEnabled"] == 1)
                {
                    //AgentPostures.values[__instance] = new Posture();
                    Posture posture = null;
                    AgentPostures.values.TryGetValue(__instance, out posture);
                    if (posture == null)
                    {
                        AgentPostures.values[__instance] = new Posture();
                    }
                    AgentPostures.values.TryGetValue(__instance, out posture);
                    if (posture != null)
                    {
                        float oldPosture = posture.posture;
                        float oldMaxPosture = posture.maxPosture;
                        float oldPosturePercentage = oldPosture / oldMaxPosture;

                        int usageIndex = 0;
                        EquipmentIndex slotIndex = __instance.GetWieldedItemIndex(0);
                        if (slotIndex != EquipmentIndex.None)
                        {
                            usageIndex = __instance.Equipment[slotIndex].CurrentUsageIndex;

                            WeaponComponentData wcd = __instance.Equipment[slotIndex].GetWeaponComponentDataForUsage(usageIndex);
                            SkillObject weaponSkill = WeaponComponentData.GetRelevantSkillFromWeaponClass(wcd.WeaponClass);
                            int effectiveWeaponSkill = 0;
                            if (weaponSkill != null)
                            {
                                effectiveWeaponSkill = MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(__instance.Character, __instance.Origin, __instance.Formation, weaponSkill);
                            }

                            float athleticBase = 20f;
                            float weaponSkillBase = 80f;
                            float strengthSkillModifier = 500f;
                            float weaponSkillModifier = 500f;
                            float athleticRegenBase = 0.008f;
                            float weaponSkillRegenBase = 0.032f;
                            float baseModifier = 1f;

                            if (__instance.HasMount)
                            {
                                int effectiveRidingSkill = MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(__instance.Character, __instance.Origin, __instance.Formation, DefaultSkills.Riding);
                                posture.maxPosture = (athleticBase * (baseModifier + (effectiveRidingSkill / strengthSkillModifier))) + (weaponSkillBase * (baseModifier + (effectiveWeaponSkill / weaponSkillModifier)));
                                posture.regenPerTick = (athleticRegenBase * (baseModifier + (effectiveRidingSkill / strengthSkillModifier))) + (weaponSkillRegenBase * (baseModifier + (effectiveWeaponSkill / weaponSkillModifier)));
                                //posture.maxPosture = 100f;
                                //posture.regenPerTick = 0.035f;
                            }
                            else
                            {
                                int effectiveAthleticSkill = MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(__instance.Character, __instance.Origin, __instance.Formation, DefaultSkills.Athletics);
                                posture.maxPosture = (athleticBase * (baseModifier + (effectiveAthleticSkill / strengthSkillModifier))) + (weaponSkillBase * (baseModifier + (effectiveWeaponSkill / weaponSkillModifier)));
                                posture.regenPerTick = (athleticRegenBase * (baseModifier + (effectiveAthleticSkill / strengthSkillModifier))) + (weaponSkillRegenBase * (baseModifier + (effectiveWeaponSkill / weaponSkillModifier)));
                                //posture.maxPosture = 100f;
                                //posture.regenPerTick = 0.035f;
                            }

                            posture.posture = posture.maxPosture * oldPosturePercentage;
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(MissionState))]
        [HarmonyPatch("FinishMissionLoading")]
        public class FinishMissionLoadingPatch
        {
            static void Postfix()
            {
                AgentPostures.values.Clear();
            }
        }

        [HarmonyPatch(typeof(Mission))]
        [HarmonyPatch("CreateMeleeBlow")]
        class CreateMeleeBlowPatch
        {
            static void Postfix(ref Mission __instance, ref Blow __result, Agent attackerAgent, Agent victimAgent, ref AttackCollisionData collisionData, in MissionWeapon attackerWeapon, CrushThroughState crushThroughState, Vec3 blowDirection, Vec3 swingDirection, bool cancelDamage)
            {
                if(XmlConfig.dict["Global.PostureEnabled"] == 1 && attackerAgent != null && victimAgent != null && attackerWeapon.CurrentUsageItem != null &&
                    attackerWeapon.CurrentUsageItem != null) { 
                    Posture defenderPosture = null;
                    Posture attackerPosture = null;
                    AgentPostures.values.TryGetValue(victimAgent, out defenderPosture);
                    AgentPostures.values.TryGetValue(attackerAgent, out attackerPosture);

                    float postureResetModifier = 0.5f;

                    float absoluteDamageModifier = 3f;
                    float absoluteShieldDamageModifier = 1.2f;

                    if (!collisionData.AttackBlockedWithShield) { 
                        if (collisionData.CollisionResult == CombatCollisionResult.Blocked)
                        {
                            if(defenderPosture != null)
                            {
                                defenderPosture.posture = defenderPosture.posture - calculateDefenderPostureDamage(victimAgent, attackerAgent, absoluteDamageModifier, 0.85f, ref collisionData, attackerWeapon);
                                addPosturedamageVisual(attackerAgent, victimAgent);
                                if (defenderPosture.posture <= 0f)
                                {
                                    EquipmentIndex wieldedItemIndex = victimAgent.GetWieldedItemIndex(0);
                                    if (wieldedItemIndex != EquipmentIndex.None)
                                    {
                                        if (victimAgent == Agent.Main)
                                        {
                                            InformationManager.DisplayMessage(new InformationMessage("Posture break: Posture depleted, " + collisionData.InflictedDamage + " damage crushed through", Color.FromUint(4282569842u)));
                                        }

                                        makePostureCrashThroughBlow(ref __instance, ref __result, attackerAgent, victimAgent, ref collisionData, attackerWeapon, crushThroughState, blowDirection, swingDirection, cancelDamage, BlowFlags.KnockDown);

                                    }
                                    defenderPosture.posture = defenderPosture.maxPosture * postureResetModifier;
                                    addPosturedamageVisual(attackerAgent, victimAgent);
                                }
                            }
                            if (attackerPosture != null)
                            {
                                attackerPosture.posture = attackerPosture.posture - calculateAttackerPostureDamage(victimAgent, attackerAgent, absoluteDamageModifier, 0.25f, ref collisionData, attackerWeapon);
                                addPosturedamageVisual(attackerAgent, victimAgent);
                                if (attackerPosture.posture <= 0f)
                                {
                                }
                            }
                        }

                        if (collisionData.CollisionResult == CombatCollisionResult.Parried)
                        {
                            if (defenderPosture != null)
                            {
                                defenderPosture.posture = defenderPosture.posture - calculateDefenderPostureDamage(victimAgent, attackerAgent, absoluteDamageModifier, 0.5f, ref collisionData, attackerWeapon);
                                addPosturedamageVisual(attackerAgent, victimAgent);
                                if (defenderPosture.posture <= 0f)
                                {
                                    EquipmentIndex wieldedItemIndex = victimAgent.GetWieldedItemIndex(0);
                                    if (wieldedItemIndex != EquipmentIndex.None)
                                    {
                                        if (victimAgent == Agent.Main)
                                        {
                                            InformationManager.DisplayMessage(new InformationMessage("Posture break: Posture depleted, perfect parry, " + collisionData.InflictedDamage + " damage crushed through", Color.FromUint(4282569842u)));
                                        }
                                        makePostureCrashThroughBlow(ref __instance, ref __result, attackerAgent, victimAgent, ref collisionData, attackerWeapon, crushThroughState, blowDirection, swingDirection, cancelDamage, BlowFlags.KnockBack);
                                    }
                                    defenderPosture.posture = defenderPosture.maxPosture * postureResetModifier;
                                    addPosturedamageVisual(attackerAgent, victimAgent);
                                }
                            }
                            if (attackerPosture != null)
                            {
                                attackerPosture.posture = attackerPosture.posture - calculateAttackerPostureDamage(victimAgent, attackerAgent, absoluteDamageModifier, 0.75f, ref collisionData, attackerWeapon);
                                addPosturedamageVisual(attackerAgent, victimAgent);
                                if (attackerPosture.posture <= 0f)
                                {
                                    if (attackerAgent == Agent.Main)
                                    {
                                        InformationManager.DisplayMessage(new InformationMessage("Posture break: Posture depleted, perfect parry", Color.FromUint(4282569842u)));
                                    }
                                    makePostureRiposteBlow(ref __instance, ref __result, attackerAgent, victimAgent, ref collisionData, attackerWeapon, crushThroughState, blowDirection, swingDirection, cancelDamage, BlowFlags.KnockDown);
                                    attackerPosture.posture = attackerPosture.maxPosture * postureResetModifier;
                                    addPosturedamageVisual(attackerAgent, victimAgent);
                                }
                            }
                        }

                    }

                    if (collisionData.AttackBlockedWithShield)
                    {
                        if (collisionData.CollisionResult == CombatCollisionResult.Blocked && !collisionData.CorrectSideShieldBlock)
                        {
                            if (defenderPosture != null)
                            {
                                defenderPosture.posture = defenderPosture.posture - calculateDefenderPostureDamage(victimAgent, attackerAgent, absoluteShieldDamageModifier, 1f, ref collisionData, attackerWeapon);
                                addPosturedamageVisual(attackerAgent, victimAgent);
                                if (defenderPosture.posture <= 0f)
                                {
                                    EquipmentIndex wieldedItemIndex = victimAgent.GetWieldedItemIndex(0);
                                    if (wieldedItemIndex != EquipmentIndex.None)
                                    {
                                        if (victimAgent == Agent.Main)
                                        {
                                            InformationManager.DisplayMessage(new InformationMessage("Posture break: Posture depleted, incorrect side block", Color.FromUint(4282569842u)));
                                        }
                                        makePostureBlow(ref __instance, ref __result, attackerAgent, victimAgent, ref collisionData, attackerWeapon, crushThroughState, blowDirection, swingDirection, cancelDamage, BlowFlags.KnockDown);

                                    }
                                    defenderPosture.posture = defenderPosture.maxPosture * postureResetModifier;
                                    addPosturedamageVisual(attackerAgent, victimAgent);
                                }
                            }
                            if (attackerPosture != null)
                            {
                                attackerPosture.posture = attackerPosture.posture - calculateAttackerPostureDamage(victimAgent, attackerAgent, absoluteShieldDamageModifier, 0.2f, ref collisionData, attackerWeapon);
                                addPosturedamageVisual(attackerAgent, victimAgent);
                                if (attackerPosture.posture <= 0f)
                                {
                                }
                            }
                        }

                        if ((collisionData.CollisionResult == CombatCollisionResult.Blocked && collisionData.CorrectSideShieldBlock) ||
                        (collisionData.CollisionResult == CombatCollisionResult.Parried && !collisionData.CorrectSideShieldBlock))
                        {
                            if (defenderPosture != null)
                            {
                                defenderPosture.posture = defenderPosture.posture - calculateDefenderPostureDamage(victimAgent, attackerAgent, absoluteShieldDamageModifier, 1f, ref collisionData, attackerWeapon);
                                addPosturedamageVisual(attackerAgent, victimAgent);
                                if (defenderPosture.posture <= 0f)
                                {
                                    EquipmentIndex wieldedItemIndex = victimAgent.GetWieldedItemIndex(0);
                                    if (wieldedItemIndex != EquipmentIndex.None)
                                    {
                                        if (victimAgent == Agent.Main)
                                        {
                                            InformationManager.DisplayMessage(new InformationMessage("Posture break: Posture depleted, correct side block", Color.FromUint(4282569842u)));
                                        }

                                        makePostureBlow(ref __instance, ref __result, attackerAgent, victimAgent, ref collisionData, attackerWeapon, crushThroughState, blowDirection, swingDirection, cancelDamage, BlowFlags.KnockDown);

                                    }
                                    defenderPosture.posture = defenderPosture.maxPosture * postureResetModifier;
                                    addPosturedamageVisual(attackerAgent, victimAgent);
                                }
                            }
                            if (attackerPosture != null)
                            {
                                attackerPosture.posture = attackerPosture.posture - calculateAttackerPostureDamage(victimAgent, attackerAgent, absoluteShieldDamageModifier, 0.3f, ref collisionData, attackerWeapon);
                                addPosturedamageVisual(attackerAgent, victimAgent);
                                if (attackerPosture.posture <= 0f)
                                {
                                }
                            }
                        }

                        if (collisionData.CollisionResult == CombatCollisionResult.Parried && collisionData.CorrectSideShieldBlock)
                        {
                            if (defenderPosture != null)
                            {
                                defenderPosture.posture = defenderPosture.posture - calculateDefenderPostureDamage(victimAgent, attackerAgent, absoluteShieldDamageModifier, 0.8f, ref collisionData, attackerWeapon);
                                addPosturedamageVisual(attackerAgent, victimAgent);
                                if (defenderPosture.posture <= 0f)
                                {
                                    EquipmentIndex wieldedItemIndex = victimAgent.GetWieldedItemIndex(0);
                                    if (wieldedItemIndex != EquipmentIndex.None)
                                    {
                                        if (victimAgent == Agent.Main)
                                        {
                                            InformationManager.DisplayMessage(new InformationMessage("Posture break: Posture depleted, perfect parry, correct side block", Color.FromUint(4282569842u)));
                                        }
                                        makePostureBlow(ref __instance, ref __result, attackerAgent, victimAgent, ref collisionData, attackerWeapon, crushThroughState, blowDirection, swingDirection, cancelDamage, BlowFlags.KnockBack);
                                    }
                                    defenderPosture.posture = defenderPosture.maxPosture * postureResetModifier;
                                    addPosturedamageVisual(attackerAgent, victimAgent);
                                }
                            }
                            if (attackerPosture != null)
                            {
                                attackerPosture.posture = attackerPosture.posture - calculateAttackerPostureDamage(victimAgent, attackerAgent, absoluteShieldDamageModifier, 0.5f, ref collisionData, attackerWeapon);
                                addPosturedamageVisual(attackerAgent, victimAgent);
                                if (attackerPosture.posture <= 0f)
                                {
                                    if (attackerAgent == Agent.Main)
                                    {
                                        InformationManager.DisplayMessage(new InformationMessage("Posture break: Posture depleted, perfect parry, correct side block", Color.FromUint(4282569842u)));
                                    }
                                    makePostureRiposteBlow(ref __instance, ref __result, attackerAgent, victimAgent, ref collisionData, attackerWeapon, crushThroughState, blowDirection, swingDirection, cancelDamage, BlowFlags.KnockDown);
                                    attackerPosture.posture = attackerPosture.maxPosture * postureResetModifier;
                                    addPosturedamageVisual(attackerAgent, victimAgent);
                                }
                            }
                        }
                    }
                    
                    if (collisionData.CollisionResult == CombatCollisionResult.ChamberBlocked)
                    {
                        if (defenderPosture != null)
                        {
                            defenderPosture.posture = defenderPosture.posture - calculateDefenderPostureDamage(victimAgent, attackerAgent, absoluteDamageModifier, 0.25f, ref collisionData, attackerWeapon);
                            addPosturedamageVisual(attackerAgent, victimAgent);
                            if (defenderPosture.posture <= 0f)
                            {
                                EquipmentIndex wieldedItemIndex = victimAgent.GetWieldedItemIndex(0);
                                if (wieldedItemIndex != EquipmentIndex.None)
                                {
                                    if (victimAgent == Agent.Main)
                                    {
                                        InformationManager.DisplayMessage(new InformationMessage("Posture break: Posture depleted, chamber block " + collisionData.InflictedDamage + " damage crushed through", Color.FromUint(4282569842u)));
                                    }
                                    makePostureBlow(ref __instance, ref __result, attackerAgent, victimAgent, ref collisionData, attackerWeapon, crushThroughState, blowDirection, swingDirection, cancelDamage, BlowFlags.NonTipThrust);
                                }
                                defenderPosture.posture = defenderPosture.maxPosture * postureResetModifier;
                                addPosturedamageVisual(attackerAgent, victimAgent);
                            }
                        }
                        if (attackerPosture != null)
                        {
                            attackerPosture.posture = attackerPosture.posture - calculateAttackerPostureDamage(victimAgent, attackerAgent, absoluteDamageModifier, 1.25f, ref collisionData, attackerWeapon);
                            addPosturedamageVisual(attackerAgent, victimAgent);
                            if (attackerPosture.posture <= 0f)
                            {
                                if (attackerAgent == Agent.Main)
                                {
                                    InformationManager.DisplayMessage(new InformationMessage("Posture break: Posture depleted, chamber block", Color.FromUint(4282569842u)));
                                }
                                makePostureCrashThroughBlow(ref __instance, ref __result, attackerAgent, victimAgent, ref collisionData, attackerWeapon, crushThroughState, blowDirection, swingDirection, cancelDamage, BlowFlags.KnockBack | BlowFlags.CrushThrough);
                                attackerPosture.posture = attackerPosture.maxPosture * postureResetModifier;
                                addPosturedamageVisual(attackerAgent, victimAgent);
                            }
                        }
                    }
                }
            }

            static void addPosturedamageVisual(Agent attackerAgent, Agent victimAgent)
            {
                if(XmlConfig.dict["Global.PostureGUIEnabled"] == 1) 
                {
                    if (victimAgent == Agent.Main || attackerAgent == Agent.Main)
                    {
                        Agent enemyAgent = null;
                        if (victimAgent == Agent.Main)
                        {
                            enemyAgent = attackerAgent;
                            Posture posture = null;
                            if (AgentPostures.values.TryGetValue(victimAgent, out posture))
                            {
                                if (AgentPostures.postureVisual != null && AgentPostures.postureVisual._dataSource.ShowPlayerPostureStatus)
                                {
                                    AgentPostures.postureVisual._dataSource.PlayerPosture = (int)posture.posture;
                                    AgentPostures.postureVisual._dataSource.PlayerPostureMax = (int)posture.maxPosture;
                                }
                            }
                        }
                        else
                        {
                            enemyAgent = victimAgent;
                            Posture posture = null;
                            if (AgentPostures.values.TryGetValue(attackerAgent, out posture))
                            {
                                if (AgentPostures.postureVisual != null && AgentPostures.postureVisual._dataSource.ShowPlayerPostureStatus)
                                {
                                    AgentPostures.postureVisual._dataSource.PlayerPosture = (int)posture.posture;
                                    AgentPostures.postureVisual._dataSource.PlayerPostureMax = (int)posture.maxPosture;
                                }
                            }
                        }
                        if (AgentPostures.postureVisual != null)
                        {
                            Posture posture = null;
                            if (AgentPostures.values.TryGetValue(enemyAgent, out posture))
                            {
                                AgentPostures.postureVisual._dataSource.ShowEnemyStatus = true;
                                AgentPostures.postureVisual.affectedAgent = enemyAgent;
                                if (AgentPostures.postureVisual._dataSource.ShowEnemyStatus && AgentPostures.postureVisual.affectedAgent == enemyAgent)
                                {
                                    AgentPostures.postureVisual.timer = AgentPostures.postureVisual.DisplayTime;
                                    AgentPostures.postureVisual._dataSource.EnemyPosture = (int)posture.posture;
                                    AgentPostures.postureVisual._dataSource.EnemyPostureMax = (int)posture.maxPosture;
                                    AgentPostures.postureVisual._dataSource.EnemyHealth = (int)enemyAgent.Health;
                                    AgentPostures.postureVisual._dataSource.EnemyHealthMax = (int)enemyAgent.HealthLimit;
                                    if (enemyAgent.IsMount)
                                    {
                                        AgentPostures.postureVisual._dataSource.EnemyName = enemyAgent.RiderAgent?.Name + " (Mount)";
                                    }
                                    else
                                    {
                                        AgentPostures.postureVisual._dataSource.EnemyName = enemyAgent.Name;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            static float calculateDefenderPostureDamage(Agent defenderAgent, Agent attackerAgent, float absoluteDamageModifier, float actionTypeDamageModifier, ref AttackCollisionData collisionData, MissionWeapon weapon)
            {
                float result = 0f;
                float defenderPostureDamageModifier = 1f; // terms and conditions may apply

                float strengthSkillModifier = 500f;
                float weaponSkillModifier = 500f;
                float basePostureDamage = 20f;

                SkillObject attackerWeaponSkill = WeaponComponentData.GetRelevantSkillFromWeaponClass(weapon.CurrentUsageItem.WeaponClass);
                float attackerEffectiveWeaponSkill = 0;
                float attackerEffectiveStrengthSkill = 0;
                if (attackerWeaponSkill != null)
                {
                    attackerEffectiveWeaponSkill = MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(attackerAgent.Character, attackerAgent.Origin, attackerAgent.Formation, attackerWeaponSkill);
                }
                if (attackerAgent.HasMount)
                {
                    attackerEffectiveStrengthSkill = MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(attackerAgent.Character, attackerAgent.Origin, attackerAgent.Formation, DefaultSkills.Riding);
                }
                else
                {
                    attackerEffectiveStrengthSkill = MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(attackerAgent.Character, attackerAgent.Origin, attackerAgent.Formation, DefaultSkills.Athletics);
                }
                
                float defenderEffectiveWeaponSkill = 0;
                float defenderEffectiveStrengthSkill = 0;

                if (defenderAgent.GetWieldedItemIndex(Agent.HandIndex.MainHand) != EquipmentIndex.None)
                {
                    MissionWeapon defenderWeapon = defenderAgent.Equipment[defenderAgent.GetWieldedItemIndex(Agent.HandIndex.MainHand)];
                    SkillObject defenderWeaponSkill = WeaponComponentData.GetRelevantSkillFromWeaponClass(defenderWeapon.CurrentUsageItem.WeaponClass);
                    if (defenderWeaponSkill != null)
                    {
                        defenderEffectiveWeaponSkill = MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(defenderAgent.Character, defenderAgent.Origin, defenderAgent.Formation, defenderWeaponSkill);
                    }
                    if (defenderAgent.GetWieldedItemIndex(Agent.HandIndex.OffHand) != EquipmentIndex.None)
                    {
                        if (defenderAgent.Equipment[defenderAgent.GetWieldedItemIndex(Agent.HandIndex.OffHand)].IsShield())
                        {
                            defenderEffectiveWeaponSkill += 20f;
                        }
                    }
                }
                if (defenderAgent.HasMount)
                {
                    defenderEffectiveStrengthSkill = MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(defenderAgent.Character, defenderAgent.Origin, defenderAgent.Formation, DefaultSkills.Riding);
                }
                else
                {
                    defenderEffectiveStrengthSkill = MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(defenderAgent.Character, defenderAgent.Origin, defenderAgent.Formation, DefaultSkills.Athletics);
                }

                defenderEffectiveWeaponSkill = defenderEffectiveWeaponSkill / weaponSkillModifier;
                defenderEffectiveStrengthSkill = defenderEffectiveStrengthSkill / strengthSkillModifier;

                attackerEffectiveWeaponSkill = attackerEffectiveWeaponSkill / weaponSkillModifier;
                attackerEffectiveStrengthSkill = attackerEffectiveStrengthSkill / strengthSkillModifier;

                basePostureDamage = basePostureDamage *((1f + attackerEffectiveStrengthSkill + attackerEffectiveWeaponSkill) / (1f + defenderEffectiveStrengthSkill + defenderEffectiveWeaponSkill));

                float attackerPostureModifier = 1f;
                //WeaponClass attackerWeaponClass = WeaponClass.Undefined;
                //if (weapon.CurrentUsageItem != null)
                //{
                //    attackerWeaponClass = weapon.CurrentUsageItem.WeaponClass;
                //}
                //switch (attackerWeaponClass)
                //{
                //    case WeaponClass.Dagger:
                //    case WeaponClass.OneHandedSword:
                //        {
                //            attackerPostureModifier = 0.85f;
                //            break;
                //        }
                //    case WeaponClass.TwoHandedSword:
                //        {
                //            attackerPostureModifier = 0.75f;
                //            break;
                //        }
                //    case WeaponClass.TwoHandedAxe:
                //    case WeaponClass.TwoHandedMace:
                //    case WeaponClass.TwoHandedPolearm:
                //        {
                //            attackerPostureModifier = 1f;
                //            break;
                //        }
                //    case WeaponClass.Mace:
                //    case WeaponClass.Pick:
                //        {
                //            attackerPostureModifier = 1.15f;
                //            break;
                //        }
                //    default:
                //        {
                //            attackerPostureModifier = 1f;
                //            break;
                //        }
                //}

                WeaponClass defenderWeaponClass = WeaponClass.Undefined;
                if (defenderAgent.GetWieldedItemIndex(Agent.HandIndex.OffHand) != EquipmentIndex.None)
                {
                    if (defenderAgent.Equipment[defenderAgent.GetWieldedItemIndex(Agent.HandIndex.OffHand)].IsShield())
                    {
                        defenderWeaponClass = defenderAgent.Equipment[defenderAgent.GetWieldedItemIndex(Agent.HandIndex.OffHand)].CurrentUsageItem.WeaponClass;
                    }
                }
                else
                {
                    if (defenderAgent.GetWieldedItemIndex(0) != EquipmentIndex.None)
                    {
                        defenderWeaponClass = defenderAgent.Equipment[defenderAgent.GetWieldedItemIndex(0)].CurrentUsageItem.WeaponClass;
                    }
                }
                switch (defenderWeaponClass)
                {
                    case WeaponClass.Dagger:
                    case WeaponClass.OneHandedSword:
                        {
                            defenderPostureDamageModifier = 0.85f;
                            break;
                        }
                    case WeaponClass.TwoHandedSword:
                        {
                            defenderPostureDamageModifier = 0.75f;
                            break;
                        }
                    case WeaponClass.TwoHandedAxe:
                    case WeaponClass.TwoHandedMace:
                    case WeaponClass.TwoHandedPolearm:
                        {
                            defenderPostureDamageModifier = 0.9f;
                            break;
                        }
                    case WeaponClass.Mace:
                    case WeaponClass.Pick:
                        {
                            defenderPostureDamageModifier = 1.15f;
                            break;
                        }
                    case WeaponClass.LargeShield:
                    case WeaponClass.SmallShield:
                        {
                            defenderPostureDamageModifier = 0.8f;
                            break;
                        }
                    default:
                        {
                            defenderPostureDamageModifier = 1f;
                            break;
                        }
                }

                result = basePostureDamage * actionTypeDamageModifier * defenderPostureDamageModifier * attackerPostureModifier;
                //InformationManager.DisplayMessage(new InformationMessage("Deffender PD: " + result));
                return result;
            }

            static float calculateAttackerPostureDamage(Agent defenderAgent, Agent attackerAgent, float absoluteDamageModifier, float actionTypeDamageModifier, ref AttackCollisionData collisionData, MissionWeapon weapon)
            {
                float result = 0f;
                float postureDamageModifier = 1f; // terms and conditions may apply

                float strengthSkillModifier = 500f;
                float weaponSkillModifier = 500f;
                float basePostureDamage = 20f;

                SkillObject attackerWeaponSkill = WeaponComponentData.GetRelevantSkillFromWeaponClass(weapon.CurrentUsageItem.WeaponClass);

                float attackerEffectiveWeaponSkill = 0;
                float attackerEffectiveStrengthSkill = 0;

                if (attackerWeaponSkill != null)
                {
                    attackerEffectiveWeaponSkill = MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(attackerAgent.Character, attackerAgent.Origin, attackerAgent.Formation, attackerWeaponSkill);
                }
                if (attackerAgent.HasMount)
                {
                    attackerEffectiveStrengthSkill = MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(attackerAgent.Character, attackerAgent.Origin, attackerAgent.Formation, DefaultSkills.Riding);
                }
                else
                {
                    attackerEffectiveStrengthSkill = MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(attackerAgent.Character, attackerAgent.Origin, attackerAgent.Formation, DefaultSkills.Athletics);
                }

                float defenderEffectiveWeaponSkill = 0;
                float defenderEffectiveStrengthSkill = 0;

                if (defenderAgent.GetWieldedItemIndex(Agent.HandIndex.MainHand) != EquipmentIndex.None)
                {
                    MissionWeapon defenderWeapon = defenderAgent.Equipment[defenderAgent.GetWieldedItemIndex(Agent.HandIndex.MainHand)];
                    SkillObject defenderWeaponSkill = WeaponComponentData.GetRelevantSkillFromWeaponClass(defenderWeapon.CurrentUsageItem.WeaponClass);
                    if (defenderWeaponSkill != null)
                    {
                        defenderEffectiveWeaponSkill = MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(defenderAgent.Character, defenderAgent.Origin, defenderAgent.Formation, defenderWeaponSkill);
                    }
                    if (defenderAgent.GetWieldedItemIndex(Agent.HandIndex.OffHand) != EquipmentIndex.None)
                    {
                        if (defenderAgent.Equipment[defenderAgent.GetWieldedItemIndex(Agent.HandIndex.OffHand)].IsShield())
                        {
                            defenderEffectiveWeaponSkill += 20f;
                        }
                    }
                }
                if (defenderAgent.HasMount)
                {
                    defenderEffectiveStrengthSkill = MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(defenderAgent.Character, defenderAgent.Origin, defenderAgent.Formation, DefaultSkills.Riding);
                }
                else
                {
                    defenderEffectiveStrengthSkill = MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(defenderAgent.Character, defenderAgent.Origin, defenderAgent.Formation, DefaultSkills.Athletics);
                }

                defenderEffectiveWeaponSkill = defenderEffectiveWeaponSkill / weaponSkillModifier;
                defenderEffectiveStrengthSkill = defenderEffectiveStrengthSkill / strengthSkillModifier;

                attackerEffectiveWeaponSkill = attackerEffectiveWeaponSkill / weaponSkillModifier;
                attackerEffectiveStrengthSkill = attackerEffectiveStrengthSkill / strengthSkillModifier;

                basePostureDamage = basePostureDamage * ((1f + defenderEffectiveStrengthSkill + defenderEffectiveWeaponSkill) / (1f + attackerEffectiveStrengthSkill + attackerEffectiveWeaponSkill));

                switch (weapon.CurrentUsageItem.WeaponClass)
                {
                    case WeaponClass.Dagger:
                    case WeaponClass.OneHandedSword:
                        {
                            postureDamageModifier = 0.85f;
                            break;
                        }
                    case WeaponClass.TwoHandedSword:
                        {
                            postureDamageModifier = 0.75f;
                            break;
                        }
                    case WeaponClass.TwoHandedAxe:
                    case WeaponClass.TwoHandedMace:
                    case WeaponClass.TwoHandedPolearm:
                        {
                            postureDamageModifier = 1f;
                            break;
                        }
                    case WeaponClass.Mace:
                    case WeaponClass.Pick:
                        {
                            postureDamageModifier = 1.15f;
                            break;
                        }
                    default:
                        {
                            postureDamageModifier = 1f;
                            break;
                        }
                }

                result = basePostureDamage * actionTypeDamageModifier * postureDamageModifier;
                //InformationManager.DisplayMessage(new InformationMessage("Attacker PD: " + result));
                return result;
            }

            static void makePostureRiposteBlow(ref Mission mission, ref Blow blow, Agent attackerAgent, Agent victimAgent, ref AttackCollisionData collisionData, in MissionWeapon attackerWeapon, CrushThroughState crushThroughState, Vec3 blowDirection, Vec3 swingDirection, bool cancelDamage, BlowFlags addedBlowFlag)
            {
                blow.BaseMagnitude = collisionData.BaseMagnitude;
                blow.MovementSpeedDamageModifier = collisionData.MovementSpeedDamageModifier;
                blow.InflictedDamage = 1;
                blow.SelfInflictedDamage = collisionData.SelfInflictedDamage;
                blow.AbsorbedByArmor = collisionData.AbsorbedByArmor;

                sbyte weaponAttachBoneIndex = (sbyte)(attackerWeapon.IsEmpty ? (-1) : attackerAgent.Monster.GetBoneToAttachForItemFlags(attackerWeapon.Item.ItemFlags));
                blow.WeaponRecord.FillAsMeleeBlow(attackerWeapon.Item, attackerWeapon.CurrentUsageItem, collisionData.AffectorWeaponSlotOrMissileIndex, weaponAttachBoneIndex);
                blow.StrikeType = (StrikeType)collisionData.StrikeType;
                blow.DamageType = ((!attackerWeapon.IsEmpty && true && !collisionData.IsAlternativeAttack) ? ((DamageTypes)collisionData.DamageType) : DamageTypes.Blunt);
                blow.NoIgnore = collisionData.IsAlternativeAttack;
                blow.AttackerStunPeriod = collisionData.AttackerStunPeriod;
                blow.DefenderStunPeriod = collisionData.DefenderStunPeriod;
                blow.BlowFlag = BlowFlags.None;
                blow.Position = collisionData.CollisionGlobalPosition;
                blow.BoneIndex = collisionData.CollisionBoneIndex;
                blow.Direction = blowDirection;
                blow.SwingDirection = swingDirection;
                //blow.InflictedDamage = 1;
                blow.VictimBodyPart = collisionData.VictimHitBodyPart;
                blow.BlowFlag |= addedBlowFlag;
                attackerAgent.RegisterBlow(blow);
                foreach (MissionBehavior missionBehaviour in mission.MissionBehaviors)
                {
                    missionBehaviour.OnRegisterBlow(victimAgent, attackerAgent, null, blow, ref collisionData, in attackerWeapon);
                }
            }

            static void makePostureBlow(ref Mission mission, ref Blow blow, Agent attackerAgent, Agent victimAgent, ref AttackCollisionData collisionData, in MissionWeapon attackerWeapon, CrushThroughState crushThroughState, Vec3 blowDirection, Vec3 swingDirection, bool cancelDamage, BlowFlags addedBlowFlag)
            {
                blow.BaseMagnitude = collisionData.BaseMagnitude;
                blow.MovementSpeedDamageModifier = collisionData.MovementSpeedDamageModifier;
                blow.InflictedDamage = 1;
                blow.SelfInflictedDamage = collisionData.SelfInflictedDamage;
                blow.AbsorbedByArmor = collisionData.AbsorbedByArmor;

                sbyte weaponAttachBoneIndex = (sbyte)(attackerWeapon.IsEmpty ? (-1) : attackerAgent.Monster.GetBoneToAttachForItemFlags(attackerWeapon.Item.ItemFlags));
                blow.WeaponRecord.FillAsMeleeBlow(attackerWeapon.Item, attackerWeapon.CurrentUsageItem, collisionData.AffectorWeaponSlotOrMissileIndex, weaponAttachBoneIndex);
                blow.StrikeType = (StrikeType)collisionData.StrikeType;
                blow.DamageType = ((!attackerWeapon.IsEmpty && true && !collisionData.IsAlternativeAttack) ? ((DamageTypes)collisionData.DamageType) : DamageTypes.Blunt);
                blow.NoIgnore = collisionData.IsAlternativeAttack;
                blow.AttackerStunPeriod = collisionData.AttackerStunPeriod;
                blow.DefenderStunPeriod = collisionData.DefenderStunPeriod;
                blow.BlowFlag = BlowFlags.None;
                blow.Position = collisionData.CollisionGlobalPosition;
                blow.BoneIndex = collisionData.CollisionBoneIndex;
                blow.Direction = blowDirection;
                blow.SwingDirection = swingDirection;
                //blow.InflictedDamage = 1;
                blow.VictimBodyPart = collisionData.VictimHitBodyPart;
                blow.BlowFlag |= addedBlowFlag;
                victimAgent.RegisterBlow(blow);
                foreach (MissionBehavior missionBehaviour in mission.MissionBehaviors)
                {
                    missionBehaviour.OnRegisterBlow(attackerAgent, victimAgent, null, blow, ref collisionData, in attackerWeapon);
                }
            }

            static void makePostureCrashThroughBlow(ref Mission mission, ref Blow blow, Agent attackerAgent, Agent victimAgent, ref AttackCollisionData collisionData, in MissionWeapon attackerWeapon, CrushThroughState crushThroughState, Vec3 blowDirection, Vec3 swingDirection, bool cancelDamage, BlowFlags addedBlowFlag)
            {
                blow.BaseMagnitude = collisionData.BaseMagnitude;
                blow.MovementSpeedDamageModifier = collisionData.MovementSpeedDamageModifier;
                blow.InflictedDamage = collisionData.InflictedDamage;
                blow.SelfInflictedDamage = collisionData.SelfInflictedDamage;
                blow.AbsorbedByArmor = collisionData.AbsorbedByArmor;

                sbyte weaponAttachBoneIndex = (sbyte)(attackerWeapon.IsEmpty ? (-1) : attackerAgent.Monster.GetBoneToAttachForItemFlags(attackerWeapon.Item.ItemFlags));
                blow.WeaponRecord.FillAsMeleeBlow(attackerWeapon.Item, attackerWeapon.CurrentUsageItem, collisionData.AffectorWeaponSlotOrMissileIndex, weaponAttachBoneIndex);
                blow.StrikeType = (StrikeType)collisionData.StrikeType;
                blow.DamageType = ((!attackerWeapon.IsEmpty && true && !collisionData.IsAlternativeAttack) ? ((DamageTypes)collisionData.DamageType) : DamageTypes.Blunt);
                blow.NoIgnore = collisionData.IsAlternativeAttack;
                blow.AttackerStunPeriod = collisionData.AttackerStunPeriod;
                blow.DefenderStunPeriod = collisionData.DefenderStunPeriod;
                blow.BlowFlag = BlowFlags.None;
                blow.Position = collisionData.CollisionGlobalPosition;
                blow.BoneIndex = collisionData.CollisionBoneIndex;
                blow.Direction = blowDirection;
                blow.SwingDirection = swingDirection;
                //blow.InflictedDamage = 1;
                blow.VictimBodyPart = collisionData.VictimHitBodyPart;
                blow.BlowFlag |= BlowFlags.CrushThrough;
                blow.BlowFlag |= addedBlowFlag;
                victimAgent.RegisterBlow(blow);
                foreach (MissionBehavior missionBehaviour in mission.MissionBehaviors)
                {
                    missionBehaviour.OnRegisterBlow(attackerAgent, victimAgent, null, blow, ref collisionData, in attackerWeapon);
                }
            }
        }


        [HarmonyPatch(typeof(Mission))]
        [HarmonyPatch("OnTick")]
        class OnTickPatch
        {
            //private static int tickCooldownReset = 30;
            //private static int tickCooldown = 0;
            private static float timeToCalc = 0.5f;
            private static float currentDt = 0f;

            static void Postfix(float dt)
            {
                if(XmlConfig.dict["Global.PostureEnabled"] == 1)
                {
                    if (currentDt < timeToCalc)
                    {
                        currentDt += dt;
                    }
                    else
                    {
                        foreach (KeyValuePair<Agent, Posture> entry in AgentPostures.values)
                        {
                            // do something with entry.Value or entry.Key
                            if (entry.Value.posture < entry.Value.maxPosture)
                            {
                                if (XmlConfig.dict["Global.PostureGUIEnabled"] == 1)
                                {
                                    if (entry.Key == Agent.Main)
                                    {
                                        //InformationManager.DisplayMessage(new InformationMessage(entry.Value.posture.ToString()));
                                        if (AgentPostures.postureVisual != null && AgentPostures.postureVisual._dataSource.ShowPlayerPostureStatus)
                                        {
                                            AgentPostures.postureVisual._dataSource.PlayerPosture = (int)entry.Value.posture;
                                            AgentPostures.postureVisual._dataSource.PlayerPostureMax = (int)entry.Value.maxPosture;
                                        }
                                    }

                                    if (AgentPostures.postureVisual != null && AgentPostures.postureVisual._dataSource.ShowEnemyStatus && AgentPostures.postureVisual.affectedAgent == entry.Key)
                                    {
                                        AgentPostures.postureVisual._dataSource.EnemyPosture = (int)entry.Value.posture;
                                        AgentPostures.postureVisual._dataSource.EnemyPostureMax = (int)entry.Value.maxPosture;
                                    }
                                }
                                entry.Value.posture += entry.Value.regenPerTick * 30f;
                            }
                        }
                        currentDt = 0f;
                    }
                }
            }
        }
    }
}
