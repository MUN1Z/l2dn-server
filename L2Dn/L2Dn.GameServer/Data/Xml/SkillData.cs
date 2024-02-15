using System.Runtime.CompilerServices;
using System.Xml.Linq;
using L2Dn.Extensions;
using L2Dn.GameServer.Handlers;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Effects;
using L2Dn.GameServer.Model.Skills;
using L2Dn.GameServer.Utilities;
using L2Dn.Utilities;
using NLog;

namespace L2Dn.GameServer.Data.Xml;

/**
 * Skill data parser.
 * @author NosBit
 */
public class SkillData: DataReaderBase
{
	private static readonly Logger LOGGER = LogManager.GetLogger(nameof(SkillData));

	private readonly Map<long, Skill> _skills = new();
	private readonly Map<int, int> _skillsMaxLevel = new();

	private class NamedParamInfo
	{
		private readonly String _name;
		private readonly int _fromLevel;
		private readonly int _toLevel;
		private readonly int _fromSubLevel;
		private readonly int _toSubLevel;
		private readonly Map<int, Map<int, StatSet>> _info;

		public NamedParamInfo(String name, int fromLevel, int toLevel, int fromSubLevel, int toSubLevel,
			Map<int, Map<int, StatSet>> info)
		{
			_name = name;
			_fromLevel = fromLevel;
			_toLevel = toLevel;
			_fromSubLevel = fromSubLevel;
			_toSubLevel = toSubLevel;
			_info = info;
		}

		public String getName()
		{
			return _name;
		}

		public int getFromLevel()
		{
			return _fromLevel;
		}

		public int getToLevel()
		{
			return _toLevel;
		}

		public int getFromSubLevel()
		{
			return _fromSubLevel;
		}

		public int getToSubLevel()
		{
			return _toSubLevel;
		}

		public Map<int, Map<int, StatSet>> getInfo()
		{
			return _info;
		}
	}

	protected SkillData()
	{
		load();
	}

	/**
	 * Provides the skill hash
	 * @param skill The Skill to be hashed
	 * @return getSkillHashCode(skill.getId(), skill.getLevel())
	 */
	public static long getSkillHashCode(Skill skill)
	{
		return getSkillHashCode(skill.getId(), skill.getLevel(), skill.getSubLevel());
	}

	/**
	 * Centralized method for easier change of the hashing sys
	 * @param skillId The Skill Id
	 * @param skillLevel The Skill Level
	 * @return The Skill hash number
	 */
	public static long getSkillHashCode(int skillId, int skillLevel)
	{
		return getSkillHashCode(skillId, skillLevel, 0);
	}

	/**
	 * Centralized method for easier change of the hashing sys
	 * @param skillId The Skill Id
	 * @param skillLevel The Skill Level
	 * @param subSkillLevel The skill sub level
	 * @return The Skill hash number
	 */
	public static long getSkillHashCode(int skillId, int skillLevel, int subSkillLevel)
	{
		return (skillId * 4294967296L) + (subSkillLevel * 65536) + skillLevel;
	}

	public Skill getSkill(int skillId, int level)
	{
		return getSkill(skillId, level, 0);
	}

	public Skill getSkill(int skillId, int level, int subLevel)
	{
		Skill result = _skills.get(getSkillHashCode(skillId, level, subLevel));
		if (result != null)
		{
			return result;
		}

		// skill/level not found, fix for transformation scripts
		int maxLevel = getMaxLevel(skillId);
		// requested level too high
		if ((maxLevel > 0) && (level > maxLevel))
		{
			LOGGER.Warn(GetType().Name + ": Call to unexisting skill level id: " + skillId + " requested level: " +
			            level + " max level: " + maxLevel + ".");
			return _skills.get(getSkillHashCode(skillId, maxLevel));
		}

		LOGGER.Warn(GetType().Name + ": No skill info found for skill id " + skillId + " and skill level " + level);
		return null;
	}

	public int getMaxLevel(int skillId)
	{
		int maxLevel = _skillsMaxLevel.get(skillId);
		return maxLevel != null ? maxLevel : 0;
	}

