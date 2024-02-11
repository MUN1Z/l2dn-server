﻿using System.Text;
using L2Dn.GameServer.AI;
using L2Dn.GameServer.Data.Sql;
using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Geo;
using L2Dn.GameServer.Handlers;
using L2Dn.GameServer.InstanceManagers;
using L2Dn.GameServer.Model.Actor.Instances;
using L2Dn.GameServer.Model.Actor.Stats;
using L2Dn.GameServer.Model.Actor.Status;
using L2Dn.GameServer.Model.Actor.Templates;
using L2Dn.GameServer.Model.Effects;
using L2Dn.GameServer.Model.Events;
using L2Dn.GameServer.Model.Events.Impl.Creatures.Players;
using L2Dn.GameServer.Model.ItemContainers;
using L2Dn.GameServer.Model.Items;
using L2Dn.GameServer.Model.Items.Instances;
using L2Dn.GameServer.Model.Items.Types;
using L2Dn.GameServer.Model.Olympiads;
using L2Dn.GameServer.Model.Skills;
using L2Dn.GameServer.Model.Skills.Targets;
using L2Dn.GameServer.Model.Zones;
using L2Dn.GameServer.Network.Enums;
using L2Dn.GameServer.Network.OutgoingPackets;
using L2Dn.GameServer.TaskManagers;
using L2Dn.GameServer.Utilities;
using ThreadPool = L2Dn.GameServer.Utilities.ThreadPool;

namespace L2Dn.GameServer.Model.Actor;

public abstract class Summon : Playable
{
	private Player _owner;
	private int _attackRange = 36; // Melee range
	private bool _follow = true;
	private bool _previousFollowStatus = true;
	protected bool _restoreSummon = true;
	private int _summonPoints = 0;
	private ScheduledFuture _abnormalEffectTask;
	
	private static readonly int[] PASSIVE_SUMMONS =
	{
		12564, 14702, 14703, 14704, 14705, 14706, 14707, 14708, 14709, 14710, 14711,
		14712, 14713, 14714, 14715, 14716, 14717, 14718, 14719, 14720, 14721, 14722, 14723,
		14724, 14725, 14726, 14727, 14728, 14729, 14730, 14731, 14732, 14733, 14734, 14735, 14736,
		15955
	};
	
	public Summon(NpcTemplate template, Player owner):  base(template)
	{
		setInstanceType(InstanceType.Summon);
		setInstance(owner.getInstanceWorld()); // set instance to same as owner
		setShowSummonAnimation(true);
		_owner = owner;
		getAI();
		
		// Make sure summon does not spawn in a wall.
		int x = owner.getX();
		int y = owner.getY();
		int z = owner.getZ();
		Location location = GeoEngine.getInstance().getValidLocation(x, y, z, x + Rnd.get(-100, 100), y + Rnd.get(-100, 100), z, getInstanceWorld());
		setXYZInvisible(location.getX(), location.getY(), location.getZ());
	}
	
	public override void onSpawn()
	{
		base.onSpawn();
		
		if (Config.SUMMON_STORE_SKILL_COOLTIME && !isTeleporting())
		{
			restoreEffects();
		}
		
		setFollowStatus(true);
		updateAndBroadcastStatus(0);
		
		if (_owner != null)
		{
			if (isPet())
			{
				sendPacket(new PetSummonInfo(this, 1));
				sendPacket(new ExPetSkillList(true, (Pet) this));
				if (getInventory() != null)
				{
					sendPacket(new PetItemList(getInventory().getItems()));
				}
			}
			sendPacket(new RelationChanged(this, _owner.getRelation(_owner), false));
			World.getInstance().forEachVisibleObject<Player>(getOwner(), player => player.sendPacket(new RelationChanged(this, _owner.getRelation(player), isAutoAttackable(player))));
		}
		
		Party party = _owner.getParty();
		if (party != null)
		{
			party.broadcastToPartyMembers(_owner, new ExPartyPetWindowAdd(this));
		}
		setShowSummonAnimation(false); // addVisibleObject created the info packets with summon animation
		// if someone comes into range now, the animation shouldn't show any more
		_restoreSummon = false;
		rechargeShots(true, true, false);
		
		// Notify to scripts
		if (EventDispatcher.getInstance().hasListener(EventType.ON_PLAYER_SUMMON_SPAWN, this))
		{
			EventDispatcher.getInstance().notifyEventAsync(new OnPlayerSummonSpawn(this), this);
		}
	}
	
