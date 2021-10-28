﻿using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace RealisticBattleAiModule.AiModule.RbmBehaviors
{
    class RBMBehaviorForwardSkirmish : BehaviorComponent
	{
		private SkirmishMode _skirmishMode;

		private Timer _returnTimer = null;
		private Timer _reformTimer = null;
		private Timer _attackTimer = null;

		public FormationAI.BehaviorSide FlankSide;

		private float mobilityModifier = 1f;
		private enum SkirmishMode
		{
			Reform,
			Returning,
			Attack
		}

		private bool _isEnemyReachable = true;

		public RBMBehaviorForwardSkirmish(Formation formation)
			: base(formation)
		{
			_skirmishMode = SkirmishMode.Reform;
			behaviorSide = formation.AI.Side;
			CalculateCurrentOrder();
			base.BehaviorCoherence = 0.5f;
		}

		protected override float GetAiWeight()
		{
			if (!_isEnemyReachable)
			{
				return 0f;
			}
			if (base.Formation != null && base.Formation.QuerySystem.IsCavalryFormation)
			{
				if (Utilities.CheckIfMountedSkirmishFormation(base.Formation, 0.6f))
				{
					return 5f;
				}
			}
			if (base.Formation != null && base.Formation.QuerySystem.IsInfantryFormation)
			{
				int countOfSkirmishers = 0;
				base.Formation.ApplyActionOnEachUnitViaBackupList(delegate (Agent agent)
				{
					if (Utilities.CheckIfSkirmisherAgent(agent, 1))
					{
						countOfSkirmishers++;
					}
				});
				if (countOfSkirmishers / base.Formation.CountOfUnits > 0.6f)
				{
					return 5f;
				}
				else
				{
					return 0f;
				}
			}
			return 0f;
		}

		protected override void OnBehaviorActivatedAux()
		{
			CalculateCurrentOrder();
			base.Formation.SetMovementOrder(base.CurrentOrder);
			base.Formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLine;
			base.Formation.FacingOrder = FacingOrder.FacingOrderLookAtEnemy;
			base.Formation.FiringOrder = FiringOrder.FiringOrderFireAtWill;
			base.Formation.FormOrder = FormOrder.FormOrderDeep;
			base.Formation.WeaponUsageOrder = WeaponUsageOrder.WeaponUsageOrderUseAny;
		}

		protected override void CalculateCurrentOrder()
		{
            if (base.Formation.QuerySystem.IsInfantryFormation)
            {
				mobilityModifier = 1.25f;
            }
            else
            {
				mobilityModifier = 1f;
			}
			WorldPosition position = base.Formation.QuerySystem.MedianPosition;
			_isEnemyReachable = base.Formation.QuerySystem.ClosestSignificantlyLargeEnemyFormation != null && (!(base.Formation.Team.TeamAI is TeamAISiegeComponent) || !TeamAISiegeComponent.IsFormationInsideCastle(base.Formation.QuerySystem.ClosestSignificantlyLargeEnemyFormation.Formation, includeOnlyPositionedUnits: false));
			Vec2 averagePosition = base.Formation.QuerySystem.AveragePosition;
			if (!_isEnemyReachable)
			{
				position.SetVec2(averagePosition);
			}
			else
			{
				float skirmishRange = 45f / mobilityModifier;
				float flankRange = 25f;

				Formation enemyFormation = base.Formation.QuerySystem.ClosestSignificantlyLargeEnemyFormation.Formation;
				Formation allyFormation = Utilities.FindSignificantAlly(base.Formation, true, true, false, false, false);

				if (base.Formation != null && base.Formation.QuerySystem.IsInfantryFormation)
				{
					enemyFormation = Utilities.FindSignificantEnemyToPosition(base.Formation, position, true, true, false, false, false, false);
				}

				Vec2 averageAllyFormationPosition = base.Formation.QuerySystem.Team.AveragePosition;
				WorldPosition medianTargetFormationPosition = base.Formation.QuerySystem.Team.MedianTargetFormationPosition;
				Vec2 enemyDirection = (medianTargetFormationPosition.AsVec2 - averageAllyFormationPosition).Normalized();

				switch (_skirmishMode)
                {
					case SkirmishMode.Reform:
                        {
							_returnTimer = null;
							if (averagePosition.Distance(enemyFormation.QuerySystem.AveragePosition) > skirmishRange)
                            {
								if (_reformTimer == null)
								{
									_reformTimer = new Timer(Mission.Current.CurrentTime, 4f/ mobilityModifier);
								}
							}
							if (_reformTimer != null && _reformTimer.Check(Mission.Current.CurrentTime))
							{
								_skirmishMode = SkirmishMode.Attack;
							}

							if (behaviorSide == FormationAI.BehaviorSide.Right || FlankSide == FormationAI.BehaviorSide.Right)
							{
								Vec2 calcPosition = allyFormation.CurrentPosition + enemyDirection.RightVec().Normalized() * (allyFormation.Width + base.Formation.Width + flankRange);
								position.SetVec2(calcPosition);
							}
							else if (behaviorSide == FormationAI.BehaviorSide.Left || FlankSide == FormationAI.BehaviorSide.Left)
							{
								Vec2 calcPosition = allyFormation.CurrentPosition + enemyDirection.LeftVec().Normalized() * (allyFormation.Width + base.Formation.Width + flankRange);
								position.SetVec2(calcPosition);
							}
                            else
                            {
								position = allyFormation.QuerySystem.MedianPosition;
							}
							break;
                        }
					case SkirmishMode.Returning:
						{
							_attackTimer = null;
							if (_returnTimer == null)
							{
								_returnTimer = new Timer(Mission.Current.CurrentTime, 10f/ mobilityModifier);
							}
							if (_returnTimer != null && _returnTimer.Check(Mission.Current.CurrentTime))
							{
									_skirmishMode = SkirmishMode.Reform;
							}

							if (behaviorSide == FormationAI.BehaviorSide.Right || FlankSide == FormationAI.BehaviorSide.Right)
							{
								Vec2 calcPosition = allyFormation.CurrentPosition + enemyDirection.RightVec().Normalized() * (allyFormation.Width + base.Formation.Width + flankRange);
								position.SetVec2(calcPosition);
							}
							else if (behaviorSide == FormationAI.BehaviorSide.Left || FlankSide == FormationAI.BehaviorSide.Left)
							{
								Vec2 calcPosition = allyFormation.CurrentPosition + enemyDirection.LeftVec().Normalized() * (allyFormation.Width + base.Formation.Width + flankRange);
								position.SetVec2(calcPosition);
							}
							else
							{
								position = allyFormation.QuerySystem.MedianPosition;
							}

							break;
						}
					case SkirmishMode.Attack:
						{
							_reformTimer = null;
							if ((averagePosition.Distance(enemyFormation.QuerySystem.AveragePosition) < skirmishRange) || (base.Formation.QuerySystem.MakingRangedAttackRatio > 0.1f))
							{
								if(_attackTimer == null)
                                {
									_attackTimer = new Timer(Mission.Current.CurrentTime, 3f * mobilityModifier);
								}
							}
							if (_attackTimer != null && _attackTimer.Check(Mission.Current.CurrentTime))
                            {
								_skirmishMode = SkirmishMode.Returning;
							}

							if (behaviorSide == FormationAI.BehaviorSide.Right || FlankSide == FormationAI.BehaviorSide.Right)
							{
								position = medianTargetFormationPosition;
								Vec2 calcPosition = position.AsVec2 - enemyDirection * (skirmishRange - (10f + base.Formation.Depth * 0.5f));
								calcPosition = calcPosition + enemyFormation.Direction.LeftVec().Normalized() * (enemyFormation.Width / 2f) * mobilityModifier;
								position.SetVec2(calcPosition);
							}
							else if (behaviorSide == FormationAI.BehaviorSide.Left || FlankSide == FormationAI.BehaviorSide.Left)
							{
								position = medianTargetFormationPosition;
								Vec2 calcPosition = position.AsVec2 - enemyDirection * (skirmishRange - (10f + base.Formation.Depth * 0.5f));
								calcPosition = calcPosition + enemyFormation.Direction.RightVec().Normalized() * (enemyFormation.Width/2f) * mobilityModifier;
								position.SetVec2(calcPosition);
							}
							else
							{
								position = medianTargetFormationPosition;
								Vec2 calcPosition = position.AsVec2 - enemyDirection * (skirmishRange - (10f + base.Formation.Depth * 0.5f));
								position.SetVec2(calcPosition);
							}

							//position = enemyFormation.QuerySystem.MedianPosition;
							break;
						}
				}
			}
			base.CurrentOrder = MovementOrder.MovementOrderMove(position);
		}

		public override void TickOccasionally()
		{
			CalculateCurrentOrder();
			base.Formation.SetMovementOrder(base.CurrentOrder);
		}
	}
}
