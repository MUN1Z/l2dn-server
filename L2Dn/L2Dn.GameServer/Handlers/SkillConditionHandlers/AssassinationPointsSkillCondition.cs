using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Skills;

namespace L2Dn.GameServer.Handlers.SkillConditionHandlers;

/**
 * @author Mobius
 */
public class AssassinationPointsSkillCondition: ISkillCondition
{
	private readonly int _amount;
	
	public AssassinationPointsSkillCondition(StatSet @params)
	{
		_amount = @params.getInt("amount") * 10000;
	}
	
	public bool canUse(Creature caster, Skill skill, WorldObject target)
	{
		return caster.getActingPlayer().getAssassinationPoints() >= _amount;
	}
}