	public override SummonStat getStat()
	{
		return (SummonStat)base.getStat();
	}
	
	public override void initCharStat()
	{
		setStat(new SummonStat(this));
	}
	
	public override SummonStatus getStatus()
	{
		return (SummonStatus) base.getStatus();
	}
	
	public override void initCharStatus()
	{
		setStatus(new SummonStatus(this));
	}
	
	protected override CreatureAI initAI()
	{
		return new SummonAI(this);
	}
	
	public override NpcTemplate getTemplate()
	{
		return (NpcTemplate)base.getTemplate();
	}
	
	// this defines the action buttons, 1 for Summon, 2 for Pets
	public abstract int getSummonType();
	
	public override void stopAllEffects()
	{
		base.stopAllEffects();
		updateAndBroadcastStatus(1);
	}
	
	public override void stopAllEffectsExceptThoseThatLastThroughDeath()
	{
		base.stopAllEffectsExceptThoseThatLastThroughDeath();
		updateAndBroadcastStatus(1);
	}
	
	public override void updateAbnormalVisualEffects()
	{
		if (_abnormalEffectTask == null)
		{
			_abnormalEffectTask = ThreadPool.schedule(() =>
			{
				if (isSpawned())
				{
					World.getInstance().forEachVisibleObject<Player>(this, player =>
					{
						if (player == _owner)
						{
							player.sendPacket(new PetSummonInfo(this, 1));
							return;
						}
						
						AbstractMaskPacket<NpcInfoType> packet;
						if (isPet())
						{
							packet = new ExPetInfo(this, player, 1);
						}
						else
						{
							packet = new SummonInfo(this, player, 1);
						}
						packet.addComponentType(NpcInfoType.ABNORMALS);
						player.sendPacket(packet);
					});
				}
				_abnormalEffectTask = null;
			}, 50);
		}
	}
	
	/**
	 * @return Returns the mountable.
	 */
	public bool isMountable()
	{
		return false;
	}
	
	public long getExpForThisLevel()
	{
		if (getLevel() >= ExperienceData.getInstance().getMaxPetLevel())
		{
			return 0;
		}
		return ExperienceData.getInstance().getExpForLevel(getLevel());
	}
	
	public long getExpForNextLevel()
	{
		if (getLevel() >= (ExperienceData.getInstance().getMaxPetLevel() - 1))
		{
			return 0;
		}
		return ExperienceData.getInstance().getExpForLevel(getLevel() + 1);
	}
	
	public override int getReputation()
	{
		return _owner != null ? _owner.getReputation() : 0;
	}
	
	public override bool getPvpFlag()
	{
		return _owner != null ? _owner.getPvpFlag() : 0;
	}
	
	public override Team getTeam()
	{
		return _owner != null ? _owner.getTeam() : Team.NONE;
	}
	
	public Player getOwner()
	{
		return _owner;
	}
	
	/**
	 * Gets the summon ID.
	 * @return the summon ID
	 */
	public override int getId()
	{
		return getTemplate().getId();
	}
	
	public short getSoulShotsPerHit()
	{
		if (getTemplate().getSoulShot() > 0)
		{
			return (short) getTemplate().getSoulShot();
		}
		return 1;
	}
	
	public short getSpiritShotsPerHit()
	{
		if (getTemplate().getSpiritShot() > 0)
		{
			return (short) getTemplate().getSpiritShot();
		}
		return 1;
	}
	
	public void followOwner()
	{
		setFollowStatus(true);
	}
	
	public override bool doDie(Creature killer)
	{
		if (isNoblesseBlessedAffected())
		{
			stopEffects(EffectFlag.NOBLESS_BLESSING);
			storeEffect(true);
		}
		else
		{
			storeEffect(false);
		}
		
		if (!base.doDie(killer))
		{
			return false;
		}
		
		if (_owner != null)
		{
			World.getInstance().forEachVisibleObject<Attackable>(this, target =>
			{
				if (target.isDead())
				{
					return;
				}
				
				AggroInfo info = target.getAggroList().get(this);
				if (info != null)
				{
					target.addDamageHate(_owner, info.getDamage(), info.getHate());
				}
			});
		}
		
		DecayTaskManager.getInstance().add(this);
		return true;
	}
	
