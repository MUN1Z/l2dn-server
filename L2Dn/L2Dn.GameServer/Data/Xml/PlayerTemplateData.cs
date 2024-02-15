using System.Xml.Linq;
using L2Dn.Extensions;
using L2Dn.GameServer.Db;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor.Templates;
using L2Dn.GameServer.Utilities;
using L2Dn.Utilities;
using NLog;

namespace L2Dn.GameServer.Data.Xml;

/**
 * Loads player's base stats.
 * @author Forsaiken, Zoey76, GKR
 */
public class PlayerTemplateData: DataReaderBase
{
	private static readonly Logger LOGGER = LogManager.GetLogger(nameof(PlayerTemplateData));
	
	private readonly Map<CharacterClass, PlayerTemplate> _playerTemplates = new();
	
	private int _dataCount = 0;
	private int _autoGeneratedCount = 0;
	
	protected PlayerTemplateData()
	{
		load();
	}
	
	public void load()
	{
		_playerTemplates.clear();
		
		LoadXmlDocuments(DataFileLocation.Data, "stats/chars/baseStats").ForEach(t =>
		{
			t.Document.Elements("list").ForEach(x => loadElement(t.FilePath, x));
		});
		
		LOGGER.Info(GetType().Name + ": Loaded " + _playerTemplates.size() + " character templates.");
		LOGGER.Info(GetType().Name + ": Loaded " + _dataCount + " level up gain records.");
		if (_autoGeneratedCount > 0)
		{
			LOGGER.Info(GetType().Name + ": Generated " + _autoGeneratedCount + " level up gain records.");
		}
	}

	private void loadElement(string filePath, XElement element)
	{
		CharacterClass classId = (CharacterClass)(int)element.Elements("classId").Single();

		XElement staticDataElement = element.Elements("staticData").Single();
		StatSet set = new StatSet();
		set.set("classId", classId);
		List<Location> creationPoints = new();
		staticDataElement.Elements().ForEach(el =>
		{
			string elName = el.Name.LocalName;
			List<XElement> children = el.Elements().ToList();
			if (children.Count > 0)
			{
				el.Elements().ForEach(e =>
				{
					string eName = e.Name.LocalName;
					// use CreatureTemplate(superclass) fields for male collision height and collision radius
					if (elName == "collisionMale")
					{
						if (eName == "radius")
							set.set("collision_radius", e.Value);
						else if (eName == "height")
							set.set("collision_height", e.Value);
					}

					if ("node".equalsIgnoreCase(eName))
						creationPoints.add(new Location(e.Attribute("x").GetInt32(), e.Attribute("y").GetInt32(),
							e.Attribute("z").GetInt32()));
					else if ("walk".equalsIgnoreCase(eName))
						set.set("baseWalkSpd", e.Value);
					else if ("run".equalsIgnoreCase(eName))
						set.set("baseRunSpd", e.Value);
					else if ("slowSwim".equals(eName))
						set.set("baseSwimWalkSpd", e.Value);
					else if ("fastSwim".equals(eName))
						set.set("baseSwimRunSpd", e.Value);
					else
						set.set(elName + eName, e.Value);
				});
			}
			else
				set.set(elName, el.Value);
		});

		// calculate total pdef and mdef from parts
		set.set("basePDef",
			(set.getInt("basePDefchest", 0) + set.getInt("basePDeflegs", 0) + set.getInt("basePDefhead", 0) +
			 set.getInt("basePDeffeet", 0) + set.getInt("basePDefgloves", 0) + set.getInt("basePDefunderwear", 0) +
			 set.getInt("basePDefcloak", 0) + set.getInt("basePDefhair", 0)));
		set.set("baseMDef",
			(set.getInt("baseMDefrear", 0) + set.getInt("baseMDeflear", 0) + set.getInt("baseMDefrfinger", 0) +
			 set.getInt("baseMDefrfinger", 0) + set.getInt("baseMDefneck", 0)));

		PlayerTemplate template = new PlayerTemplate(set, creationPoints);
		_playerTemplates.put(classId, template);

		XElement lvlUpgainDataElement = element.Elements("lvlUpgainData").Single();
		int level = 0;
		lvlUpgainDataElement.Elements("level").ForEach(el =>
		{
			int lvl = el.Attribute("val").GetInt32();
			if (lvl > level)
				level = lvl;

			el.Elements().ForEach(e =>
			{
				string nodeName = e.Name.LocalName;
				if ((lvl < Config.PLAYER_MAXIMUM_LEVEL) &&
				    (nodeName.startsWith("hp") || nodeName.startsWith("mp") || nodeName.startsWith("cp")) &&
				    _playerTemplates.containsKey(classId))
				{
					template.setUpgainValue(nodeName, lvl, (double)e);
					_dataCount++;
				}
			});
		});

		// Generate missing stats automatically.
		while (level < (Config.PLAYER_MAXIMUM_LEVEL - 1))
		{
			level++;
			_autoGeneratedCount++;
			double hpM1 = template.getBaseHpMax(level - 1);
			template.setUpgainValue("hp", level,
				(((hpM1 * level) / (level - 1)) + ((hpM1 * (level + 1)) / (level - 1))) / 2);
			double mpM1 = template.getBaseMpMax(level - 1);
			template.setUpgainValue("mp", level,
				(((mpM1 * level) / (level - 1)) + ((mpM1 * (level + 1)) / (level - 1))) / 2);
			double cpM1 = template.getBaseCpMax(level - 1);
			template.setUpgainValue("cp", level,
				(((cpM1 * level) / (level - 1)) + ((cpM1 * (level + 1)) / (level - 1))) / 2);
			double hpRegM1 = template.getBaseHpRegen(level - 1);
			double hpRegM2 = template.getBaseHpRegen(level - 2);
			template.setUpgainValue("hpRegen", level, (hpRegM1 * 2) - hpRegM2);
			double mpRegM1 = template.getBaseMpRegen(level - 1);
			double mpRegM2 = template.getBaseMpRegen(level - 2);
			template.setUpgainValue("mpRegen", level, (mpRegM1 * 2) - mpRegM2);
			double cpRegM1 = template.getBaseCpRegen(level - 1);
			double cpRegM2 = template.getBaseCpRegen(level - 2);
			template.setUpgainValue("cpRegen", level, (cpRegM1 * 2) - cpRegM2);
		}
	}

	public PlayerTemplate getTemplate(CharacterClass classId)
	{
		return _playerTemplates.get(classId);
	}
	
	public static PlayerTemplateData getInstance()
	{
		return SingletonHolder.INSTANCE;
	}
	
	private static class SingletonHolder
	{
		public static readonly PlayerTemplateData INSTANCE = new();
	}
}