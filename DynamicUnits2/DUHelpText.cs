using Mohawk.SystemCore;
using Mohawk.UIInterfaces;
using System;
using static TenCrowns.GameCore.Text.TextExtensions;
using TenCrowns.ClientCore;
using TenCrowns.GameCore;
using TenCrowns.GameCore.Text;
using System.Collections.Generic;

namespace DynamicUnits
{
    internal class DUHelpText : HelpText
    {
        public DUHelpText(TextManager txtMgr) : base(txtMgr)
        {
        }
        public override TextBuilder buildUnitTypeHelp(TextBuilder builder, UnitType eUnit, City pCity, Player pPlayer, TribeType eTribe, Game pGame, Player pActivePlayer, bool bName = true, bool bCosts = true, bool bStats = true, bool bDetails = true)
        {
            var result = base.buildUnitTypeHelp(builder, eUnit, pCity, pPlayer, eTribe, pGame, pActivePlayer, bName, bCosts, bStats, bDetails);
            var Helper = (DynamicUnitsInfoHelper)infos().Helpers;
            if (mInfos.unit(eUnit).mbAmphibious)
            {
                builder.AddTEXT("TEXT_HELPTEXT_AMPHIBIOUS");
            }
            if (Helper.isMountaineer(eUnit))
            {
                builder.AddTEXT("TEXT_HELPTEXT_MOUNTAIN_TRAVERSAL");
            }
            return result;
        }

        public override TextVariable buildVPLinkVariable(TeamType eTeam, Game pGame, bool bShowUnmet = true, bool bShowMax = true)
        {
            bool bShowPlayerName = (bShowUnmet || pGame.isTeamContact(pGame.manager().getActiveTeam(), eTeam));
            bool bUsOrAlly = eTeam == pGame.manager().getActiveTeam() || pGame.getTeamAlliance(eTeam) == pGame.manager().getActiveTeam();

            if (pGame.isVictoryEnabled(infos().Globals.POINTS_VICTORY) && bShowMax)
            {
                (int lower, int upper) = ((DUGame)pGame).getVPRange();
                if (bShowPlayerName)
                    {
                        int maxLength = (int)Math.Log10(upper) + 1;
                        ColorType eLinkColor = ColorType.NONE;
                        if (!bUsOrAlly)
                        {
                            if (pGame.countTeamVPs(eTeam) >= lower)
                            {
                                eLinkColor = pGame.infos().Globals.COLOR_NEGATIVE;
                            }
                            else if (pGame.countTeamVPs(eTeam) >= lower * 90 / 100)
                            {
                                eLinkColor = pGame.infos().Globals.COLOR_AVERAGE;
                            }
                        }
                        TextVariable vpVariable = buildLinkTextVariable(buildSlashText(TEXTVAR(pGame.countTeamVPs(eTeam).ToStringCached().PadLeft(maxLength)), TEXTVAR(lower+"-"+upper)), ItemType.HELP_LINK, nameof(LinkType.HELP_VICTORY), ((int)eTeam).ToStringCached(), false.ToStringCached(), eLinkColor: eLinkColor);
                        return vpVariable;
                    }
                    else
                    {
                        return buildSlashText(TEXTVAR("?"), TEXTVAR(lower + "-" + upper));
                    }
                }
            else return base.buildVPLinkVariable(eTeam, pGame, bShowUnmet, bShowMax);
        }
        public override TextBuilder buildTechHelp(TextBuilder builder, TechType eTech, Player pPlayer, ClientManager pManager, bool bHelp = false)
        {
            // UnityEngine.Debug.Log("diffusing?");
            var output = base.buildTechHelp(builder, eTech, pPlayer, pManager, bHelp);
            //   UnityEngine.Debug.Log("diffusing");
            try
            {
                ((DynamicUnitsPlayer)pPlayer).diffusedTechCost(eTech, out var why);
                if (why.Count == 4)
                {
                    TextVariable value = buildColorTextSignedVariable(why[3], true);
                    output.AddTEXT("TEXT_HELP_DIFUSED_TECH_COST", why[0], why[1], why[2], value);
                }
            }
            catch (Exception e)
            {
            //    UnityEngine.Debug.Log("de fusion boom");
             //   UnityEngine.Debug.Log(e);
            }
            return output;
        }
        public override void buildImprovementRequiresHelp(List<TextVariable> lRequirements, ImprovementType eImprovement, Game pGame, Player pActivePlayer, Tile pTile, bool bUpgradeImprovement = false)
        {
            base.buildImprovementRequiresHelp(lRequirements, eImprovement, pGame, pActivePlayer, pTile, bUpgradeImprovement);
            if (pActivePlayer == null)
                return;
            if (infos().improvement(eImprovement).mbWonder && pActivePlayer.countActiveLaws() < ((DynamicUnitsPlayer)pActivePlayer).countAllWonders())
            {
                lRequirements.Add(buildWarningTextVariable(TEXTVAR_TYPE("TEXT_HELPTEXT_IMPROVEMENT_REQUIRES_MORE_LAWS")));
            }
                
        }
        public override TextBuilder buildProjectHelp(TextBuilder builder, ProjectType eProject, City pCity, Game pGame, Player pPlayer, Player pActivePlayer, bool bName = true, bool bTechPrereq = true, bool bCosts = true, bool bDetails = true, bool bEncyclopedia = false, TextBuilder.ScopeType scopeType = TextBuilder.ScopeType.NONE)
        {
            builder = base.buildProjectHelp(builder, eProject, pCity, pGame, pPlayer, pActivePlayer, bName, bTechPrereq, bCosts, bDetails, bEncyclopedia, scopeType);
            // add custom help text for projects
            if (pCity != null && (infos().project(eProject).meProductionType != YieldType.NONE) && !infos().project(eProject).mbHidden)
            {
                
                if (((DUCity)pCity).isNegativeYieldforProject(eProject))
                {
                    using (buildWarningTextScope(builder, (pPlayer != null)))
                    {
                        builder.AddTEXT("TEXT_HELPTEXT_NO_PRODUCTION", buildYieldLinkVariable(infos().project(eProject).meProductionType));
                    }

                }
            }
            return builder;
        }
    }
}