	public bool doDie(Creature killer, bool decayed)
	{
		if (!base.doDie(killer))
		{
			return false;
		}
		if (!decayed)
		{
			DecayTaskManager.getInstance().add(this);
		}
		return true;
	}
	
	public void stopDecay()
	{
		DecayTaskManager.getInstance().cancel(this);
	}
	
	public override void onDecay()
	{
		unSummon(_owner);
		deleteMe(_owner);
	}
	
	public override void broadcastStatusUpdate(Creature caster)
	{
		base.broadcastStatusUpdate(caster);
		updateAndBroadcastStatus(1);
	}
	
	public void deleteMe(Player owner)
	{
		base.deleteMe();
		
		if (owner != null)
		{
			owner.sendPacket(new PetDelete(getSummonType(), getObjectId()));
			Party party = owner.getParty();
			if (party != null)
			{
				party.broadcastToPartyMembers(owner, new ExPartyPetWindowDelete(this));
			}
			
			if (isPet())
			{
				owner.setPet(null);
			}
			else
			{
				owner.removeServitor(getObjectId());
			}
		}
		
		// Pet will be deleted along with all his items.
		if (getInventory() != null)
		{
			// Pet related - Removed on Essence.
			// getInventory().destroyAllItems("pet deleted", _owner, this);
			// Pet related - Added the following.
			foreach (Item item in getInventory().getItems())
			{
				World.getInstance().removeObject(item);
			}
		}
		
		decayMe();
		
		if (!isPet())
		{
			CharSummonTable.getInstance().removeServitor(_owner, getObjectId());
		}
	}
	
	public void unSummon(Player owner)
	{
		if (isSpawned())
		{
			if (isDead())
			{
				stopDecay();
			}
			
			// Prevent adding effects while unsummoning.
			setInvul(true);
			
			abortAttack();
			abortCast();
			storeMe();
			storeEffect(true);
			
			// Stop AI tasks
			if (hasAI())
			{
				getAI().stopAITask(); // Calls stopFollow as well.
			}
			
			// Cancel running skill casters.
			abortAllSkillCasters();
			
			stopAllEffects();
			stopHpMpRegeneration();
			
			if (owner != null)
			{
				if (isPet())
				{
					getSkills().forEach((id, skill) => ((Pet) this).storePetSkills(id, skill.getLevel()));
					owner.setPet(null);
				}
				else
				{
					owner.removeServitor(getObjectId());
				}
				
				owner.sendPacket(new PetDelete(getSummonType(), getObjectId()));
				Party party = owner.getParty();
				if (party != null)
				{
					party.broadcastToPartyMembers(owner, new ExPartyPetWindowDelete(this));
				}
				
				if ((getInventory() != null) && (getInventory().getSize() > 0))
				{
					_owner.setPetInvItems(true);
					sendPacket(SystemMessageId.THERE_ARE_ITEMS_IN_THE_PET_S_INVENTORY_TAKE_THEM_OUT_FIRST);
				}
				else
				{
					_owner.setPetInvItems(false);
				}
			}
			
			ZoneRegion oldRegion = ZoneManager.getInstance().getRegion(this);
			decayMe();
			oldRegion.removeFromZones(this);
			
			setTarget(null);
			if (owner != null)
			{
				foreach (int itemId in owner.getAutoSoulShot())
				{
					String handler = ((EtcItem) ItemData.getInstance().getTemplate(itemId)).getHandlerName();
					if ((handler != null) && handler.Contains("Beast"))
					{
						owner.disableAutoShot(itemId);
					}
				}
			}
		}
	}
	
	public int getAttackRange()
	{
		return _attackRange;
	}
	
	public void setAttackRange(int range)
	{
		_attackRange = (range < 36) ? 36 : range;
	}
	
	public void setFollowStatus(bool value)
	{
		_follow = value;
		if (_follow)
		{
			getAI().setIntention(CtrlIntention.AI_INTENTION_FOLLOW, _owner);
		}
		else
		{
			getAI().setIntention(CtrlIntention.AI_INTENTION_IDLE);
		}
	}
	
	public bool getFollowStatus()
	{
		return _follow;
	}
	
	public override bool isAutoAttackable(Creature attacker)
	{
		return (_owner != null) && _owner.isAutoAttackable(attacker);
	}
	
	public int getControlObjectId()
	{
		return 0;
	}
	
	public Weapon getActiveWeapon()
	{
		return null;
	}
	
	public override PetInventory getInventory()
	{
		return null;
	}
	
