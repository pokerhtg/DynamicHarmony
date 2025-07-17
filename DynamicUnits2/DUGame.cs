using Mohawk.SystemCore;
using System;
using System.Collections.Generic;
using System.IO;
using TenCrowns.ClientCore;
using TenCrowns.GameCore;
using TenCrowns.GameCore.Text;
using UnityEngine;
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
    
        public override bool isHostile(TeamType eTeam1, TribeType eTribe1, TeamType eTeam2, TribeType eTribe2)
        {
            //a diplomlatic tribe can never be hostile toward a nondiplomatic one and vice versa
            if (eTribe1 != TribeType.NONE && eTribe2 != TribeType.NONE && infos().tribe(eTribe1).mbDiplomacy != infos().tribe(eTribe2).mbDiplomacy)
                { return false; }

            return infos().diplomacy(getDiplomacy(eTeam1, eTribe1, eTeam2, eTribe2)).mbHostile;
        }
        //END BLOCK of code to allow tribes to raid

        protected override int getYieldDemand(YieldType eYield)
        {
            if (eYield == infos().Globals.ORDERS_YIELD)
                return base.getYieldDemand(eYield);
            
            int amplitude = infos().yield(eYield).miDemand; //repurpose this to be amplitude
            float period = infos().yield(eYield).miDemandTurns; //repurpose this field to mean the period of demand
            
            if (period < 1 || amplitude < 1)
                return 0;

            float seed = miMaxLatitude / 100f + miMinLatitude/10f; // e.g. 3.65, definitely less than 6 and strictly less than 9.1, and bigger than 0
            float elasticityOfDemand = 0.65f + 0.01f * seed;  //$15 at 70, $20 at 110, $4 at 0, for an elasticity of 0.7. 
            
            int shift = (int)(eYield - 11) * (int) seed; //11 is magic number for Food; shift by that to get rid of food's shift. Not important, but having a food price anchor seems nice
            
            int export = (int)((infos().yield(eYield).miPrice * Constants.YIELDS_MULTIPLIER * (1.2 + (0.01 + 0.001 * seed) * getTurn()) - getYieldBuyPrice(eYield)) * elasticityOfDemand);//net export from all of known OW, based on price below initial price, and elasticity of demand. World inflation is at 1% a year
            var curve = (amplitude * Math.Sin(2 * Math.PI / period * (getTurn() + shift))); //demand sine curve

            if (export > 30 && curve < 0)
                curve *= -1;
            else if (export < -30 && curve > 0)
                curve *= -1;
            else if (export > -5 && export < 5) //basically no export 
                curve *= 2; //let the curve be exciting and drive some price movement
            if (curve < 0 && getTurn() < 30)
                curve *= -0.5f; //no imports for the first 30 turns, give a little early game inflation
            return export + (int)curve;
        }
     

        public override int getVPToWin() //always return a fakish result; this is for ClientUI to decide when to add unmet players to the buildPlayerListText(StringBuilder output)
        {
            return getVPRange().lower;
        }
        public override int getVPToWinByVictory(VictoryType eVictory, TeamType eTeam)
        {
            InfoVictory infoVictory = infos().victory(eVictory);

            int num = (getVPToWin(true) * infoVictory.miPercentVP + 50) / 100;
            int topRange = 1000; //sufficiently large
            if (infoVictory.miOpponentMaxPointPercent != 0) //Double victory; let's use half of min range instead of half of exact VP to win
            {
                (num, topRange) = getVPRange();
                num++;
                num /= 2;
                for (TeamType teamType = (TeamType)0; teamType < getNumTeams(); teamType++)
                {
                    if (isTeamAlive(teamType) && eTeam != teamType)
                    {
                        num = Math.Max(num, (countTeamVPs(teamType) * infoVictory.miOpponentMaxPointPercent + 50) / 100);
                    }
                }
            }
            return Math.Min(num, topRange);
        }
        //begin new methods
        public int getVPToWin (bool precise)
        {
            (int low, int high) = getVPRange();
            if (!precise)
                return low;
            else
            {
                var real = low + (base.getVPToWin() + miMaxLatitude/2 + miMinLatitude/3) % (high - low + 1); //a deterministic random number
                                                                                                             //   Debug.Log("VPToWin: " + real);
                return real;
            }
        }
        public (int lower, int upper) getVPRange()
        {
            int iPointsNeeded = base.getVPToWin();
            int lower = iPointsNeeded * 85 / 100; //85% of the points needed to win
            lower = lower / 5 * 5; // round down to nearest 5
            int upper = lower + iPointsNeeded / 4;
            upper = upper / 5 * 5; // round down to nearest 5
            
            return (lower, upper);
        }
    }
}