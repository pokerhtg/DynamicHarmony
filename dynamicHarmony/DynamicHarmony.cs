using HarmonyLib;
using TenCrowns.AppCore;
using TenCrowns.GameCore;
using System;
using System.Collections.Generic;
using Mohawk.SystemCore;
using TenCrowns.GameCore.Text;
using UnityEngine;

/// TODO priority
/// charge  3
/// evade 2
/// kite 1

namespace dynamicHarmony
{
    public class DynamicHarmony : ModEntryPointAdapter
    {
        public const string MY_HARMONY_ID = "harry.DynamicHarmony.patch";
        public static Harmony harmony;

        public override void Initialize(ModSettings modSettings)
        {
            if (harmony != null)
                return;
            harmony = new Harmony(MY_HARMONY_ID);
            harmony.PatchAll();
        }

        public override void Shutdown()
        {
            harmony.UnpatchAll(MY_HARMONY_ID);
        }
        //decoding special effects from aiAttackValue's MOVE_SPECIAL
        static readonly int isSkirmisher = -1;
        static readonly int isKite = -2;
            
            
        [HarmonyPatch(typeof(Unit))]
        public class PatchUnitBehaviors
        {
            static EffectUnitType specialEffect = EffectUnitType.NONE;

            private static int getSpecialMove(ReadOnlyList<EffectUnitType> effectUnitTypes, Infos info, out EffectUnitType eff)
            {
                int index = -1;
                eff = specialEffect;
                for (AttackType eLoopAttack = 0; eLoopAttack < info.attacksNum(); eLoopAttack++)
                {
                    if (eLoopAttack == info.getType<AttackType>("MOVE_SPECIAL"))
                        index = (int)eLoopAttack;
                }

                if (index > -1)
                    foreach (EffectUnitType eLoopEffectUnit in effectUnitTypes)
                    {
                        int iSubValue = info.effectUnit(eLoopEffectUnit).maiAttackValue[index];
                        if (iSubValue != 0)
                        {
                            eff = eLoopEffectUnit;
                            specialEffect = eff;
                            return iSubValue;
                        }
                    }
                return 0;
            }
            
            

            [HarmonyPatch(nameof(Unit.attackEffectPreview))]
            ///define special effects to display when this unit is attacked
            static bool Prefix(ref TextBuilder __result, ref Unit __instance, TextBuilder builder, Unit pFromUnit, Tile pFromTile)
            {
                var pToTile = __instance.tile();
                var g = __instance.game();
                EffectUnitType defenderEffect, attackerEffect;
                int specialMoveCodeDefender = getSpecialMove(__instance.getEffectUnits(), g.infos(), out defenderEffect);
                int specialMoveCodeAttacker = getSpecialMove(pFromUnit.getEffectUnits(), g.infos(), out attackerEffect);
                bool bSkirmish = isSkirmisher == specialMoveCodeDefender;
                bool bKite = isKite == specialMoveCodeAttacker;
                bool special = false;
                if (bSkirmish)
                {
                    if (__instance.attackDamagePreview(pFromUnit, pFromTile, pFromUnit.player()) >= __instance.getHP()) //dead
                        return true; //not special

                    if (pFromUnit.canAttackUnitOrCity(pFromTile, pToTile, null) && pToTile.isTileAdjacent(pFromTile)) //skirmish condition: adj and getting hit
                    {
                        var txt2 = g.HelpText.getGenderedEffectUnitName(g.infos().effectUnit(defenderEffect), pFromUnit.getGender());
                        builder.AddTEXT(txt2);
                        __result = builder;
                        special = true; //Special!
                    }
                }
                if (bKite)
                {
                    if (pFromUnit.canAttackUnitOrCity(pFromTile, pToTile, null) && pFromUnit.getTurnSteps() == 0) //kite condition: not moved and hitting
                    {
                        var txt2 = g.HelpText.getGenderedEffectUnitName(g.infos().effectUnit(attackerEffect), pFromUnit.getGender());
                        builder.AddTEXT(txt2);
                        __result = builder;
                        special = true; //Special!
                    }
                }
                return !special;
            }

