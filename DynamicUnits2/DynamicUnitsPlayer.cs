﻿using Mohawk.SystemCore;
using System;
using System.Collections.Generic;
using System.Text;
using TenCrowns.GameCore;
using TenCrowns.GameCore.Text;
using UnityEngine;

namespace DynamicUnits
{
    internal class DynamicUnitsPlayer : Player
    {
        private int offset = 0;
        static Dictionary<(int, TechType), (int, List<int>)> techCostCache;

        public override void setConvertedLegitimacy(bool bNewValue)
        {
            if (!bNewValue) //mid process turn is when this happens
                convertExtraOrdersToCivics();
            base.processTurn();

        }

        private void convertExtraOrdersToCivics()
        {
            int iExtra = (getYieldStockpileWhole(infos().Globals.ORDERS_YIELD) - getMaxOrders());
            if (iExtra < 1)
                return;

            changeYieldStockpileWhole(infos().Globals.CIVICS_YIELD, iExtra);
            changeYieldStockpileWhole(infos().Globals.ORDERS_YIELD, -(iExtra));
            using (new TextManager.LanguageSwitchScoped(TextManager, this))
            using (var scope = CollectionCache.GetStringBuilderScoped())
            {
                Mohawk.SystemCore.StringBuilder logDataSB = scope.Value;
                TextManager.TEXT(logDataSB, "TEXT_GAME_PLAYER_YIELD_ORDERS_SOLD_FOR_YIELD",
                    HelpText.buildYieldValueIconLinkVariable(infos().Globals.ORDERS_YIELD, iExtra),
                    HelpText.buildYieldValueIconLinkVariable(infos().Globals.CIVICS_YIELD, iExtra, false, false, 1));
                pushLogData(() => logDataSB.ProfiledToString(), GameLogType.MAX_ORDERS);
            }
        }

        public override bool canGiftCity(City pCity, PlayerType eToPlayer, bool bTestEnabled = true)
        {
            if (base.canGiftCity(pCity, eToPlayer, bTestEnabled))
            {
                Tile cityCenter = pCity.tile();
                if (game().player(eToPlayer).findClosestCity(cityCenter).tile().distanceTile(cityCenter) < 2 * infos().Globals.MIN_CITY_SITE_DISTANCE)
                    return true;

            }
            return false;
        }
        public override int getTechCostWhole(TechType eTech)
        {
            if (capitalCity() == null)
                return base.getTechCostWhole(eTech);
            else
                return diffusedTechCost(eTech, out _);
        }
        protected override void doEventTriggers()
        {
            //add a lot of playerevent triggers to be fairer to AI when event level is high
            base.doEventTriggers();
            if (game().randomNext(game().eventLevel().miTurns) == 0)
                doEventPlayer();

            if (game().eventLevel().miPercent > 90)
                doEventPlayer();

        }

        public int diffusedTechCost(TechType eTech, out List<int> why)
        {
            if (techCostCache == null)
                techCostCache = new Dictionary<(int, TechType), (int, List<int>)>();

            var key = (game().getTurn(), eTech);

            if (techCostCache.TryGetValue(key, out (int, List<int>) cache))
            {
                why = cache.Item2;
                return cache.Item1;
            }

            int cost = infos().utils().modify(infos().tech(eTech).miCost, infos().Globals.TECH_GLOBAL_MODIFIER);

            const int MAXDISCOUNT = 95; //won't end up this high, thanks to integer division, plus the spooky phantom 1 tile away

            int distanceFactor = 0;
            int knownNations = 0;
            int eligibleNations = 1;//a phantom! 
            int totalDistFactor = 1; //phantom lives 101-200 hexes away

            why = new List<int>();
            var capital = capitalCity().tile();

            for (PlayerType eLoopPlayer = 0; eLoopPlayer < game().getNumPlayers(); ++eLoopPlayer)
            {
                Player p = game().player(eLoopPlayer);
                if (!p.isAlive() || p == this)
                    continue;

                var pCapital = p.capitalCity();

                if (pCapital == null)
                    continue;

                int dist = capital.distanceTile(pCapital.tile());

                if (p.isTechAcquired(eTech))
                {
                    knownNations++;
                    distanceFactor += 200 / dist;
                }

                if (p.isTechValid(eTech))
                {
                    eligibleNations++;
                    totalDistFactor += 200 / dist;
                }
            }

            int discount = 35 + MAXDISCOUNT * knownNations * distanceFactor / totalDistFactor / eligibleNations; //standard discount is 35%, compensated in globalsxml's tech cost, to make people feel better about getting a discount most of the time

            int difficulty = (int)getDifficulty();
            if (knownNations == 0)
                discount -= 20; //no one else knows? 20% more expensive!
            else discount += (10 - difficulty) * 2; //someone knows? you get a discount based on your difficulty
            discount -= (int)Math.Pow(difficulty, 1.8); //playing on harder difficulties? research gets harder (42% on Great)

            if (eligibleNations == 1) //unique tech just for you
                discount = (MAXDISCOUNT + discount) / 2;
            if (infos().tech(eTech).mbTrash)
                discount += cost / 30; //discount by 1 extra percent for every 30 cost of science --so late game bonus cards are notably cheaper

            discount = Math.Min(MAXDISCOUNT, discount);
            why.Add(cost);
            why.Add(knownNations);
            why.Add(eligibleNations - 1);     //let's not display the phantom to player...may be too spooky
            why.Add(discount);

            cost = infos().utils().modify(cost, -discount, true);
            techCostCache.Add(key, (cost, why));

            return cost;
        }

