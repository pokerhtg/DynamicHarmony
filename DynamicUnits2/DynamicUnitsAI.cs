
using TenCrowns.AppCore;
using TenCrowns.GameCore;
using System;
using System.Diagnostics;
using static TenCrowns.GameCore.Unit;
namespace DynamicUnits
{
	internal class DUUnitAI : Unit.UnitAI
	{
		public override bool isProtectedTile(Tile pTile, bool bAfterAttack, int iMinPowerPercent, int iExtraDanger = 0)
		{
			try
			{
				return base.isProtectedTile(pTile, bAfterAttack, iMinPowerPercent, iExtraDanger = 0);
			}
			catch (Exception)
			{
				
				return true;
			}
		}

		public void AttackFromCurrentTile(bool bKillOnly)
		{ 
			base.doAttackFromCurrentTile(bKillOnly);
		}

        } }