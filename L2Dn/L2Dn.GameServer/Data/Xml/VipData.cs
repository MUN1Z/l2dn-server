using System.Xml.Linq;
using L2Dn.Extensions;
using L2Dn.GameServer.Model.Vips;
using L2Dn.GameServer.Utilities;
using L2Dn.Utilities;
using NLog;

namespace L2Dn.GameServer.Data.Xml;

/**
 * @author Gabriel Costa Souza
 */
public class VipData: DataReaderBase
{
	private static readonly Logger LOGGER = LogManager.GetLogger(nameof(VipData));
	
	private readonly Map<int, VipInfo> _vipTiers = new();
	
	protected VipData()
	{
		load();
	}
	
	public void load()
	{
		if (!Config.VIP_SYSTEM_ENABLED)
		{
			return;
		}
		
		_vipTiers.clear();
		
		XDocument document = LoadXmlDocument(DataFileLocation.Data, "Vip.xml");
		document.Elements("list").Elements("vip").ForEach(parseElement);
		
		LOGGER.Info(GetType().Name + ": Loaded " + _vipTiers.size() + " vips.");
	}

	private void parseElement(XElement element)
	{
		int tier = element.Attribute("tier").GetInt32();
		int pointsRequired = element.Attribute("points-required").GetInt32();
		int pointsLose = element.Attribute("points-lose").GetInt32();
		VipInfo vipInfo = new VipInfo(tier, pointsRequired, pointsLose);
		element.Elements("bonus").ForEach(el =>
		{
			int skill = el.Attribute("skill").GetInt32();
			vipInfo.setSkill(skill);
		});
		
		_vipTiers.put(tier, vipInfo);
	}

	/**
	 * Gets the single instance of VipData.
	 * @return single instance of VipData
	 */
	public static VipData getInstance()
	{
		return SingletonHolder.INSTANCE;
	}
	
	/**
	 * The Class SingletonHolder.
	 */
	private static class SingletonHolder
	{
		public static readonly VipData INSTANCE = new();
	}
	
	public int getSkillId(int tier)
	{
		return _vipTiers.get(tier).getSkill();
	}
	
	public Map<int, VipInfo> getVipTiers()
	{
		return _vipTiers;
	}
}