	/**
	 * @param addNoble
	 * @param hasCastle
	 * @return an array with siege skills. If addNoble == true, will add also Advanced headquarters.
	 */
	public List<Skill> getSiegeSkills(bool addNoble, bool hasCastle)
	{
		List<Skill> temp = new();
		temp.add(_skills.get(getSkillHashCode((int)CommonSkill.SEAL_OF_RULER, 1)));
		temp.add(_skills.get(getSkillHashCode(247, 1))); // Build Headquarters
		if (addNoble)
		{
			temp.add(_skills.get(getSkillHashCode(326, 1))); // Build Advanced Headquarters
		}

		if (hasCastle)
		{
			temp.add(_skills.get(getSkillHashCode(844, 1))); // Outpost Construction
			temp.add(_skills.get(getSkillHashCode(845, 1))); // Outpost Demolition
		}

		return temp;
	}

	public bool isValidating()
	{
		return false;
	}

	[MethodImpl(MethodImplOptions.Synchronized)]
	public void load()
	{
		_skills.clear();
		_skillsMaxLevel.clear();

		LoadXmlDocuments(DataFileLocation.Data, "stats/skills").ForEach(t =>
		{
			t.Document.Elements("list").Elements("skill").ForEach(x => loadElement(t.FilePath, x));
		});

		if (Config.CUSTOM_SKILLS_LOAD)
		{
			LoadXmlDocuments(DataFileLocation.Data, "stats/skills/custom").ForEach(t =>
			{
				t.Document.Elements("list").Elements("skill").ForEach(x => loadElement(t.FilePath, x));
			});
		}

		LOGGER.Info(GetType().Name + ": Loaded " + _skills.size() + " Skills.");
	}

	public void reload()
	{
		load();
		// Reload Skill Tree as well.
		SkillTreeData.getInstance().load();
	}