            [HarmonyPatch(nameof(Unit.hasPush))]
            static void Postfix(ref bool __result, ref Unit __instance, Tile pToTile)
            {
                if (__result)
                    return;

                if (!pToTile.isTileAdjacent(__instance.tile()))
                    return; //false

                using (var unitListScoped = CollectionCache.GetListScoped<int>())
                {
                    pToTile.getAliveUnits(unitListScoped.Value);

                    foreach (int iLoopUnit in unitListScoped.Value)
                    {
                        Unit pLoopUnit = __instance.game().unit(iLoopUnit);
                        if (getSpecialMove(pLoopUnit.getEffectUnits(), __instance.game().infos(), out _) != isSkirmisher)
                        {
                            return; //false
                        }
                    }
                }
                __result = true; //if all units in the target tile are retreating, hasPush = true;
            }

            [HarmonyPatch(nameof(Unit.attackDamagePreview))]
            static void Postfix(ref int __result, Unit __instance, Unit pFromUnit, Tile pMouseoverTile, bool bCheckHostile)
            {
                if (!bCheckHostile)
                    return;
                if (pFromUnit == null)
                    return;
                for (AttackType eLoopAttack = 0; eLoopAttack < __instance.game().infos().attacksNum(); eLoopAttack++)
                {
                    int iValue = pFromUnit.attackValue(eLoopAttack);
                    if (iValue > 0)
                    {
                        using (var tilesScoped = CollectionCache.GetListScoped<int>())
                        {
                            Tile pFromTile = pFromUnit.tile();
                            Tile pTile = __instance.tile();

                            pFromTile.getAttackTiles(tilesScoped.Value, pMouseoverTile, pFromUnit.getType(), eLoopAttack, iValue);
                            foreach (int iLoopTile in tilesScoped.Value)
                            {
                                Tile pLoopTile = __instance.game().tile(iLoopTile);

                                if (pLoopTile == pTile)
                                {
                                    if (pLoopTile.hasCity())
                                    {
                                        __result = pFromUnit.attackCityDamage(pFromTile, pLoopTile.city(), pFromUnit.attackPercent(eLoopAttack), false);
                                    }
                                    //then damage anyway. AKA friendly fire
                                    else
                                        __result = pFromUnit.attackUnitDamage(pFromTile, __instance, false, pFromUnit.attackPercent(eLoopAttack));

                                }
                            }
                        }
                    }
                }
            }
            [HarmonyPatch(nameof(Unit.attackUnitOrCity), new Type[] { typeof(Tile), typeof(Player) })]
            static void Prefix(ref Unit __instance, Tile pToTile, Player pActingPlayer)
            {
                Tile pFromTile = __instance.tile();
                List<TileText> azTileTexts = null;
                List<int> aiAdditionalDefendingUnits = new List<int>();
                List<Unit.AttackOutcome> outcomes = new List<Unit.AttackOutcome>();
                int cityHP = -1;
                for (AttackType eLoopAttack = 0; eLoopAttack < __instance.game().infos().attacksNum(); eLoopAttack++)
                {
                    int iValue = __instance.attackValue(eLoopAttack);
                    if (iValue <= 0)
                        continue;
                    using (var tilesScoped = CollectionCache.GetListScoped<int>())
                    {
                        pFromTile.getAttackTiles(tilesScoped.Value, pToTile, __instance.getType(), eLoopAttack, iValue);

                        foreach (int iLoopTile in tilesScoped.Value)
                        {
                            Tile pLoopTile = __instance.game().tile(iLoopTile);

                            if (__instance.canDamageUnitOrCity(pLoopTile, true))
                                continue;
                            int percent = __instance.attackPercent(eLoopAttack);
                            Unit pLoopDefendingUnit = pLoopTile.defendingUnit();
                            if (pLoopDefendingUnit == null)
                                continue;
                            if (percent < 1)
                                continue;
                            if (pLoopTile.hasCity())
                            {
                                cityHP = pLoopTile.city().getHP();
                                int dmg = __instance.attackCityDamage(pFromTile, pLoopTile.city(), percent);
                                pLoopTile.city().changeDamage(dmg);
                                __instance.game().addTileTextAllPlayers(ref azTileTexts, pLoopTile.getID(), () => "-" + dmg + " HP");
                                outcomes.Add(pLoopTile.city().getHP() == 0 ? Unit.AttackOutcome.CAPTURED : Unit.AttackOutcome.CITY);
                            }
                            else
                            {
                                aiAdditionalDefendingUnits.Add(pLoopDefendingUnit.getID());
                                int dmg = __instance.attackUnitDamage(pFromTile, pLoopDefendingUnit, false, percent);
                                pLoopDefendingUnit.changeDamage(dmg, false);
                                __instance.game().addTileTextAllPlayers(ref azTileTexts, pLoopTile.getID(), () => "-" + dmg + " HP");
                                outcomes.Add(pLoopDefendingUnit.getHP() == 0 ? Unit.AttackOutcome.KILL : Unit.AttackOutcome.NORMAL);

                            }
                                
                        }
                    }

                    
                }
                if (azTileTexts != null)
                {
                    //   MohawkAssert.Assert(false, "got text" + azTileTexts.Last());
                    __instance.game().sendUnitBattleAction(__instance, null, pFromTile, pToTile, pToTile, Unit.AttackOutcome.NORMAL, azTileTexts, pActingPlayer?.getPlayer() ?? PlayerType.NONE, cityHP > 0, cityHP, aiAdditionalDefendingUnits, outcomes);
                }
            }


            
        }

