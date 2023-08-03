using HarmonyLib;
using TenCrowns.AppCore;
using TenCrowns.GameCore;
using System;
using System.Collections.Generic;
using Mohawk.SystemCore;
using TenCrowns.GameCore.Text;
using TenCrowns.ClientCore;

/// TODO priority
/// charge  3
/// evade 2


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
            /// <summary>
            /// main utility method identifying the type of special movement rules, if any
            /// </summary>
            /// <param name="effectUnitTypes"></param>
            /// <param name="info"></param>
            /// <param name="eff"></param>
            /// <returns> int code of the MOVE_SPECIAL </returns>
            public static int getSpecialMove(ReadOnlyList<EffectUnitType> effectUnitTypes, Infos info, out EffectUnitType eff)
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
            ///For Kite and Skirmish
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
            ///for skirmish, which is treated as if the attacker has Push
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
            ///for friendly fire
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


            [HarmonyPatch(nameof(Unit.canAct), new Type[] { typeof(Player), typeof(int), typeof(bool), typeof(bool) })]
            // canAct(Player pActingPlayer, int iCost = 1, bool bRout = false, bool bCancelImprovement = false)
            ///for Kite
            static bool Prefix(ref Unit __instance, ref bool __result, Player pActingPlayer, int iCost = 1, bool bRout = false, bool bCancelImprovement = false)
            {
              //  MohawkAssert.Assert(false, "canAct is called ");
                if (__instance.getCooldown() != __instance.game().infos().Globals.ATTACK_COOLDOWN) //if didn't attack, normal
             //   { MohawkAssert.Assert(false, "hatch 1 ");
                    return true; 
                if (getSpecialMove(__instance.getEffectUnits(), __instance.game().infos(), out _) != isKite) //if not kite, normal. //may want to catch the out and display text
                {
             //       MohawkAssert.Assert(false, "hatch 2 ");
                    return true; }
                if (__instance.getTurnSteps() > 0) //moved; normal
                {
            //        MohawkAssert.Assert(false, "hatch 3 ");
                    return true; }
                __result = true; //can move....once.....hmmmm
                return false;
            }

            [HarmonyPatch(nameof(Unit.attackUnitOrCity), new Type[] { typeof(Tile), typeof(Player) })]
            ///for Kite and friendly fire; defines what can be attacked, and what effect pop up texts to display
            static void Prefix(ref Unit __instance, Tile pToTile, Player pActingPlayer)
            {
                Tile pFromTile = __instance.tile();
                List<TileText> azTileTexts = null;
                List<int> aiAdditionalDefendingUnits = new List<int>();
                List<Unit.AttackOutcome> outcomes = new List<Unit.AttackOutcome>();
                int cityHP = -1;

                if (isKite == getSpecialMove(__instance.getEffectUnits(), __instance.game().infos(), out EffectUnitType eff))
                {
                    __instance.game().addTileTextAllPlayers(ref azTileTexts, pFromTile.getID(), () => "kite");
                }
                //  

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
            ///for skirmisher; a bit hacky
            static bool Prefix(ref InfoEffectUnit __result, ref List<InfoEffectUnit> ___maEffectUnits, ref EffectUnitType eIndex)
            {
                //NOTE this is used when looking for an effect to explain hasPush but none was found; we can explain it on Skirmisher. This method could have side effects
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
            ///for disabling unnecessary help text for all special effects
            static bool Prefix(AttackType eAttack, HelpText __instance)
            {
                using (new UnityProfileScope("HelpText.buildAttackLinkVariable"))
                {
                    InfoAttack infoAttack = __instance.ModSettings.Infos.attack(eAttack);

                    if (infoAttack.mePattern == AttackPatternType.NONE)
                    {
                        // __result is nada
                        return false;
                    }
                }
                return true;
            }

            [HarmonyPatch(nameof(HelpText.buildEffectUnitHelp))]
            ///for displaying all custom special effects 
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
                    else if (iValue == isKite)
                    {
                        builder.AddTEXT("TEXT_HELP_KITE_SHORT");
                    }
                }
                catch (Exception e)
                {
                    MohawkAssert.Assert(false, "Move Special not found; mod failed to unload? " + e.Message);
                    // harmony.UnpatchAll(MY_HARMONY_ID);
                }
            }
        }

        [HarmonyPatch(typeof(ClientInput))]
        public class PatchClient
        {
            [HarmonyPatch(nameof(ClientInput.moveTo))]
            // public virtual void moveTo(Unit pUnit, Tile pTile)
            ///outside of gamecore, so be very careful here. Client level game logic control in base game...tsk tsk. Changing it to make Kiting work
            static bool Prefix(ref IApplication ___APP, Unit pUnit, Tile pTile)
            {
                ClientManager ClientMgr = ___APP.GetClientManager();
                if (pUnit.getCooldown() != ClientMgr?.Infos.Globals.ATTACK_COOLDOWN)
                    return true;
            
                if (PatchUnitBehaviors.getSpecialMove(pUnit.getEffectUnits(), ClientMgr?.Infos, out _) != isKite) //if not kite, normal.
                {
                    return true; 
                }
                if (pUnit.getTurnSteps() > 0) //moved; normal
                {
                    return true; 
                }
               
                ClientMgr.sendMoveUnit(pUnit, pTile, false, false, ClientMgr.Selection.getSelectedUnitWaypoint());
                ClientMgr.Selection.setSelectedUnitWaypoint(null);
                return false;
            }
        }
    }
}
