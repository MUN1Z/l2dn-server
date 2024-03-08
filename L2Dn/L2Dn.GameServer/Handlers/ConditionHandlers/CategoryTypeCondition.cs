﻿using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Conditions;
using L2Dn.GameServer.Utilities;

namespace L2Dn.GameServer.Handlers.ConditionHandlers;

/**
 * @author Sdw, Mobius
 */
public class CategoryTypeCondition: ICondition
{
    private readonly Set<CategoryType> _categoryTypes = new();

    public CategoryTypeCondition(StatSet @params)
    {
        _categoryTypes.addAll(@params.getEnumList<CategoryType>("category"));
    }

    public bool test(Creature creature, WorldObject target)
    {
        foreach (CategoryType type in _categoryTypes)
        {
            if (creature.isInCategory(type))
            {
                return true;
            }
        }

        return false;
    }
}