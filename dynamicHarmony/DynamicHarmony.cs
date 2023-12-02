using HarmonyLib;
using TenCrowns.AppCore;
using TenCrowns.GameCore;
using System;
using UnityEngine;
using System.Collections.Generic;
using Mohawk.SystemCore;
using TenCrowns.GameCore.Text;
using TenCrowns.ClientCore;
using System.Linq;

/// TODO priority
/// evade 2


namespace dynamicHarmony
{
    public class DynamicHarmony : ModEntryPointAdapter
    {
        public const string MY_HARMONY_ID = "harry.DynamicHarmony.patch";
        public static Harmony harmony;
        public static bool debug = false;
        public static EffectUnitType retreat = EffectUnitType.NONE;

        public override void Initialize(ModSettings modSettings)
        {
            if (harmony != null)
                return;
            harmony = new Harmony(MY_HARMONY_ID);
            harmony.PatchAll();
        }

        public override void Shutdown()
        {
            if (harmony == null)
                return;
            harmony.UnpatchAll(MY_HARMONY_ID);
            harmony = null;
         
        }
        //decoding special effects from aiAttackValue's MOVE_SPECIAL
        static readonly int isSkirmisher = -1;
        static readonly int isKite = -2;
       
        [HarmonyPatch(typeof(Unit))]
        public class PatchUnitBehaviors
        {
           
            /// <summary>
            /// main utility method identifying the type of special movement rules, if any
            /// </summary>
            /// <param name="effectUnitTypes"></param>
            /// <param name="info"></param>
            /// <param name="eff"></param>
            /// <returns> int code of the MOVE_SPECIAL </returns>
            public static int getSpecialMove(ReadOnlyList<EffectUnitType> effectUnitTypes, Infos info, out EffectUnitType eff)
            {
                int index = AttackIndex("MOVE_SPECIAL", info);
                eff = EffectUnitType.NONE;
                
                if (index > -1)
                    foreach (EffectUnitType eLoopEffectUnit in effectUnitTypes)
                    {
                        int iSubValue = info.effectUnit(eLoopEffectUnit).maiAttackValue[index];
                        if (iSubValue != 0)
                        {
                            eff = eLoopEffectUnit;
                           
                            return iSubValue;
                        }
                    }
                return 0;
            }
            public static int AttackIndex(String type, Infos info)
            {
                for (AttackType eLoopAttack = 0; eLoopAttack < info.attacksNum(); eLoopAttack++)
                {
                    if (eLoopAttack == info.getType<AttackType>(type))
                        return (int)eLoopAttack;
                }
                return -1;
            }
            public static bool isCharge(Unit unit, out Tile impactFrom, Tile pFromTile=null, Tile pToTile=null)
            {
                ///TODO: use attack value's Value from xml...to denote the max charge distance? Right now all charges are defined to have range of exactly 2
                impactFrom = null;
                if (unit == null)
                    return false;
               
                bool isCharge = false;
                var info = unit.game().infos();
                int chargeIndex = AttackIndex("CHARGE", info);
                
                if (chargeIndex > -1)
                    foreach (EffectUnitType eLoopEffectUnit in unit.getEffectUnits())
                    {
                        int iSubValue = info.effectUnit(eLoopEffectUnit)?.maiAttackValue[chargeIndex] ?? 0;
                        if (iSubValue != 0)
                        {
                            isCharge = true;
                        }
                    }
               
                if (unit.info().mbMelee && isCharge) 
                {
                
                    if (pToTile == null || pFromTile == null)
                        return true;

                    List<int> enemies = new List<int>();
                    pToTile.getAliveUnits(enemies);

                    if (!pToTile.hasCity() && enemies.Count == 1 && ((pToTile.improvement()?.miDefenseModifier?? 0) < 1) && pFromTile.distanceTile(pToTile) == 2 //here's that "2" referred to in the TODO above
                        && (pToTile.defendingUnit()?.movement() ?? -1) > 0 && pToTile.canUnitOccupy(unit, unit.getTeam(), false, false, true)
                        && !unit.game().unit(enemies.First()).isWorker() ) //charging against worker, who could be making a wonder, is pretty OP. Banned!
                    {
                        List<int> adjTiles = new List<int>();
                        pFromTile.getTilesAtDistance(1, adjTiles, false);
                        Tile candidate1 = null, candidate2 =null;
                        for (int i = 0; i < adjTiles.Count; i++)
                        {
                            Tile friend = unit.game().tile(adjTiles[i]);
                            if (pToTile.isTileAdjacent(friend))
                            {
                                if (candidate1 == null)
                                    candidate1 = friend;
                                else if (candidate2 == null)
                                    candidate2 = friend;
                                else
                                    MohawkAssert.Assert(false, "Found a third tile; geometry is broken");
                            }
                        }
                        if (candidate1 != null && candidate1.canUnitOccupy(unit, unit.getTeam(), true, true, true))
                        {
                            impactFrom= candidate1;
                            return true;
                        }
                        else if(candidate2 != null && candidate2.canUnitOccupy(unit, unit.getTeam(), true, true, true))
                        {
                            impactFrom = candidate2;
                            return true;
                        }
                    }
                }

                return false;
            }