	public void setRestoreSummon(bool value)
	{
	}
	
	public override Item getActiveWeaponInstance()
	{
		return null;
	}
	
	public override Weapon getActiveWeaponItem()
	{
		return null;
	}
	
	public override Item getSecondaryWeaponInstance()
	{
		return null;
	}
	
	public override Weapon getSecondaryWeaponItem()
	{
		return null;
	}
	
	/**
	 * Return True if the Summon is invulnerable or if the summoner is in spawn protection.
	 */
	public override bool isInvul()
	{
		return base.isInvul() || _owner.isSpawnProtected();
	}
	
	/**
	 * Return the Party object of its Player owner or null.
	 */
	public override Party getParty()
	{
		if (_owner == null)
		{
			return null;
		}
		return _owner.getParty();
	}
	
	/**
	 * Return True if the Creature has a Party in progress.
	 */
	public override bool isInParty()
	{
		return (_owner != null) && _owner.isInParty();
	}
	
	/**
	 * Check if the active Skill can be casted.<br>
	 * <br>
	 * <b><u>Actions</u>:</b>
	 * <ul>
	 * <li>Check if the target is correct</li>
	 * <li>Check if the target is in the skill cast range</li>
	 * <li>Check if the summon owns enough HP and MP to cast the skill</li>
	 * <li>Check if all skills are enabled and this skill is enabled</li>
	 * <li>Check if the skill is active</li>
	 * <li>Notify the AI with AI_INTENTION_CAST and target</li>
	 * </ul>
	 * @param skill The Skill to use
	 * @param forceUse used to force ATTACK on players
	 * @param dontMove used to prevent movement, if not in range
	 */
	public override bool useMagic(Skill skill, Item item, bool forceUse, bool dontMove)
	{
		// Null skill, dead summon or null owner are reasons to prevent casting.
		if ((skill == null) || isDead() || (_owner == null))
		{
			return false;
		}
		
		// Check if the skill is active
		if (skill.isPassive())
		{
			// just ignore the passive skill request. why does the client send it anyway ??
			return false;
		}
		
		// If a skill is currently being used
		if (isCastingNow(s => s.isAnyNormalType()))
		{
			return false;
		}
		
		// Get the target for the skill
		WorldObject target;
		if (skill.getTargetType() == TargetType.OWNER_PET)
		{
			target = _owner;
		}
		else
		{
			WorldObject currentTarget = _owner.getTarget();
			if (currentTarget != null)
			{
				target = skill.getTarget(this, forceUse && (!currentTarget.isPlayable() || !currentTarget.isInsideZone(ZoneId.PEACE) || !currentTarget.isInsideZone(ZoneId.NO_PVP)), dontMove, false);
				Player currentTargetPlayer = currentTarget.getActingPlayer();
				if (!forceUse && (currentTargetPlayer != null) && !currentTargetPlayer.isAutoAttackable(_owner))
				{
					sendPacket(SystemMessageId.INVALID_TARGET);
					return false;
				}
			}
			else
			{
				target = skill.getTarget(this, forceUse, dontMove, false);
			}
		}
		
		// Check the validity of the target
		if (target == null)
		{
			if (!isMovementDisabled())
			{
				setTarget(_owner.getTarget());
				target = skill.getTarget(this, forceUse, dontMove, false);
			}
			if (target == null)
			{
				sendPacket(SystemMessageId.YOUR_TARGET_CANNOT_BE_FOUND);
				return false;
			}
		}
		
		// Check if this skill is enabled (e.g. reuse time)
		if (isSkillDisabled(skill))
		{
			sendPacket(SystemMessageId.THAT_SERVITOR_SKILL_CANNOT_BE_USED_BECAUSE_IT_IS_RECHARGING);
			return false;
		}
		
		// Check if the summon has enough MP
		if (getCurrentMp() < (getStat().getMpConsume(skill) + getStat().getMpInitialConsume(skill)))
		{
			// Send a System Message to the caster
			sendPacket(SystemMessageId.NOT_ENOUGH_MP);
			return false;
		}
		
		// Check if the summon has enough HP
		if (getCurrentHp() <= skill.getHpConsume())
		{
			// Send a System Message to the caster
			sendPacket(SystemMessageId.NOT_ENOUGH_HP);
			return false;
		}
		
		// Check if all casting conditions are completed
		if (!skill.checkCondition(this, target, true))
		{
			// Send a Server->Client packet ActionFailed to the Player
			sendPacket(ActionFailedPacket.STATIC_PACKET);
			return false;
		}
		
		// Check if this is bad magic skill and if Player is in Olympiad and the match isn't already start, send a Server->Client packet ActionFailed
		if (skill.isBad() && _owner.isInOlympiadMode() && !_owner.isOlympiadStart())
		{
			sendPacket(ActionFailedPacket.STATIC_PACKET);
			return false;
		}
		
		// Notify the AI with AI_INTENTION_CAST and target
		getAI().setIntention(CtrlIntention.AI_INTENTION_CAST, skill, target);
		return true;
	}
	
