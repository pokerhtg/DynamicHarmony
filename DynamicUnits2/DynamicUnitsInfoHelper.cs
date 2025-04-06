using System;
using TenCrowns.GameCore;

namespace DynamicUnits
{
    internal class DynamicUnitsInfoHelper : InfoHelpers
    {
        public DynamicUnitsInfoHelper(Infos pInfos) : base(pInfos)
        {

        }
        public override int getAttackDamage(int iFromStrength, int iToStrength, int iPercent)
        {
            int iDamage = mInfos.Globals.BASE_DAMAGE;

            iDamage *= iFromStrength;
            iDamage /= iToStrength;
            bool bRoundUp = iFromStrength * iPercent / 100 > iToStrength;
            if (bRoundUp)
            {
                iDamage++;
            }
            iDamage *= iPercent;
            iDamage /= 100;

            return Math.Max(iPercent < 100? 0: 1, iDamage);

        }
        public override int getHappinessLevelYieldModifier(int iLevel, YieldType eYield)
        {
            if (iLevel > 0)
                return iLevel * mInfos.yield(eYield).miPositiveHappinessModifier;
            else if (iLevel < -1)
                return -mInfos.utils().triangle(iLevel + 1) * mInfos.yield(eYield).miNegativeHappinessModifier;
            else return 0; //level 0 and -1 yields nothing
        }
        public override bool canStack(UnitType eUnit)
        {
            if (mInfos.unit(eUnit).miMovement < 1)
                return true;
            return base.canStack(eUnit);
        }
        public override int turnsLeft(int iThresholdWhole, int iProgress, int iRate)
        {
            if (iRate > 0)
            {
                int num = (iThresholdWhole * 10 - iProgress) / iRate;
                if (Math.Abs(iProgress + iRate * num) < Math.Abs(iThresholdWhole * 10))
                {
                    num++;
                }

                return Math.Max(1, num);
            }

            if (iRate < 0)
            {
                int num2 = iProgress / -iRate;
                if (iProgress % -iRate > 0)
                {
                    num2++;
                }

                return Math.Max(-1, num2);
            }

            return 0;
        }
     
        //***************************begin new methods*********************************
        public bool isMountaineer(UnitType eUnit)
        {
            //a nonwater unit that can anchor is a mountaineer

            InfoUnit unit = mInfos.unit(eUnit);
            
            return unit.mbAnchor && !unit.mbWater && !unit.mbAmphibious;
        }

    }

}