        //how this AI perceives their warscore is, biased from reality
        public int desirePeace(PlayerType other)
        {
            DynamicUnitsPlayer pOtherPlayer = (DynamicUnitsPlayer)game().player(other);
            if (pOtherPlayer == null)
                return 0;
            var otherTeam = pOtherPlayer.getTeam();
            int desireForPeace = 0;
            if (isHuman())
                return desireForPeace;   //humans don't perceive with algo
            if (game().hasHumanAlly(this.getTeam()))
                return -200;   //human's AI buddies don't end wars

            //begin abridged AI's usual truce offering calculations 
            if (game().isPlayToWinVs(other))
            {
                desireForPeace -= 200;
            }
            desireForPeace += pOtherPlayer.playerOpinionOfUs(other).miTrucePercent;

            desireForPeace += 5 * (game().getTeamConflictNumTurns(getTeam(), otherTeam) - game().opponentLevel().miEndWarMinTurns);

            desireForPeace = infos().utils().modify(desireForPeace, infos().proximity(pOtherPlayer.calculateProximityPlayer(other)).miTruceModifier, true);
            desireForPeace = infos().utils().modify(desireForPeace, infos().power(pOtherPlayer.calculatePowerOf(other)).miTruceModifier, true);

            Character pLeader = leader();

            if (pLeader != null)
            {
                foreach (TraitType eLoopTrait in pLeader.getTraits())
                {
                    desireForPeace = infos().utils().modify(desireForPeace, infos().trait(eLoopTrait).miTruceModifier);
                }
            }
            //end truce calc

            //lower rank means stronger; each rank we ae stronger gives us 20% more confidence
            int weight = 20;
            desireForPeace += weight * (calculateStrengthRank() - pOtherPlayer.calculateStrengthRank());
            if (!game().isPlayToWinAny())
            {
                desireForPeace /= 2;
            }

            //fear if they are stronger, or smarter
            var ourPower = calculateTotalStrength();
            int DAMPER = 10;

            var theirPower = pOtherPlayer.calculateTotalStrength();

            desireForPeace += 100 * weight * (theirPower - ourPower) / (DAMPER + ourPower) / 100;

            var ourTech = getTotalTechProgress();
            var theirTech = pOtherPlayer.getTotalTechProgress();
            desireForPeace += 100 * weight * (theirTech - ourTech) / (DAMPER + ourTech) / 100;

            if (other == mePlayer)
                offset = desireForPeace; //this makes sure opinion of others remain anchored
            else
                desireForPeace -= offset;

            int diplomaticSupport = countTeamDiplomacy(infos().Globals.PEACE_DIPLOMACY) - countTeamWars()
               - (pOtherPlayer.countTeamDiplomacy(infos().Globals.PEACE_DIPLOMACY) - 2 * pOtherPlayer.countTeamWars());

            desireForPeace -= weight * diplomaticSupport;

            // Debug.Log(this.mePlayer+ "'s raw peace perceiption of " + other + " is " + desireForPeace);
            desireForPeace = infos().utils().range(desireForPeace, -200, 200);
            return desireForPeace;
        }
        public override int calculateTotalStrength()
        {
            int strength = base.calculateTotalStrength();
            for (int i = 0; i < getNumUnits(); ++i)
            {
                Unit pUnit = unitAt(i);

                if (pUnit != null)
                {
                    if (pUnit.canDamage())
                    {
                        strength -= 10; //in DU, having more unit isn't as inherently good as it is in base
                        strength += pUnit.getLevel() * 5; //in DU, level is especially important because of HP boost and promotion buff
                        strength += pUnit.range() * pUnit.range(); //in DU, long ranged units often have powers beyond raw strength--eg AoE
                    }
                }
            }

            return strength;
        }
        public override int countTeamWars()
        {
            int iCount = 0;

            for (TeamType eLoopTeam = 0; eLoopTeam < game().getNumTeams(); eLoopTeam++)
            {
                if (eLoopTeam != getTeam())
                {
                    if (game().isTeamAlive(eLoopTeam))
                    {
                        var diplo = game().teamDiplomacy(eLoopTeam, getTeam());

                        if (diplo != null && diplo.mbHostile)
                        {
                            iCount++;
                        }
                    }
                }
            }
            return iCount;
        }

    }

}