	public override void setImmobilized(bool value)
	{
		base.setImmobilized(value);
		
		if (value)
		{
			_previousFollowStatus = _follow;
			// if immobilized temporarily disable follow mode
			if (_previousFollowStatus)
			{
				setFollowStatus(false);
			}
		}
		else
		{
			// if not more immobilized restore previous follow mode
			setFollowStatus(_previousFollowStatus);
		}
	}
	
	public void setOwner(Player newOwner)
	{
		_owner = newOwner;
	}
	
	public override void sendDamageMessage(Creature target, Skill skill, int damage, double elementalDamage, bool crit, bool miss, bool elementalCrit)
	{
		if (miss || (_owner == null))
		{
			return;
		}
		
		// Prevents the double spam of system messages, if the target is the owning player.
		if (target.getObjectId() != _owner.getObjectId())
		{
			if (crit)
			{
				if (isServitor())
				{
					sendPacket(SystemMessageId.SUMMONED_MONSTER_S_CRITICAL_HIT);
				}
				else
				{
					sendPacket(SystemMessageId.PET_S_CRITICAL_HIT);
				}
			}
			
			if (_owner.isInOlympiadMode() && target.isPlayer() && ((Player) target).isInOlympiadMode() && (((Player) target).getOlympiadGameId() == _owner.getOlympiadGameId()))
			{
				OlympiadGameManager.getInstance().notifyCompetitorDamage(getOwner(), damage);
			}
			
			SystemMessage sm;
			if ((target.isHpBlocked() && !target.isNpc()) || (target.isPlayer() && target.isAffected(EffectFlag.DUELIST_FURY) && !_owner.isAffected(EffectFlag.FACEOFF)))
			{
				sm = new SystemMessage(SystemMessageId.THE_ATTACK_HAS_BEEN_BLOCKED);
			}
			else
			{
				sm = new SystemMessage(SystemMessageId.C1_HAS_DEALT_S3_DAMAGE_TO_C2);
				sm.addNpcName(this);
				sm.addString(target.getName());
				sm.addInt(damage);
				sm.addPopup(target.getObjectId(), getObjectId(), (damage * -1));
			}
			
			sendPacket(sm);
		}
	}
	
	public override void reduceCurrentHp(double damage, Creature attacker, Skill skill)
	{
		base.reduceCurrentHp(damage, attacker, skill);
		
		if (!isDead() && !isHpBlocked() && (_owner != null) && (attacker != null) && (!_owner.isAffected(EffectFlag.DUELIST_FURY) || attacker.isAffected(EffectFlag.FACEOFF)))
		{
			SystemMessage sm = new SystemMessage(SystemMessageId.C1_HAS_RECEIVED_S3_DAMAGE_FROM_C2);
			sm.addNpcName(this);
			sm.addString(attacker.getName());
			sm.addInt((int) damage);
			sm.addPopup(getObjectId(), attacker.getObjectId(), (int) -damage);
			sendPacket(sm);
		}
	}
	
	public override void doCast(Skill skill)
	{
		if ((skill.getTarget(this, false, false, false) == null) && !_owner.getAccessLevel().allowPeaceAttack())
		{
			// Send a System Message to the Player
			_owner.sendPacket(SystemMessageId.THAT_IS_AN_INCORRECT_TARGET);
			
			// Send a Server->Client packet ActionFailed to the Player
			_owner.sendPacket(ActionFailedPacket.STATIC_PACKET);
			return;
		}
		
		base.doCast(skill);
	}
	
	public override bool isInCombat()
	{
		return (_owner != null) && _owner.isInCombat();
	}
	
	public override Player getActingPlayer()
	{
		return _owner;
	}
	