            /// <summary>
            /// find the first effectUnit that provides an attack matching the name of the attack
            /// </summary>
            /// <param name="effectUnitTypes"></param>
            /// <param name="target"></param>
            /// <param name="info"></param>
            /// <returns></returns>
            public static EffectUnitType getEffectName(ReadOnlyList<EffectUnitType> effectUnitTypes, String target, Infos info)
            {
                for (AttackType eLoopAttack = 0; eLoopAttack < info.attacksNum(); eLoopAttack++)
                {
                    if (eLoopAttack == info.getType<AttackType>(target))
                        foreach (EffectUnitType eLoopEffectUnit in effectUnitTypes)
                        {
                            int iSubValue = info.effectUnit(eLoopEffectUnit).maiAttackValue[(int) eLoopAttack];
                            if (iSubValue != 0)
                            {
                                return eLoopEffectUnit;
                            }
                        }
                }
                return EffectUnitType.NONE;
            }

            [HarmonyPatch(nameof(Unit.hasPush))]
            ///for skirmish, which is treated as if the attacker has Push
            static void Postfix(ref bool __result, ref Unit __instance, Tile pToTile)
            {
                if (__result)
                {      
                    return;
                }
                try
                {
                    if (pToTile.hasCity()) //can't push off defensive structures
                        return;

                    using (var unitListScoped = CollectionCache.GetListScoped<int>())
                    {
                        pToTile.getAliveUnits(unitListScoped.Value);

                        foreach (int iLoopUnit in unitListScoped.Value)
                        {
                            Unit pLoopUnit = __instance.game().unit(iLoopUnit);
                            if (pToTile.hasImprovement() && pLoopUnit.improvementDefenseModifier(pToTile.getImprovement(), pToTile) > 0)
                                return;
                            if (getSpecialMove(pLoopUnit.getEffectUnits(), __instance.game().infos(), out _) != isSkirmisher)
                            {
                                return; //false, no push
                            }
                        }
                    }
                    if (!pToTile.isTileAdjacent(__instance.tile()))
                        return;
                    __result = true; //if all units in the target tile are retreating, hasPush = true
                }
                catch (Exception) {
                    return;
                }
            }

