﻿using Mohawk.SystemCore;
using System;
using System.Collections.Generic;
using TenCrowns.ClientCore;
using TenCrowns.GameCore;
using TenCrowns.GameCore.Text;
using static TenCrowns.GameCore.Text.TextExtensions;

namespace DynamicUnits
{
    internal class DUGame : Game
    {
        public DUGame(ModSettings pModSettings, IApplication pApp, bool bShowGame) : base(pModSettings, pApp, bShowGame)
        {

        }
        protected override void postStart()
        {
            base.postStart();
            foreach (InfoHeight h in infos().heights())
            {
                if (h.mbImpassable && h.miMovementCost > 1)
                {
                    //if you have a movement cost, you aren't actually inpassable
                    h.mbImpassable = false;
                }
            }
            foreach (Tile t in allTiles()) {
                if (t.hasOwner() && t.hasImprovement()) {
                    if (t.improvement().mbTribe)
                    {
                        t.clearImprovement();
                    }  
                }
            }
        }
        public override bool initFromMapScript(GameParameters pGameParams, MapBuilder pMapBuilder)
        {

            foreach (InfoHeight h in infos().heights())
            {
                if (h.miMovementCost > 14)
                {
                    //if your movement cost is high, treat it like inpassable for map gen
                    h.mbImpassable = true;
                }
            }
            return base.initFromMapScript(pGameParams, pMapBuilder);
        }

        public override TextVariable getTeamNameVariable(TeamType eTeam)
        {
            using (new UnityProfileScope("Game.getTeamNameVariable"))
            {
                var nations = new List<TextVariable>();
                for (PlayerType eLoopPlayer = 0; eLoopPlayer < getNumPlayers(); eLoopPlayer++)
                {
                    Player pLoopPlayer = player(eLoopPlayer);

                    if (pLoopPlayer.getTeam() == eTeam)
                    {
                        nations.Add(HelpText.buildNationLinkVariable(pLoopPlayer.getNation(), eLoopPlayer));
                    }
                }
                switch (nations.Count)
                {
                    case 1:
                        return TEXTVAR("TEXT_GAME_TEAM1", textManager(), nations[0]);
                    case 2:
                        return TEXTVAR("TEXT_GAME_TEAM2", textManager(), nations[0], nations[1]);
                    case 3:
                        return TEXTVAR("TEXT_GAME_TEAM3", textManager(), nations[0], nations[1], nations[2]);
                    case 0:
                    
                    default:
                        return base.getTeamNameVariable(eTeam);
                }
                        
            }
        }
        public override int getTeamWarScore(TeamType eIndex1, TeamType eIndex2)
        {

            int perception = ((DynamicUnitsPlayer)firstTeamPlayer(eIndex1)).desirePeace(firstTeamPlayer(eIndex2).getPlayer());
            var rawWarScore = mpCurrentData.maaiTeamWarScore[(int)eIndex1, (int)eIndex2];
            int perceiptionWeight = 3;
            int realityWeight = 2;
            //30 year war gives standard desire for peace. shorter wars aren't as conclusive so desire is closer to 0
            int conclusivePercent = (70 + getTeamConflictNumTurns(eIndex1, eIndex2));

            return (perceiptionWeight * perception + realityWeight * rawWarScore) / realityWeight * conclusivePercent / 100;
        }

        public override bool canAddTrait(TraitType eTrait, Character pCharacter, CharacterType eCharacter, bool bTestPrereqs = false, bool bNoFallback = true, bool bSetArchetype = false)
        {
            if (pCharacter == null)
                return false;
            if (infos().trait(eTrait).mbRemoveNonLeader && !pCharacter.isLeaderOrSuccessor())
                return false;
            return base.canAddTrait(eTrait, pCharacter, eCharacter, bTestPrereqs, bNoFallback, bSetArchetype);
        }

        protected override int getSellPrice(int iPrice, YieldType eYield, Player pPlayer)
        {
            int salePrice = Math.Max(1, getBuyPrice(iPrice) * infos().Globals.PRICE_SELL_PERCENT / 100);
            if (pPlayer.isNoSellPenaltyYieldUnlock(eYield))
                return base.getSellPrice(iPrice, eYield, pPlayer);

            if (pPlayer?.isNoSellPenaltyUnlock() ?? false)
            {
                return salePrice * 2;
            }
            else
            {
                return salePrice;
            }
        }
        //Tribal allies shall raid and not fight barbs!
        public override bool canTribeRaid(TribeType eTribe, TeamType eTeam)
        {
            //deep copy with one change
            if (eTribe != TribeType.NONE && eTribe != infos().Globals.RAIDERS_TRIBE)
            {
                if (!infos().tribe(eTribe).mbDiplomacy)
                {
                    return false;
                }
                if (!isTribeContact(eTribe, eTeam))
                {
                    return false;
                }
                if (tribeDiplomacy(eTribe, eTeam).mbPeace)
                {
                    return false;
                }
                if (hasTribeAlly(eTribe))
                {
                    //here's that change!
          //          return false;
                }
            }
            return true;
        }
        public override bool isGameOption(GameOptionType eIndex)
        {
            if (eIndex == infos().Globals.GAMEOPTION_PLAY_TO_WIN)
                return true; //always play to win!
            else return base.isGameOption(eIndex);
        }
        public override bool isHostile(TeamType eTeam1, TribeType eTribe1, TeamType eTeam2, TribeType eTribe2)
        {
            //a diplomlatic tribe can never be hostile toward a nondiplomatic one and vice versa
            if (eTribe1 != TribeType.NONE && eTribe2 != TribeType.NONE && infos().tribe(eTribe1).mbDiplomacy != infos().tribe(eTribe2).mbDiplomacy)
                { return false; }

            return infos().diplomacy(getDiplomacy(eTeam1, eTribe1, eTeam2, eTribe2)).mbHostile;
        }
        

        //END BLOCK of code to allow tribes to raid

    }
}