	public void updateAndBroadcastStatus(int value)
	{
		if (_owner == null)
		{
			return;
		}
		
		if (isSpawned())
		{
			sendPacket(new PetSummonInfo(this, value));
			sendPacket(new PetStatusUpdate(this));
			broadcastNpcInfo(value);
			
			Party party = _owner.getParty();
			if (party != null)
			{
				party.broadcastToPartyMembers(_owner, new ExPartyPetWindowUpdate(this));
			}
		}
	}
	
	public void broadcastNpcInfo(int value)
	{
		World.getInstance().forEachVisibleObject<Player>(this, player =>
		{
			if ((player == _owner))
			{
				return;
			}
			
			if (isPet())
			{
				player.sendPacket(new ExPetInfo(this, player, value));
			}
			else
			{
				player.sendPacket(new SummonInfo(this, player, value));
			}
		});
	}
	
	public bool isHungry()
	{
		return false;
	}
	
	public int getWeapon()
	{
		return 0;
	}
	
	public int getArmor()
	{
		return 0;
	}
	
	public override void sendInfo(Player player)
	{
		// Check if the Player is the owner of the Pet
		if (player == _owner)
		{
			player.sendPacket(new PetSummonInfo(this, isDead() ? 0 : 1));
			if (isPet())
			{
				player.sendPacket(new PetItemList(getInventory().getItems()));
			}
		}
		else
		{
			if (isPet())
			{
				player.sendPacket(new ExPetInfo(this, player, 0));
			}
			else
			{
				player.sendPacket(new SummonInfo(this, player, 0));
			}
		}
	}
	
	public override void onTeleported()
	{
		base.onTeleported();
		sendPacket(new TeleportToLocation(this, getX(), getY(), getZ(), getHeading()));
	}
	
	public override bool isUndead()
	{
		return getTemplate().getRace() == Race.UNDEAD;
	}
	
	/**
	 * Change the summon's state.
	 */
	public void switchMode()
	{
		// Do nothing.
	}
	
	/**
	 * Cancel the summon's action.
	 */
	public void cancelAction()
	{
		if (!isMovementDisabled())
		{
			getAI().setIntention(CtrlIntention.AI_INTENTION_ACTIVE);
		}
	}
	
	/**
	 * Performs an attack to the owner's target.
	 * @param target the target to attack.
	 */
	public void doAttack(WorldObject target)
	{
		if ((_owner != null) && (target != null))
		{
			setTarget(target);
			getAI().setIntention(CtrlIntention.AI_INTENTION_ATTACK, target);
			if (target.isFakePlayer() && !Config.FAKE_PLAYER_AUTO_ATTACKABLE)
			{
				_owner.updatePvPStatus();
			}
		}
	}
	
	/**
	 * Verify if the summon can perform an attack.
	 * @param target the target to check if can be attacked.
	 * @param ctrlPressed {@code true} if Ctrl key is pressed
	 * @return {@code true} if the summon can attack, {@code false} otherwise
	 */
	public bool canAttack(WorldObject target, bool ctrlPressed)
	{
		if (_owner == null)
		{
			return false;
		}
		
		if ((target == null) || (this == target) || (_owner == target))
		{
			return false;
		}
		
		// Sin eater, Big Boom, Wyvern can't attack with attack button.
		int npcId = getId();
		if (CommonUtil.contains(PASSIVE_SUMMONS, npcId))
		{
			_owner.sendPacket(ActionFailedPacket.STATIC_PACKET);
			return false;
		}
		
		if (isBetrayed())
		{
			sendPacket(SystemMessageId.YOUR_SERVITOR_IS_UNRESPONSIVE_AND_WILL_NOT_OBEY_ANY_ORDERS);
			sendPacket(ActionFailedPacket.STATIC_PACKET);
			return false;
		}
		
		if (isPet() && ((getLevel() - _owner.getLevel()) > 20))
		{
			sendPacket(SystemMessageId.YOUR_PET_IS_TOO_HIGH_LEVEL_TO_CONTROL);
			sendPacket(ActionFailedPacket.STATIC_PACKET);
			return false;
		}
		
		if (_owner.isInOlympiadMode() && !_owner.isOlympiadStart())
		{
			// If owner is in Olympiad and the match isn't already start, send a Server->Client packet ActionFailed
			_owner.sendPacket(ActionFailedPacket.STATIC_PACKET);
			return false;
		}
		
		if (_owner.isSiegeFriend(target))
		{
			sendPacket(SystemMessageId.FORCE_ATTACK_IS_IMPOSSIBLE_AGAINST_A_TEMPORARY_ALLIED_MEMBER_DURING_A_SIEGE);
			sendPacket(ActionFailedPacket.STATIC_PACKET);
			return false;
		}
		
		if (!_owner.getAccessLevel().allowPeaceAttack() && _owner.isInsidePeaceZone(this, target))
		{
			sendPacket(SystemMessageId.YOU_CANNOT_ATTACK_THIS_TARGET_IN_A_PEACEFUL_ZONE);
			return false;
		}
		
		if (isLockedTarget())
		{
			sendPacket(SystemMessageId.FAILED_TO_CHANGE_ENMITY);
			return false;
		}
		
		// Summons can attack NPCs even when the owner cannot.
		if (!target.isAutoAttackable(_owner) && !ctrlPressed && !target.isNpc())
		{
			setFollowStatus(false);
			getAI().setIntention(CtrlIntention.AI_INTENTION_FOLLOW, target);
			sendPacket(SystemMessageId.INVALID_TARGET);
			return false;
		}
		
		// Siege golems AI doesn't support attacking other than doors/walls at the moment.
		if (target.isDoor() && (getTemplate().getRace() != Race.SIEGE_WEAPON))
		{
			return false;
		}
		
		return true;
	}
	