	private void loadElement(string filePath, XElement element)
	{
		Map<int, Set<int>> levels = new();
		Map<int, Map<int, StatSet>> skillInfo = new();
		StatSet generalSkillInfo = skillInfo.computeIfAbsent(-1, k => new()).computeIfAbsent(-1, k => new StatSet());
		parseAttributes(element, "", generalSkillInfo);

		Map<String, Map<int, Map<int, Object>>> variableValues = new();
		Map<EffectScope, List<NamedParamInfo>> effectParamInfo = new();
		Map<SkillConditionScope, List<NamedParamInfo>> conditionParamInfo = new();

		foreach (XElement skillNode in element.Elements())
		{
			string skillNodeName = skillNode.Name.LocalName;
			switch (skillNodeName.toLowerCase())
			{
				case "variable":
				{
					string name = "@" + skillNode.Attribute("name").GetString();
					variableValues.put(name, parseValues(skillNode));
					break;
				}

				default:
				{
					EffectScope? effectScope = EffectScopeUtil.FindByName(skillNodeName);
					if (effectScope != null)
					{
						skillNode.Elements("effect").ForEach(effectsNode =>
						{
							effectParamInfo.computeIfAbsent(effectScope.Value, k => new())
								.add(parseNamedParamInfo(effectsNode, variableValues));
						});

						break;
					}

					SkillConditionScope? skillConditionScope = SkillConditionScopeUtil.FindByXmlName(skillNodeName);
					if (skillConditionScope != null)
					{
						skillNode.Elements("condition").ForEach(conditionNode =>
						{
							conditionParamInfo.computeIfAbsent(skillConditionScope.Value, k => new())
								.add(parseNamedParamInfo(conditionNode, variableValues));
						});
					}
					else
					{
						parseInfo(skillNode, variableValues, skillInfo);
					}

					break;
				}
			}
		}


		int fromLevel = generalSkillInfo.getInt(".fromLevel", 1);
		int toLevel = generalSkillInfo.getInt(".toLevel", 0);
		for (int i = fromLevel; i <= toLevel; i++)
		{
			levels.computeIfAbsent(i, k => new()).add(0);
		}

		skillInfo.forEach(kvp =>
		{
			int level = kvp.Key;
			Map<int, StatSet> subLevelMap = kvp.Value;
			if (level == -1)
			{
				return;
			}

			subLevelMap.forEach(kvp2 =>
			{
				int subLevel = kvp2.Key;
				StatSet statSet = kvp2.Value;
				if (subLevel == -1)
				{
					return;
				}

				levels.computeIfAbsent(level, k => new()).add(subLevel);
			});
		});

		effectParamInfo.values().Concat(conditionParamInfo.values()).ForEach(namedParamInfos =>
			namedParamInfos.forEach(namedParamInfo =>
			{
				namedParamInfo.getInfo().forEach(kvp =>
				{
					var (level, subLevelMap) = kvp;
					if (level == -1)
					{
						return;
					}

					subLevelMap.forEach(kvp2 =>
					{
						var (subLevel, statSet) = kvp2;
						if (subLevel == -1)
						{
							return;
						}

						levels.computeIfAbsent(level, k => new()).add(subLevel);
					});
				});

				if ((namedParamInfo.getFromLevel() != null) && (namedParamInfo.getToLevel() != null))
				{
					for (int i = namedParamInfo.getFromLevel(); i <= namedParamInfo.getToLevel(); i++)
					{
						if ((namedParamInfo.getFromSubLevel() != null) && (namedParamInfo.getToSubLevel() != null))
						{
							for (int j = namedParamInfo.getFromSubLevel(); j <= namedParamInfo.getToSubLevel(); j++)
							{
								levels.computeIfAbsent(i, k => new()).add(j);
							}
						}
						else
						{
							levels.computeIfAbsent(i, k => new()).add(0);
						}
					}
				}
			}));

		levels.forEach(kvp => kvp.Value.forEach(subLevel =>
		{
			var (level, subLevels) = kvp;
			StatSet statSet = skillInfo.getOrDefault(level, new()).get(subLevel) ?? new StatSet();
			skillInfo.getOrDefault(level, new()).getOrDefault(-1, StatSet.EMPTY_STATSET).getSet()
				.forEach(x => statSet.getSet().putIfAbsent(x.Key, x.Value));
			skillInfo.getOrDefault(-1, new()).getOrDefault(-1, StatSet.EMPTY_STATSET).getSet()
				.forEach(x => statSet.getSet().putIfAbsent(x.Key, x.Value));
			statSet.set(".level", level);
			statSet.set(".subLevel", subLevel);
			Skill skill = new Skill(statSet);
			forEachNamedParamInfoParam(effectParamInfo, level, subLevel, ((effectScope, @params) =>
			{
				String effectName = @params.getString(".name");
				@params.remove(".name");
				try
				{
					Func<StatSet, AbstractEffect> effectFunction =
						EffectHandler.getInstance().getHandlerFactory(effectName);
					if (effectFunction != null)
					{
						skill.addEffect(effectScope, effectFunction(@params));
					}
					else
					{
						LOGGER.Warn(GetType().Name + ": Missing effect for Skill Id[" + statSet.getInt(".id") +
						            "] Level[" + level + "] SubLevel[" + subLevel + "] Effect Scope[" + effectScope +
						            "] Effect Name[" + effectName + "]");
					}
				}
				catch (Exception e)
				{
					LOGGER.Warn(
						GetType().Name + ": Failed loading effect for Skill Id[" + statSet.getInt(".id") + "] Level[" +
						level + "] SubLevel[" + subLevel + "] Effect Scope[" + effectScope + "] Effect Name[" +
						effectName + "]", e);
				}
			}));

			forEachNamedParamInfoParam(conditionParamInfo, level, subLevel, ((skillConditionScope, @params) =>
			{
				String conditionName = @params.getString(".name");
				@params.remove(".name");
				try
				{
					Func<StatSet, ISkillCondition> conditionFunction =
						SkillConditionHandler.getInstance().getHandlerFactory(conditionName);
					if (conditionFunction != null)
					{
						if (skill.isPassive())
						{
							if (skillConditionScope != SkillConditionScope.PASSIVE)
							{
								LOGGER.Warn(GetType().Name + ": Non passive condition for passive Skill Id[" +
								            statSet.getInt(".id") + "] Level[" + level + "] SubLevel[" + subLevel +
								            "]");
							}
						}
						else if (skillConditionScope == SkillConditionScope.PASSIVE)
						{
							LOGGER.Warn(GetType().Name + ": Passive condition for non passive Skill Id[" +
							            statSet.getInt(".id") + "] Level[" + level + "] SubLevel[" + subLevel + "]");
						}

						skill.addCondition(skillConditionScope, conditionFunction(@params));
					}
					else
					{
						LOGGER.Warn(GetType().Name + ": Missing condition for Skill Id[" + statSet.getInt(".id") +
						            "] Level[" + level + "] SubLevel[" + subLevel + "] Effect Scope[" +
						            skillConditionScope + "] Effect Name[" + conditionName + "]");
					}
				}
				catch (Exception e)
				{
					LOGGER.Warn(
						GetType().Name + ": Failed loading condition for Skill Id[" + statSet.getInt(".id") +
						"] Level[" + level + "] SubLevel[" + subLevel + "] Condition Scope[" + skillConditionScope +
						"] Condition Name[" + conditionName + "]", e);
				}
			}));

			_skills.put(getSkillHashCode(skill), skill);
			_skillsMaxLevel.merge(skill.getId(), skill.getLevel(), Math.Max);
			if ((skill.getSubLevel() % 1000) == 1)
			{
				EnchantSkillGroupsData.getInstance()
					.addRouteForSkill(skill.getId(), skill.getLevel(), skill.getSubLevel());
			}
		}));
	}