        [HarmonyPatch(typeof(Infos))]
        public class PatchInfos
        {
            [HarmonyPatch(nameof(Infos.effectUnit))]
            static bool Prefix(ref InfoEffectUnit __result, ref List<InfoEffectUnit> ___maEffectUnits, ref EffectUnitType eIndex)
            {
                if (eIndex < 0)
                    foreach (var effect in ___maEffectUnits)
                    {
                        if (effect.mzType.Equals("EFFECTUNIT_SKIRMISHER"))
                        {
                            __result = effect;
                            return false;
                        }
                    }
                return true;
            }
        }

        [HarmonyPatch(typeof(HelpText))]
        public class PatchHelpText
        {
            [HarmonyPatch(nameof(HelpText.buildAttackLinkVariable))]
            static bool Prefix(AttackType eAttack, HelpText __instance)
            {
                using (new UnityProfileScope("HelpText.buildAttackLinkVariable"))
                {
                    InfoAttack infoAttack = __instance.ModSettings.Infos.attack(eAttack);

                    if (infoAttack.mePattern == AttackPatternType.NONE)
                    {
                        // __result = ? nuffin?
                        return false;
                    }
                }
                return true;
            }

            [HarmonyPatch(nameof(HelpText.buildEffectUnitHelp))]
            static void Prefix(TextBuilder builder, EffectUnitType eEffectUnit, HelpText __instance)
            {

                Infos infos = __instance.ModSettings.Infos;
                try
                {
                    int iValue = infos.effectUnit(eEffectUnit).maiAttackValue[(int)infos.getType<AttackType>("MOVE_SPECIAL")];

                    if (iValue == isSkirmisher)
                    {
                        builder.AddTEXT("TEXT_HELP_RETREAT_SHORT");
                    }
                }
                catch (Exception e)
                {
                    MohawkAssert.Assert(false, "Move Special not found; mod failed to unload? " + e.Message);
                    harmony.UnpatchAll(MY_HARMONY_ID);
                }
            }
        }
    }
}