            [HarmonyPatch(nameof(Unit.getPushTile))]
            // public virtual Tile getPushTile(Unit pUnit, Tile pFromTile, Tile pToTile)
            ///for skirmish, which is treated as if the attacker has Push
            static void Postfix(ref Tile __result, Unit pUnit, Tile pFromTile, Tile pToTile)
            {
                if (__result == null)
                {
                    DirectionType attackerDir = pFromTile.getDirection(pToTile);
                    if (attackerDir == DirectionType.NONE)
                        __result = pToTile;
                    else
                    {
                        if (getSpecialMove(pUnit.getEffectUnits(),pUnit.game().infos(), out _) == isSkirmisher)
                        {
                            __result = pUnit.bounceTile();
                        }      
                    }
                }
            }
            [HarmonyPatch(nameof(Unit.getTargetTiles))]
            //         public virtual void getTargetTiles(Tile pFromTile, List<int> aiTargetTiles)
            ///charge! gives greater attack range, sort of
            static void Postfix(ref Unit __instance, Tile pFromTile, ref List<int> aiTargetTiles)
            {
                if (isCharge(__instance, out _))
                {
                    int iNumValidTilesAtRange = 0;
                    using (var listScoped = CollectionCache.GetListScoped<int>())
                    {
                        pFromTile.getTilesAtDistance(2, listScoped.Value);

                        foreach (int iLoopTile in listScoped.Value)
                        { 
                            Tile pToTile = __instance.game().tile(iLoopTile);
                            if (isCharge(__instance, out _, pFromTile, pToTile)) 
                            {
                                ++iNumValidTilesAtRange;
                                aiTargetTiles.Add(iLoopTile);
                            }
                        }
                    }
                }
            }
            [HarmonyPatch(nameof(Unit.canTargetTile), new Type[] { typeof(Tile), typeof(Tile)})]
            //       public virtual bool canTargetTile(Tile pFromTile, Tile pToTile)
            ///charge! gives greater attack range, sort of
            static void Postfix(ref Unit __instance, ref bool __result, Tile pFromTile, Tile pToTile)
            {
                if (__result)
                    return;
                if (isCharge(__instance, out _, pFromTile, pToTile))
                    __result = true;

            }
            [HarmonyPatch(nameof(Unit.attackDamagePreview))]
            ///for Charge
            static void Prefix(Unit __instance, Unit pFromUnit, ref Tile pMouseoverTile)
            {
                if (isCharge(pFromUnit, out Tile fromTile, pMouseoverTile, __instance.tile()))
                {
                    pMouseoverTile = fromTile;
                }
            }
            [HarmonyPatch(nameof(Unit.attackDamagePreview))]
            ///for friendly fire
            static void Postfix(ref int __result, Unit __instance, Unit pFromUnit, Tile pMouseoverTile, bool bCheckHostile)
            {
                if (debug)
                    Debug.Log("entering post attack preview");
                if (!bCheckHostile || __result != 0)
                    return;
              
                Game g = __instance.game();
                if (pFromUnit == null || g.isHostileUnitUnit(pFromUnit, __instance))
                    return;
                Tile ownTile = __instance.tile();
                Tile pFromTile = pFromUnit.tile();

                if (pMouseoverTile == null || pMouseoverTile.defendingUnit() == null || !pFromUnit.canTargetTile(pMouseoverTile))
                    return;
               
                for (AttackType eLoopAttack = 0; eLoopAttack < g.infos().attacksNum(); eLoopAttack++)
                {
                    int iValue = pFromUnit.attackValue(eLoopAttack);
                    if (iValue > 0)
                    {
                        if (debug)
                            Debug.Log("can attack " + eLoopAttack + " with a power of " + iValue);
                        using (var tilesScoped = CollectionCache.GetListScoped<int>())
                        {        
                            pFromTile.getAttackTiles(tilesScoped.Value, pMouseoverTile, pFromUnit.getType(), eLoopAttack, iValue);
                            if (debug)
                                Debug.Log(String.Format("found {0} tiles. mouseover: ({1}, {2})",tilesScoped.Value.Count, pMouseoverTile.getX(), pMouseoverTile.getY()));
                            foreach (int iLoopTile in tilesScoped.Value)
                            {
                                Tile potentialTargetTile = g.tile(iLoopTile);
                                if (debug)
                                   Debug.Log(String.Format("({2}, {3}) is considering the impact of attacking ({0}, {1})", potentialTargetTile.getX(), potentialTargetTile.getY(), ownTile.getX(), ownTile.getY()));

                                if (pFromTile == potentialTargetTile) 
                                { 
                                    continue; //for now, let's disble friendly fire on self
                                }
                                if (potentialTargetTile == ownTile)
                                {
                                    if (debug)
                                        Debug.Log("I'm hit!!");

                                    if (ownTile.hasCity())
                                    {
                                     //   __result = pFromUnit.attackCityDamage(pFromTile, potentialTargetTile.city(), pFromUnit.attackPercent(eLoopAttack), false);
                                    }
                                    //then damage anyway. AKA friendly fire
                                    else
                                    {
                                        int atkPercent = pFromUnit.attackPercent(eLoopAttack);
                                        if (pFromUnit.info().mbMelee)
                                        {
                                            //melee units don't do much friendly fire...
                                            atkPercent /= 3;
                                        }
                                        __result += pFromUnit.attackUnitDamage(pFromTile, __instance, false, atkPercent, bCheckOurUnits:false);
                                        if (__result >= __instance.getHP())
                                            __result = __instance.getHP() -1; //friendly fire is now no longer deadly
                                       
                                    }
                                }
                            }
                        }
                    }
                }
            }
   