	private void forEachNamedParamInfoParam<T>(Map<T, List<NamedParamInfo>> paramInfo, int level, int subLevel,
		Action<T, StatSet> consumer)
		where T: notnull
	{
		paramInfo.forEach(kvp => kvp.Value.forEach(namedParamInfo =>
		{
			var (scope, namedParamInfos) = kvp;
			if ((((namedParamInfo.getFromLevel() == null) && (namedParamInfo.getToLevel() == null)) ||
			     ((namedParamInfo.getFromLevel() <= level) && (namedParamInfo.getToLevel() >= level))) //
			    && (((namedParamInfo.getFromSubLevel() == null) && (namedParamInfo.getToSubLevel() == null)) ||
			        ((namedParamInfo.getFromSubLevel() <= subLevel) && (namedParamInfo.getToSubLevel() >= subLevel))))
			{
				StatSet @params = namedParamInfo.getInfo().getOrDefault(level, new()).get(subLevel) ?? new StatSet();

				namedParamInfo.getInfo().getOrDefault(level, new())
					.getOrDefault(-1, StatSet.EMPTY_STATSET).getSet()
					.forEach(x => @params.getSet().putIfAbsent(x.Key, x.Value));
				namedParamInfo.getInfo().getOrDefault(-1, new())
					.getOrDefault(-1, StatSet.EMPTY_STATSET).getSet()
					.forEach(x => @params.getSet().putIfAbsent(x.Key, x.Value));
				@params.set(".name", namedParamInfo.getName());
				consumer(scope, @params);
			}
		}));
	}

	private NamedParamInfo parseNamedParamInfo(XElement element, Map<String, Map<int, Map<int, Object>>> variableValues)
	{
		String name = element.Attribute("name").GetString();
		int level = element.Attribute("level").GetInt32();
		int fromLevel = element.Attribute("fromLevel").GetInt32(level);
		int toLevel = element.Attribute("toLevel").GetInt32(level);
		int subLevel = element.Attribute("subLevel").GetInt32();
		int fromSubLevel = element.Attribute("fromSubLevel").GetInt32(subLevel);
		int toSubLevel = element.Attribute("toSubLevel").GetInt32(subLevel);

		Map<int, Map<int, StatSet>> info = new();
		if (!string.IsNullOrEmpty(element.Value))
			parseInfo(element, variableValues, info);

		return new NamedParamInfo(name, fromLevel, toLevel, fromSubLevel, toSubLevel, info);
	}

	private void parseInfo(XElement element, Map<String, Map<int, Map<int, Object>>> variableValues,
		Map<int, Map<int, StatSet>> info)
	{
		Map<int, Map<int, Object>> values = parseValues(element);
		Object generalValue = values.getOrDefault(-1, new()).get(-1);
		if (generalValue != null)
		{
			string stringGeneralValue = generalValue?.ToString() ?? string.Empty;
			if (stringGeneralValue.startsWith("@"))
			{
				Map<int, Map<int, Object>> variableValue = variableValues.get(stringGeneralValue);
				if (variableValue != null)
				{
					values = variableValue;
				}
				else
				{
					throw new InvalidOperationException("undefined variable " + stringGeneralValue);
				}
			}
		}

		values.forEach(kvp =>
		{
			var (level, subLevelMap) = kvp;
			kvp.Value.forEach(kvp2 =>
			{
				var (subLevel, value) = kvp2;
				info.computeIfAbsent(level, k => new()).computeIfAbsent(subLevel, k => new StatSet())
					.set(element.Name.LocalName, value);
			});
		});
	}

