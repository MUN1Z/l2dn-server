using L2Dn.Events;
using L2Dn.GameServer.Model.Actor;

namespace L2Dn.GameServer.Model.Events.Impl.Summons;

/**
 * @author St3eT
 */
public class OnSummonTalk: EventBase
{
	private readonly Summon _summon;
	
	public OnSummonTalk(Summon summon)
	{
		_summon = summon;
	}
	
	public Summon getSummon()
	{
		return _summon;
	}
}