            [HarmonyPatch(nameof(Unit.attackEffectPreview))]
            ///define special effects to display when this unit is attacked
            ///For Kite and Skirmish
            static bool Prefix(ref TextBuilder __result, ref Unit __instance, ref TextBuilder builder, ref Unit pFromUnit, Tile pFromTile)
            {
                ///pFromUnit is attacking this (__instance) unit in this preview
                var pToTile = __instance.tile();
                var g = __instance.game();
                EffectUnitType defenderEffect, attackerEffect;

                int specialMoveCodeAttacker = getSpecialMove(pFromUnit.getEffectUnits(), g.infos(), out attackerEffect);

                bool bKite = isKite == specialMoveCodeAttacker;
                bool special = false;

                if (isCharge(pFromUnit, out _, pFromTile, pToTile))
                {
                    var charge = g.HelpText.getGenderedEffectUnitName(g.infos().effectUnit(getEffectName(pFromUnit.getEffectUnits(), "CHARGE", g.infos())), pFromUnit.getGender());
                    builder.AddTEXT(charge);
                    __result = builder;
                    special = true; //Special!
                }

              
                if (isSkirmishing(pFromUnit, pFromTile, __instance, out defenderEffect)) 
                {
                    //Special!
                    if (pFromUnit.getPushTile(__instance, pFromTile, pToTile) == null)
                        builder.AddTEXT("TEXT_CONCEPT_STUN");
                    else
                    {
                        builder.AddTEXT(g.HelpText.getGenderedEffectUnitName(g.infos().effectUnit(defenderEffect), pFromUnit.getGender()));
                        if (pFromUnit.hasStun(pToTile))
                            builder.AddTEXT("TEXT_CONCEPT_STUN");
                    }

                    __result = builder;
                    special = true;
                }
                
                if (bKite)
                {
                    if (__instance.attackDamagePreview(pFromUnit, pFromTile, pFromUnit.player()) >= __instance.getHP() && //dead
                          pFromUnit.canAdvanceAfterAttack(pFromTile, pToTile,__instance, true)) //and routing
                        return true;

                    if (pFromUnit.canAttackUnitOrCity(pFromTile, pToTile, null) && !pFromUnit.isFatigued() && !pFromUnit.isMarch()) //kite condition: has moves left and hitting
                    {
                        var txt2 = g.HelpText.getGenderedEffectUnitName(g.infos().effectUnit(attackerEffect), pFromUnit.getGender());
                        builder.AddTEXT(txt2);
                        __result = builder;
                        special = true; //Special!
                    }
                }
                
                
                //if pushing from afar
                if (pFromUnit.hasPush(pToTile) && pFromUnit.getPushTile(pFromUnit, pFromTile, pToTile) == pToTile)
                    special = true; //Special! Let's ignore "push" that doesn't move the unit
                return !special;
            }

          
            private static bool isSkirmishing(Unit pFromUnit, Tile pFromTile, Unit target, out EffectUnitType why)
            {
                var pToTile = target.tile();
                if (debug)
                   Debug.Log(pFromUnit + " can cause skirmish of " + target + "?");
                int specialMoveCodeDefender = getSpecialMove(target.getEffectUnits(), target.game().infos(), out why);
  
                bool result = isSkirmisher == specialMoveCodeDefender  //has this type of special move
                        && target.attackDamagePreview(pFromUnit, pFromTile, pFromUnit.player()) < target.getHP() // and not dead
                        && pFromUnit.canAttackUnitOrCity(pFromTile, pToTile, null) && pFromTile.isTileAdjacent(pToTile) 
                        && !pToTile.hasCity() && !(pToTile.hasImprovementFinished() && (target.improvementDefenseModifier(pToTile.getImprovement(), pToTile) > 0 ));   //skirmish condition: getting hit, adj, and not special tile
               
                if (debug && result)
                {
                    Debug.Log("pushed--------------");
                    Debug.Log(target.attackDamagePreview(pFromUnit, pFromTile, pFromUnit.player()) < target.getHP());
                    Debug.Log(pFromUnit.canAttackUnitOrCity(pFromTile, pToTile, null) && pFromTile.isTileAdjacent(pToTile));
                    Debug.Log(!pToTile.hasCity());
                    if (pToTile.getImprovement() != ImprovementType.NONE)
                        Debug.Log(pToTile.getImprovement() + " is there; active defense is " + target.improvementDefenseModifier(pToTile.getImprovement(), pToTile));
                }
                return result;
            }

