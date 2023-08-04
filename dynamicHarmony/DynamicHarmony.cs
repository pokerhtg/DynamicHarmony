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
            static bool Prefix(ref TextBuilder __result, ref Unit __instance, TextBuilder builder, ref Unit pFromUnit, Tile pFromTile)
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

                    if (pFromUnit.canAttackUnitOrCity(pFromTile, pToTile, null) && pFromUnit.info().mbMelee && !pToTile.hasCity() && (pToTile.improvement()?.miDefenseModifier ?? 0 )< 1) //skirmish condition: adj and getting hit
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
           //     MohawkAssert.Assert(false, "entering haspush");
                if (__result)
                    return;

                if (!__instance.info().mbMelee)
                    return; //false
                if (pToTile.hasCity() || (pToTile.improvement()?.miDefenseModifier ?? 0) > 0) //can't push off defensive structures
                    return;
                
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
          //      MohawkAssert.Assert(false, "hasPush does things");
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
                if (!pMouseoverTile.hasHostileUnit(__instance.getTeam()))
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
                                if (pFromTile == pLoopTile)
                                    continue; //for now, let's disble friendly fire on self
                                if (pLoopTile == pTile)
                                {
                                    if (pLoopTile.hasCity())
                                    {
                                     //   __result = pFromUnit.attackCityDamage(pFromTile, pLoopTile.city(), pFromUnit.attackPercent(eLoopAttack), false);
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

                Infos info = __instance.game().infos();
                if (isKite == getSpecialMove(__instance.getEffectUnits(), info, out EffectUnitType eff))
                {
                    __instance.game().addTileTextAllPlayers(ref azTileTexts, pFromTile.getID(), () => "kite");
                }
                //  

                for (AttackType eLoopAttack = 0; eLoopAttack < info.attacksNum(); eLoopAttack++)
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

                            if (__instance.canDamageUnitOrCity(pLoopTile, true)) //if this tile can be damaged, then original code will handle it
                                continue;
                            if (pLoopTile == pFromTile) //disable friendly damage on self
                                continue; 
                            int percent = __instance.attackPercent(eLoopAttack);
                            Unit pLoopDefendingUnit = pLoopTile.defendingUnit();
                            if (pLoopDefendingUnit == null)
                                continue;
                            if (percent < 1)
                                continue;
                            if (pLoopTile.hasCity())
                            {

                                City city = pLoopTile.city();
                                cityHP = city.getHP();
                                int dmg = __instance.attackCityDamage(pFromTile, city, percent);
                                city.changeDamage(dmg);
                                __instance.game().addTileTextAllPlayers(ref azTileTexts, pLoopTile.getID(), () => "-" + dmg + " HP");
                                outcomes.Add(city.getHP() == 0 ? Unit.AttackOutcome.CAPTURED : Unit.AttackOutcome.CITY);
                                city.processYield(info.Globals.DISCONTENT_YIELD, info.Globals.CITY_ATTACKED_DISCONTENT);                  
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

        [HarmonyPatch(typeof(Unit.UnitAI))]
        public class PatchAI
         {
            [HarmonyPatch(nameof(Unit.UnitAI.attackValue))]
            //public virtual int attackValue(Tile pFromTile, Tile pTargetTile, bool bCheckOtherUnits, int iExtraModifier, out bool bCivilian, out int iPushTileID, out bool bStun)
            static void Postfix(ref int __result, ref Unit ___unit, Tile pFromTile, Tile pTargetTile)
            {
                if (__result == 0) //not a target. Shortcircuit
                    return;
                //factor in some more counterattack avoidance
                __result -= 50 * ___unit.getCounterAttackDamage(pTargetTile.defendingUnit(), pTargetTile);

                for (AttackType eLoopAttack = 0; eLoopAttack < ___unit.game().infos().attacksNum(); eLoopAttack++)
                {
                    int iAttackValue = ___unit.attackValue(eLoopAttack);
                    if (iAttackValue < 1)
                        continue;
                    int iAttackPercent = ___unit.attackPercent(eLoopAttack);
                    if (iAttackPercent < 1)
                        continue;
                   
                using (var tilesScoped = CollectionCache.GetListScoped<int>())
                    {
                        pFromTile.getAttackTiles(tilesScoped.Value, pTargetTile, ___unit.getType(), eLoopAttack, iAttackValue);

                        foreach (int iAttackTile in tilesScoped.Value)
                        {
                            Tile pAttackTile = ___unit.game().tile(iAttackTile);

                            if (!___unit.canTargetTile(pFromTile, pAttackTile)) //if this unit canNOT be targeted, then it's friendly or neutral
                            {
                                Unit pUnit = pAttackTile.defendingUnit();
                                if (pUnit != null)
                                {
                                    int iDamage = ___unit.attackUnitDamage(pFromTile, pUnit, false, iAttackPercent, -1, false);
                                    if ( ___unit.game().areTeamsAllied(pUnit.getTeam(), ___unit.getTeam())) //friendly!
                                    {
                                        __result -= 150 * iDamage; //100% is default; we treat our soldiers' lives slightly above average
                                    }
                                    else
                                        __result = 50 * iDamage; //slight preference for causing collateral damage
                                }
                            }
                        }
                    } 
                }
                __result = Math.Max(0, __result); //negative should be handled same as zero...but just in case. zero means not a valid target (which is a stronger rejection than base method's floor of 1).
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


        

        [HarmonyPatch]
        public class PatchClient
        {
            [HarmonyPatch(typeof(ClientInput), nameof(ClientInput.moveTo))]
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

            [HarmonyPatch(typeof(ClientUI), nameof(ClientUI.updateCityWidget))]
            //  public virtual void updateCityWidget(City city, bool bDamagePreviewOnly = false)
            ///outside of gamecore, so be very careful here. Client level game logic control in base game...tsk tsk. Directly manipulating the city widget to display friendly fire
            static void Postfix(ref IApplication ___APP, ref ClientUI __instance, ref HashSet<int> ___msiAffectedCities, City city)

            {
                ClientManager ClientMgr = ___APP.GetClientManager();

                Unit pFromUnit = ((ClientMgr.Selection.isSelectedUnit()) ? ClientMgr.Selection.getSelectedUnit() : ClientMgr.Selection.getMouseoverTileUnit());

                if (pFromUnit == null)
                    return;

                Tile pMouseoverTile = ClientMgr.Selection.getMouseoverTile();
                if (pMouseoverTile == null)
                    return;

                if (!pFromUnit.canTargetUnitOrCity(pMouseoverTile, true)) //if you can't target, you can't fire; so no friendly fire possible
                    return;

              //  if (___msiAffectedCities.Contains(city.getID())) //widget already updated
                  //  return; 

                for (AttackType eLoopAttack = 0; eLoopAttack < ClientMgr.Infos.attacksNum(); eLoopAttack++)
                {
                   
                    int iValue = pFromUnit.attackValue(eLoopAttack);
                    if (iValue > 0)
                    {
                        using (var attackTilesScope = CollectionCache.GetListScoped<int>())
                        {
                            pFromUnit.tile().getAttackTiles(attackTilesScope.Value, pMouseoverTile, pFromUnit.getType(), eLoopAttack, iValue);
                            foreach (int iLoopTile in attackTilesScope.Value)
                            {
                               // MohawkAssert.Assert(false, "doing it? " + ClientMgr.GameClient.tile(iLoopTile) + " is not " + city.tile());
                                if (ClientMgr.GameClient.tile(iLoopTile) == city.tile())
                                {
                                    MohawkAssert.Assert(false, "doing it! ");
                                   
                                    ___msiAffectedCities.Add(city.getID());
                                    int iDamagePreviewHP = pFromUnit.attackCityDamage(pFromUnit.tile(), city, pFromUnit.attackPercent(eLoopAttack));
                                    UIAttributeTag widget = getCityWidgetTag(__instance, city.getID());
                                 
                                    widget.SetInt("DamagePreviewHP", iDamagePreviewHP);
                                    return;
                                }
                            }
                        }
                    }
                }
              
                
            }

            [HarmonyReversePatch]
            [HarmonyPatch(typeof(ClientUI), "getCityWidgetTag")]
            public static UIAttributeTag getCityWidgetTag(ClientUI ui, int cityID)
            {
                throw new NotImplementedException("It's a stub");
            }
        }
    }
}
