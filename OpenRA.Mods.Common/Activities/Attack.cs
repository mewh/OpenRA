#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Activities
{
	/* non-turreted attack */
	public class Attack : Activity
	{
		protected readonly Target Target;
		readonly AttackBase attack;
		readonly IMove move;
		readonly IFacing facing;
		readonly WRange minRange;
		readonly WRange maxRange;
		readonly IPositionable positionable;

		public Attack(Actor self, Target target, WRange minRange, WRange maxRange, bool allowMovement)
		{
			Target = target;
			this.minRange = minRange;
			this.maxRange = maxRange;

			attack = self.Trait<AttackBase>();
			facing = self.Trait<IFacing>();
			positionable = self.Trait<IPositionable>();

			move = allowMovement ? self.TraitOrDefault<IMove>() : null;
		}

		public override Activity Tick(Actor self)
		{
			var ret = InnerTick(self, attack);
			attack.IsAttacking = ret == this;
			return ret;
		}

		protected virtual Activity InnerTick(Actor self, AttackBase attack)
		{
			if (IsCanceled)
				return NextActivity;

			var type = Target.Type;
			if (!Target.IsValidFor(self) || type == TargetType.FrozenActor)
				return NextActivity;

			if (attack.Info.AttackRequiresEnteringCell && !positionable.CanEnterCell(Target.Actor.Location, null, false))
				return NextActivity;

			// Drop the target if it moves under the shroud / fog.
			// HACK: This would otherwise break targeting frozen actors
			// The problem is that Shroud.IsTargetable returns false (as it should) for
			// frozen actors, but we do want to explicitly target the underlying actor here.
			if (!attack.Info.IgnoresVisibility && type == TargetType.Actor && !Target.Actor.HasTrait<FrozenUnderFog>() && !self.Owner.CanTargetActor(Target.Actor))
				return NextActivity;

			// Try to move within range
			if (move != null && (!Target.IsInRange(self.CenterPosition, maxRange) || Target.IsInRange(self.CenterPosition, minRange)))
				return Util.SequenceActivities(move.MoveWithinRange(Target, minRange, maxRange), this);

			var desiredFacing = Util.GetFacing(Target.CenterPosition - self.CenterPosition, 0);
			if (facing.Facing != desiredFacing)
				return Util.SequenceActivities(new Turn(self, desiredFacing), this);

			attack.DoAttack(self, Target);

			return this;
		}
	}
}