            [HarmonyPatch(nameof(Unit.attackUnitOrCity), new Type[] { typeof(Tile), typeof(Player) })]
            ///for Kite, skirmisher and friendly fire; defines what can be attacked, and what effect pop up texts to display
            /// also for Charge, so to handle the unit jumping and defender scattering
            /// should update attack damage preview and/or attack effect preview to match logic every time this changes; consider refactor
            static void Prefix(ref Unit __instance, ref Tile pToTile, Player pActingPlayer, out bool __state)
            {
                Tile pFromTile = __instance.tile();
                List<TileText> azTileTexts = new List<TileText>();
                if (debug)
                    Debug.Log("debug trace: entering harmony's AttackUnitorCity prefix");
             
                List<int> aiAdditionalDefendingUnits = new List<int>();
                List<Unit.AttackOutcome> outcomes = new List<Unit.AttackOutcome>();
                int cityHP = -1;
                __state = false;
                Game g = __instance.game();
                Infos info = g.infos();
                var pToUnit = pToTile.defendingUnit(); 
                if (pToUnit == null || pToUnit == __instance) //something went wrong. abort, abort!
                    return; 
                if (isKite == getSpecialMove(__instance.getEffectUnits(), info, out EffectUnitType hitNRunEff) && !__instance.isFatigued() && !__instance.isMarch())
                {
                    if (debug)
                        Debug.Log("debug trace: entering harmony's AttackUnitorCity kite");
                    if (pToUnit.attackDamagePreview(__instance, pFromTile, __instance.player()) >= pToUnit.getHP() && //dead
                       __instance.canAdvanceAfterAttack(pFromTile, pToTile, pToUnit, true)) //and routing
                    {
                        //what a specific situation! Routing, so not running.
                    }
                    else
                    {
                        
                        SendTileTextAll(g.HelpText.TEXT(g.infos().effectUnit(hitNRunEff).mName), pFromTile.getID(), g);       
                    }               
                }
                if (isCharge(__instance, out Tile impactFrom, pFromTile, pToTile))
                {
                    if (debug)
                        Debug.Log("debug trace: entering harmony's AttackUnitorCity charge");
                    List<TileText> azTileTexts2 = new List<TileText>();
                    g.addTileTextAllPlayers(ref azTileTexts2, impactFrom.getID(), () => "Charge");
                   
                    UnitMoveAction unitAction = new UnitMoveAction
                    {
                        miUnitID = __instance.getID(),
                        meType = UnitActionType.MOVE,
                        maiTiles = new List<int>
                        {
                            pFromTile.getID(),
                            impactFrom.getID(),  
                            pToTile.getID(),
                        },
                        maTileTexts = azTileTexts2,
                        meActingPlayer = pActingPlayer?.getPlayer() ?? PlayerType.NONE
                    };
                    g.sendUnitMove(unitAction);
                   
                    setTileID(__instance, impactFrom.getID());
                    __state = true;
                }
                if (isSkirmishing(__instance, pFromTile, pToUnit, out EffectUnitType why))
                {
                    if (debug)
                        Debug.Log("debug trace: entering harmony's AttackUnitorCity skirmishing");
                    
                   retreat = why;
                }
                //look for friendly fire
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
                            Tile pLoopTile = g.tile(iLoopTile);
                           
                            if (__instance.canDamageUnitOrCity(pLoopTile, true)) //if this tile can be damaged, then original code will handle it
                                continue;
                            if (pLoopTile == pFromTile) //disable friendly damage on self
                                continue; 
                            int percent = __instance.attackPercent(eLoopAttack) / (__instance.info().mbMelee? 3: 1);
                            Unit pLoopDefendingUnit = pLoopTile.defendingUnit();
                            if (pLoopDefendingUnit == null || percent < 1)
                                continue;
                            if (debug)
                                Debug.Log("debug trace: doing harmony's AttackUnitorCity friendly fire's loop");
                            int dmg = 0;
                            if (pLoopTile.hasCity())
                            {
                                City city = pLoopTile.city();
                                cityHP = city.getHP();
                                dmg = __instance.attackCityDamage(pFromTile, city, percent);
                                if (dmg < 1)
                                    continue;
                                city.changeDamage(dmg);                        
                                outcomes.Add(city.getHP() == 0 ? Unit.AttackOutcome.CAPTURED : Unit.AttackOutcome.CITY);
                                city.processYield(info.Globals.DISCONTENT_YIELD, info.Globals.CITY_ATTACKED_DISCONTENT);                  
                            }
                            else
                            {
                                aiAdditionalDefendingUnits.Add(pLoopDefendingUnit.getID());
                                dmg = __instance.attackUnitDamage(pFromTile, pLoopDefendingUnit, false, percent);
                                if (dmg < 1)
                                    continue;
                                pLoopDefendingUnit.changeDamage(dmg, true); //friendly fire never kills
                                outcomes.Add(Unit.AttackOutcome.NORMAL);
                             }

                            g.addTileTextAllPlayers(ref azTileTexts, pLoopTile.getID(), () => "-" + dmg + " HP");
                        }
                    }
                }
                if (azTileTexts.Count != 0)
                {
                    if (debug) { 
                        Debug.Log("debug trace: entering harmony's AttackUnitorCity printing " + azTileTexts.Count + " texts");
                        for (int i = 0; i < azTileTexts.Count; i++)
                            Debug.Log(azTileTexts[i].mzText + " for player " + azTileTexts[i].mePlayer);
                    }
                    g.sendUnitBattleAction(__instance, null, pFromTile, pToTile, pToTile, Unit.AttackOutcome.NORMAL, azTileTexts, pActingPlayer?.getPlayer() ?? PlayerType.NONE, cityHP > 0, cityHP, aiAdditionalDefendingUnits, outcomes);
                }
            }
          
            private static void SendTileTextAll(string v, int tileID, Game g)
            {              
                for (PlayerType playerType = (PlayerType)0; playerType < g.getNumPlayers(); playerType++)
                {
                  
                    g.sendTileText(new TileText(v, tileID, playerType));
                }
            }
            
            [HarmonyPatch(nameof(Unit.attackUnitOrCity), new Type[] { typeof(Tile), typeof(Player) })]
            /// for Charge, __state represents mid-charging action. We want to set tile as the last thing
            static void Postfix(ref Unit __instance, Tile pToTile, Player pActingPlayer, bool __state)
            {
                if (!__state)
                    return;
     
                UnitMoveAction unitAction = new UnitMoveAction
                {
                    miUnitID = __instance.getID(),
                    meType = UnitActionType.MOVE,
                    maiTiles = new List<int>
                                    {
                                        __instance.getTileID(),                           
                                        pToTile.getID()
                                    },
                    meActingPlayer = pActingPlayer?.getPlayer() ?? PlayerType.NONE
                };
                __instance.game().sendUnitAction(unitAction, true);
                if (pToTile.defendingUnit() != null) //could be dead already
                    pToTile.defendingUnit().bounce();

                setTileID(__instance, pToTile.getID());
            }
            
            [HarmonyPatch(nameof(Unit.canActMove), new Type[] { typeof(Player), typeof(int), typeof(bool) })]
            // Player pActingPlayer, int iMoves = 1, bool bAssumeMarch = false)
            ///for Kite
            static bool Prefix(ref Unit __instance, ref bool __result)
            {

                if (__instance.getCooldown() != __instance.game().infos().Globals.ATTACK_COOLDOWN) //if didn't attack, normal
                                                                                                  
                    return true;
                if (getSpecialMove(__instance.getEffectUnits(), __instance.game().infos(), out _) != isKite) //if not kite, normal. //may want to catch the out and display text
                {
                    return true;
                }
                if (__instance.isFatigued() || __instance.isMarch()) //fatigued or marching; normal
                {
                    return true;
                }

                __result = true; //can move
                return false;
            }

            [HarmonyReversePatch]
            [HarmonyPatch(("setTileID"), new Type[] {typeof(int), typeof(bool), typeof(bool)})]
            public static void setTileID(Unit unit, int iNewValue, bool bFinalMove = true, bool bLastStep = true)
            {
                throw new NotImplementedException("It's a stub");
            }

        }

        [HarmonyPatch(typeof(Unit.UnitAI))]
        public class PatchAI
        {
            [HarmonyPatch(nameof(Unit.UnitAI.attackValue))]
            //public virtual int attackValue(Tile pFromTile, Tile pTargetTile, bool bCheckOtherUnits, int iExtraModifier, out bool bCivilian, out int iPushTileID, out bool bStun)
            //this is a targetting algo; modify to factoring friendly fire
            static void Postfix(ref int __result, ref Unit ___unit, Tile pFromTile, Tile pTargetTile)
            {
                if (__result == 0) //not a target. Shortcircuit
                    return;

                //factor in some more counterattack avoidance
                __result -= 50 * ___unit.getCounterAttackDamage(pTargetTile.defendingUnit(), pTargetTile);

                Game g = ___unit.game();
                for (AttackType eLoopAttack = 0; eLoopAttack < g.infos().attacksNum(); eLoopAttack++)
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
                            
                            Tile pAttackTile = g.tile(iAttackTile);
                            
                            Unit pUnit = pAttackTile.defendingUnit();
                            if (pUnit != null)
                            {
                                if (g.isHostileUnitUnit(___unit, pUnit)) //if hostile, original method handles it
                                    continue;
                                int iDamage = ___unit.attackUnitDamage(pFromTile, pUnit, false, iAttackPercent, -1, false);
                                if (g.areTeamsAllied(pUnit.getTeam(), ___unit.getTeam()) || pUnit.getTeam() == ___unit.getTeam()) //friendly!
                                {
                                    __result -= 150 * iDamage; //100% is default; we treat our soldiers' lives slightly above average
               
                                }
                                else
                                    __result = 50 * iDamage; //slight preference for causing collateral damage
                            }                 
                        }
                    } 
                }
                __result = Math.Max(0, __result); //negative should be handled same as zero...but just in case. zero means not a valid target (which is a stronger rejection than base method's floor of 1).
            }

            [HarmonyPatch(typeof(Unit.UnitAI), nameof(Unit.UnitAI.getAttackTiles), new Type[] { typeof(PathFinder), typeof(Tile), typeof(bool), typeof(List<int>) })]
            // public virtual void getAttackTiles(PathFinder pPathfinder, Tile pTargetTile, bool bTestUnits, List<int> aiAttackTiles)
            ///charge AI--did you know, melee can attack range 2?
            static bool Prefix(ref Unit ___unit, Tile pTargetTile, bool bTestUnits, List<int> aiAttackTiles)
            {
                try
                {
                    if (PatchUnitBehaviors.isCharge(___unit, out _))
                    {
                        int iNumValidTilesAtRange = 0;
                        using (var listScoped = CollectionCache.GetListScoped<int>())
                        {
                            pTargetTile.getTilesAtDistance(2, listScoped.Value);
                            foreach (int iLoopTile in listScoped.Value)
                            {
                                Tile pMoveTile = ___unit.game().tile(iLoopTile);

                                if (___unit.at(pMoveTile) || ___unit.canAct(___unit.player()))
                                {
                                    if (___unit.canTargetTile(pMoveTile, pTargetTile))
                                    {
                                        ++iNumValidTilesAtRange;
                                        if (___unit.canOccupyTile(pMoveTile, ___unit.getTeam(), bTestUnits, bTestUnits))
                                        {
                                            if (___unit.player().AI.isTileReachable(pMoveTile, ___unit.tile()))
                                            {
                                                aiAttackTiles.Add(iLoopTile);
                                                return false;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    return true;
                }
                catch (Exception)
                {
                    return false; //a literal catch all solution--the method causes some null pointer...somewhere
                }
            }

            [HarmonyPatch(typeof(Unit.UnitAI), "doRoleAction")]
            // protected virtual bool doAttackTargetRole(PathFinder pPathfinder, bool bSafe)
            ///kite AI aid--teach 'em how to say goodbye
            static void Prefix(ref Unit.UnitAI __instance, Unit ___unit, Game ___game, PathFinder pPathfinder)
            {
                if (PatchUnitBehaviors.getSpecialMove(___unit.getEffectUnits(), ___game.infos(), out _) == isKite && !___unit.isFatigued())
                {
                    //SHOOT! 
                    try
                    {
                        doAttackFromCurrentTile(__instance, false);
                    }
                    catch (Exception)
                    {
                        MohawkAssert.Assert(false, "failed to leave a parting shot before retreating");
                    }
                }
            }
            [HarmonyPatch(typeof(Unit.UnitAI), "doRoleAction")]
           // protected virtual bool doAttackTargetRole(PathFinder pPathfinder, bool bSafe)
            ///kite AI aid--teach 'em how to say goodbye
           static void Postfix(ref Unit.UnitAI __instance, Unit ___unit, ref Func<Tile, long> ___retreatValueDelegate,  Game ___game, PathFinder pPathfinder)
            {
              //RUN! 
                if (___retreatValueDelegate == null)
                    ___retreatValueDelegate = new Func<Tile, long>(__instance.retreatTileValue);
                
                if (PatchUnitBehaviors.getSpecialMove(___unit.getEffectUnits(), ___game.infos(), out _) == isKite && !___unit.isFatigued() && ___unit.getCooldown() == ___game.infos().Globals.ATTACK_COOLDOWN)
                doMoveToBestTile(__instance, pPathfinder, ___unit.getStepsToFatigue(), null, ___retreatValueDelegate); 
            }
         

            [HarmonyReversePatch]
            [HarmonyPatch("doMoveToBestTile")]
            public static bool doMoveToBestTile(Unit.UnitAI ai, PathFinder pPathfinder, int iMaxSteps, Predicate<Tile> tileValid, Func<Tile, long> tileValue)
            {
                throw new NotImplementedException("It's a stub");
            }
            [HarmonyReversePatch]
            [HarmonyPatch("doAttackFromCurrentTile")]
            public static bool doAttackFromCurrentTile(Unit.UnitAI ai, bool bKillOnly)
            {
                throw new NotImplementedException("It's a stub");
            }

            [HarmonyPatch(nameof(Unit.UnitAI.movePriorityCompare))]
            // public virtual int movePriorityCompare(Unit pOther)
            ///friendly fire AI--move the AoE first, so we bombard then charge
            static bool Prefix(ref int __result, Unit.UnitAI __instance, Unit ___unit, Game ___game, Unit pOther)
            {
                //begin some copy paste of key logic
                bool flag = __instance.isInTheWay();
                if (flag != pOther.AI.isInTheWay())
                {
                    __result = (flag) ? -1 : 1;
                    return false;
                }

                if (__instance.SubRole == Unit.SubRoleType.URGENT != (pOther.AI.SubRole == Unit.SubRoleType.URGENT))
                {
                   __result = (__instance.SubRole != Unit.SubRoleType.URGENT) ? 1 : (-1);
                    return false;
                }
                //end of key logic from original to preserve

                int myAoE = -1;
                int theirAoE = -1;
                for (AttackType eLoopAttack = 0; eLoopAttack < ___game.infos().attacksNum(); eLoopAttack++) //rough estimate of the best AoE attack's impact
                {
                    int estimate = ___unit.attackValue(eLoopAttack) * ___unit.attackPercent(eLoopAttack) * ((int) eLoopAttack + 1); //attack radius * attack percent * attack shape; shape is usually larger as Attack Num increases
                    myAoE = Math.Max(myAoE, estimate);
                    estimate = pOther.attackValue(eLoopAttack) * pOther.attackPercent(eLoopAttack) * ((int)eLoopAttack + 1);
                    theirAoE = Math.Max(theirAoE, estimate);
                }
                if (myAoE != theirAoE)
                {
                    __result = myAoE > theirAoE ? -1 : 1;
                    return false;
                }
                    

                return true;
            }
        } //PatchAI
        [HarmonyPatch(typeof(Infos))]
        public class PatchInfos
        {
            [HarmonyPatch(nameof(Infos.effectUnit))]
            ///NOTE this is hacky, may not always work...
            static bool Prefix(ref Infos __instance, ref InfoEffectUnit __result, ref List<InfoEffectUnit> ___maEffectUnits, ref EffectUnitType eIndex)
            {
                //NOTE this is used when looking for an effect to explain hasPush but none was found; we can explain it on Skirmisher. This method could have side effects

                if (eIndex < 0)
                {
                    if (retreat != EffectUnitType.NONE) //so we queued up a reason for the retreat in prefix of attackUnitOrCity; 
                    {
                        __result = __instance.effectUnit(retreat);
                        retreat = EffectUnitType.NONE; //do we need to clear it out? Multithread considerations...
                        return false;
                    }
                    else
                        foreach (var effect in ___maEffectUnits)
                        {
                            if (effect.mzType.Equals("EFFECTUNIT_BLANK"))
                            {
                                __result = effect;
                                return false;
                            }
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
                    int moveSpecialCode = infos.effectUnit(eEffectUnit).maiAttackValue[(int)infos.getType<AttackType>("MOVE_SPECIAL")];
                    int chargeCode = infos.effectUnit(eEffectUnit).maiAttackValue[(int)infos.getType<AttackType>("CHARGE")];
                    if (moveSpecialCode == isSkirmisher)
                    {
                        builder.AddTEXT("TEXT_HELP_RETREAT_SHORT");
                    }
                    else if (moveSpecialCode == isKite)
                    {
                        builder.AddTEXT("TEXT_HELP_KITE_SHORT");
                    }
                    if (chargeCode != 0)
                    {
                        builder.AddTEXT("TEXT_HELP_CHARGE_SHORT");
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
            static Tile phantom;
            static int phantomDelay = 0;
            [HarmonyPatch(typeof(ClientInput), nameof(ClientInput.moveTo))]
            // public virtual void moveTo(Unit pUnit, Tile ownTile)
            ///outside of gamecore, so be very careful here. Client level g logic control in base g...tsk tsk. Changing it to make Kiting work
            static bool Prefix(ref IApplication ___APP, Unit pUnit, Tile pTile)
            {
                ClientManager ClientMgr = ___APP.GetClientManager();
                if (pUnit.getCooldown() != ClientMgr?.Infos.Globals.ATTACK_COOLDOWN)
                    return true;
            
                if (PatchUnitBehaviors.getSpecialMove(pUnit.getEffectUnits(), ClientMgr?.Infos, out _) != isKite) //if not kite, normal.
                {
                    return true; 
                }
                if (pUnit.isFatigued() || pUnit.isMarch()) //fatigued; normal
                {
                    return true; 
                }
               
                ClientMgr.sendMoveUnit(pUnit, pTile, false, false, ClientMgr.Selection.getSelectedUnitWaypoint());
                ClientMgr.Selection.setSelectedUnitWaypoint(null);
                return false;
            }

            [HarmonyPatch(typeof(ClientUI), nameof(ClientUI.updateUnitAttackPreviewSelection))]
            // public virtual void updateUnitAttackPreviewSelection()
            /// first half of patching preview by setting the location of the phantom tile to be injected into the method later
            static void Prefix(ref IApplication ___APP)
            {
                ClientManager ClientMgr = ___APP.GetClientManager();
                var pSelectedUnit = ClientMgr.Selection.getSelectedUnit();
                Tile pMouseoverTile = ClientMgr.Selection.getAttackPreviewTile();

                if (PatchUnitBehaviors.isCharge(pSelectedUnit, out Tile newFrom, pSelectedUnit.tile(), pMouseoverTile))
                {
                    phantom = newFrom;
                    phantomDelay = 2; //this magic number depends on the IL code and where the assignment we want to change is happening within the method
                   
                }
            }

            [HarmonyPatch(typeof(Unit), nameof(Unit.tile))]
            // public virtual void updateUnitAttackPreviewSelection()
            /// second half of patching preview by setting the location of the phantom tile to be injected into the method later
            static void Postfix(ref Tile __result)
            {
                if (phantom == null)
                    return;
                else if (phantomDelay > 1)
                {
                    phantomDelay--;
                    return;
                }
              //  MohawkAssert.Assert(false, "phantom assigned: instead of " + __result.getX() + ", " + __result.getY() + ", we pretend the tile is at (" + phantom.getX() + ", " + phantom.getY() );
                __result = phantom;
                phantomDelay = 0;
                phantom = null;
            }

            [HarmonyPatch(typeof(ClientUI), nameof(ClientUI.updateCityWidget))]
            //  public virtual void updateCityWidget(City city, bool bDamagePreviewOnly = false)
            ///outside of gamecore, so be very careful here. Client level g logic control in base g...tsk tsk. Directly manipulating the city widget to display friendly fire
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

                if (city == null)
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