	public override void sendPacket(ServerPacket packet)
	{
		if (_owner != null)
		{
			_owner.sendPacket(packet);
		}
	}
	
	public override void sendPacket(SystemMessageId id)
	{
		if (_owner != null)
		{
			_owner.sendPacket(id);
		}
	}
	
	public override bool isSummon()
	{
		return true;
	}
	
	public override void rechargeShots(bool physical, bool magic, bool fish)
	{
		Item item;
		IItemHandler handler;
		if ((_owner.getAutoSoulShot() == null) || _owner.getAutoSoulShot().isEmpty())
		{
			return;
		}
		
		foreach (int itemId in _owner.getAutoSoulShot())
		{
			item = _owner.getInventory().getItemByItemId(itemId);
			if (item != null)
			{
				if (magic && ((item.getTemplate().getDefaultAction() == ActionType.SPIRITSHOT) || /* Old BeastSpiritShot item support. */ (item.getTemplate().getDefaultAction() == ActionType.SUMMON_SPIRITSHOT)))
				{
					handler = ItemHandler.getInstance().getHandler(item.getEtcItem());
					if (handler != null)
					{
						handler.useItem(_owner, item, false);
					}
				}
				
				if (physical && ((item.getTemplate().getDefaultAction() == ActionType.SOULSHOT) || /* Old BeastSoulShot item support. */ (item.getTemplate().getDefaultAction() == ActionType.SUMMON_SOULSHOT)))
				{
					handler = ItemHandler.getInstance().getHandler(item.getEtcItem());
					if (handler != null)
					{
						handler.useItem(_owner, item, false);
					}
				}
			}
			else
			{
				_owner.removeAutoSoulShot(itemId);
			}
		}
	}
	
	public override int getClanId()
	{
		return (_owner != null) ? _owner.getClanId() : 0;
	}
	
	public override int getAllyId()
	{
		return (_owner != null) ? _owner.getAllyId() : 0;
	}
	
	public void setSummonPoints(int summonPoints)
	{
		_summonPoints = summonPoints;
	}
	
	public int getSummonPoints()
	{
		return _summonPoints;
	}
	
	public void sendInventoryUpdate(PetInventoryUpdate iu)
	{
		Player owner = _owner;
		if (owner != null)
		{
			owner.sendPacket(iu);
			if (getInventory() != null)
			{
				owner.sendPacket(new PetItemList(getInventory().getItems()));
			}
			owner.sendPacket(new PetSummonInfo(this, 1));
		}
	}
	
	public override bool isMovementDisabled()
	{
		return base.isMovementDisabled() || !getTemplate().canMove();
	}
	
	public override bool isTargetable()
	{
		return base.isTargetable() && getTemplate().isTargetable();
	}
	
	public override bool isOnEvent()
	{
		return (_owner != null) && _owner.isOnEvent();
	}
	
	public override String ToString()
	{
		StringBuilder sb = new();
		sb.Append(base.ToString());
		sb.Append("(");
		sb.Append(getId());
		sb.Append(") Owner: ");
		sb.Append(_owner);
		return sb.ToString();
	}
}