	private Map<int, Map<int, Object>> parseValues(XElement element)
	{
		Map<int, Map<int, Object>> values = new();
		Object parsedValue = parseValue(element, true, false, new());
		if (parsedValue != null)
		{
			values.computeIfAbsent(-1, k => new()).put(-1, parsedValue);
		}
		else
		{
			foreach (XElement n in element.Elements())
			{
				if (n.Name.LocalName.equalsIgnoreCase("value"))
				{
					int level = n.Attribute("level").GetInt32(-1);
					if (level >= 0)
					{
						parsedValue = parseValue(n, false, false, new());
						if (parsedValue != null)
						{
							int subLevel = n.Attribute("subLevel").GetInt32(-1);
							values.computeIfAbsent(level, k => new()).put(subLevel, parsedValue);
						}
					}
					else
					{
						int fromLevel = n.Attribute("fromLevel").GetInt32();
						int toLevel = n.Attribute("toLevel").GetInt32();
						int fromSubLevel = n.Attribute("fromSubLevel").GetInt32(-1);
						int toSubLevel = n.Attribute("toSubLevel").GetInt32(-1);
						for (int i = fromLevel; i <= toLevel; i++)
						{
							for (int j = fromSubLevel; j <= toSubLevel; j++)
							{
								Map<int, Object> subValues = values.computeIfAbsent(i, k => new());
								Map<String, Double> variables = new();
								variables.put("index", (i - fromLevel) + 1d);
								variables.put("subIndex", (j - fromSubLevel) + 1d);
								Object @base = values.getOrDefault(i, new()).get(-1);
								String baseText = @base?.ToString() ?? string.Empty;
								if ((@base != null) && !(@base is StatSet) && (!baseText.equalsIgnoreCase("true") &&
								                                               !baseText.equalsIgnoreCase("false")))
								{
									variables.put("base", double.Parse(baseText));
								}

								parsedValue = parseValue(n, false, false, variables);
								if (parsedValue != null)
								{
									subValues.put(j, parsedValue);
								}
							}
						}
					}
				}
			}
		}

		return values;
	}

	public Object parseValue(XElement element, bool blockValue, bool parseAttributes, Map<String, Double> variables)
	{
		StatSet statSet = null;
		List<Object> list = null;
		Object text = null;
		if (parseAttributes && (!element.Name.LocalName.equals("value") || !blockValue) && (element.Attributes().Any()))
		{
			statSet = new StatSet();
			this.parseAttributes(element, "", statSet, variables);
		}

		foreach (XElement n in element.Elements())
		{
			String nodeName = n.Name.LocalName;
			switch (nodeName)
			{
				case "#text":
				{
					String value = n.Value.Trim();
					if (!value.isEmpty())
					{
						text = parseNodeValue(value, variables);
					}

					break;
				}
				case "item":
				{
					if (list == null)
					{
						list = new();
					}

					Object value = parseValue(n, false, true, variables);
					if (value != null)
					{
						list.add(value);
					}

					break;
				}
				case "value":
				{
					if (blockValue)
					{
						break;
					}

					// fallthrough
					goto default;
				}
				default:
				{
					Object value = parseValue(n, false, true, variables);
					if (value != null)
					{
						if (statSet == null)
						{
							statSet = new StatSet();
						}

						statSet.set(nodeName, value);
					}

					break;
				}
			}
		}

		if (list != null)
		{
			if (text != null)
			{
				throw new InvalidOperationException("Text and list in same node are not allowed. Node[" + element +
				                                    "]");
			}

			if (statSet != null)
			{
				statSet.set(".", list);
			}
			else
			{
				return list;
			}
		}

		if (text != null)
		{
			if (list != null)
			{
				throw new InvalidOperationException("Text and list in same node are not allowed. Node[" + element +
				                                    "]");
			}

			if (statSet != null)
			{
				statSet.set(".", text);
			}
			else
			{
				return text;
			}
		}

		return statSet;
	}

	private void parseAttributes(XElement element, string prefix, StatSet statSet, Map<string, double> variables)
	{
		foreach (XAttribute attribute in element.Attributes())
		{
			string name = attribute.Name.LocalName;
			string value = attribute.Value;
			statSet.set(prefix + "." + name, parseNodeValue(value, variables));
		}
	}

	private void parseAttributes(XElement element, string prefix, StatSet statSet)
	{
		parseAttributes(element, prefix, statSet, new());
	}

	private Object parseNodeValue(string value, Map<string, double> variables)
	{
		if (value.startsWith("{") && value.endsWith("}"))
		{
			throw new NotImplementedException();
			//return new ExpressionBuilder(value).variables(variables.Keys).build().setVariables(variables).evaluate();
		}

		return value;
	}

	public static SkillData getInstance()
	{
		return SingletonHolder.INSTANCE;
	}

	private static class SingletonHolder
	{
		public static readonly SkillData INSTANCE = new();
	}
}