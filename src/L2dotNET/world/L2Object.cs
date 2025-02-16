﻿using System.Collections.Generic;
using System.Linq;
using System.Timers;
using L2dotNET.model.player;
using L2dotNET.model.skills2;
using L2dotNET.model.zones;
using L2dotNET.model.zones.classes;
using L2dotNET.Models;
using L2dotNET.Network;
using L2dotNET.Network.serverpackets;

namespace L2dotNET.world
{
    public abstract class L2Object
    {
        public int ObjId;
        public SortedList<int, L2Object> KnownObjects = new SortedList<int, L2Object>();
        public virtual byte Level { get; set; } = 1;
        public virtual double CurHp { get; set; }
        public virtual double CurMp { get; set; }
        public virtual double CurCp { get; set; }
        public virtual bool Dead { get; set; } = false;
        public virtual int X { get; set; }
        public virtual int Y { get; set; }
        public virtual int Z { get; set; }
        public virtual int DestX { get; set; }
        public virtual int DestY { get; set; }
        public virtual int DestZ { get; set; }
        public virtual int Heading { get; set; }
        public virtual int TeamId { get; set; }
        public virtual bool Visible { get; set; } = true;
        public virtual string CurrentRegion { get; set; }
        public byte ObjectSummonType = 0;
        public virtual L2WorldRegion Region { get; set; }

        public virtual void OnAction(L2Player player) { }

        public virtual void OnActionShift(L2Player player)
        {
            OnAction(player);
        }

        public virtual void OnForcedAttack(L2Player player) { }

        public virtual void SendPacket(GameserverPacket pk) { }

        public virtual void AddAbnormal(Skill skill, L2Character caster, bool permanent, bool unlim) { }

        public virtual void OnRemObject(L2Object obj) { }

        public virtual void OnAddObject(L2Object obj, GameserverPacket pk, string msg = null) { }

        public virtual void BroadcastUserInfo() { }

        public virtual void NotifyAction(L2Player player) { }

        public virtual void StartAi() { }

        public virtual void AddKnownObject(L2Object obj) { }

        public virtual void RemoveKnownObject(L2Object obj) { }

        public virtual void SendInfo(L2Player player) { }

        public virtual void OnSpawn()
        {
        }

        public virtual void BroadcastPacket(GameserverPacket pk, bool excludeYourself)
        {
            if (!excludeYourself)
                SendPacket(pk);

            GetKnownPlayers().ForEach(p => p.SendPacket(pk));
        }

        public virtual void BroadcastPacket(GameserverPacket pk)
        {
            BroadcastPacket(pk, false);
        }

        public virtual void ReduceHp(L2Character attacker, double damage) { }

        public virtual void DecayMe()
        {
            Region = null;

            L2World.Instance.RemoveObject(this);
        }

        public void ClearKnowns(bool deleteMe, params int[] exclude)
        {
            foreach (L2Object o in KnownObjects.Values)
            {
                o.OnClearing(this, deleteMe);

                if (deleteMe && this is L2Player)
                    SendPacket(new DeleteObject(o.ObjId));
            }

            KnownObjects.Clear();
        }

        public void GetKnowns(int range, int height, bool zones)
        {
            L2World.Instance.GetObjects(); // GetKnowns(this, range, height, zones);
        }

        public virtual List<L2Player> GetKnownPlayers()
        {
            L2WorldRegion region = Region;
            if (region == null)
                return new List<L2Player>();

            List<L2Player> result = new List<L2Player>();

            region.GetSurroundingRegions().ForEach(reg => result.AddRange(L2World.Instance.GetPlayers().Where(obj => obj != this)));

            return result;
        }

        public virtual void SetRegion(L2WorldRegion newRegion)
        {
            List<L2WorldRegion> oldAreas = new List<L2WorldRegion>();

            if (Region != null)
            {
                Region.RemoveVisibleObject(this);
                oldAreas = Region.GetSurroundingRegions();
            }

            List<L2WorldRegion> newAreas = new List<L2WorldRegion>();

            if (newRegion != null)
            {
                newRegion.AddVisibleObject(this);
                newAreas = newRegion.GetSurroundingRegions();
            }

            foreach (L2WorldRegion region in oldAreas.Where(region => !newAreas.Contains(region)))
            {
                foreach (L2Object obj in region.GetObjects().Where(obj => obj != this))
                {
                    obj.RemoveKnownObject(this);
                    RemoveKnownObject(obj);
                }

                if (this is L2Player && region.IsEmptyNeighborhood())
                    region.SetActive(false);
            }

            foreach (L2WorldRegion region in newAreas.Where(region => !oldAreas.Contains(region)))
            {
                // Update all objects.
                foreach (L2Object obj in region.GetObjects().Where(obj => obj != this))
                {
                    obj.AddKnownObject(this);
                    AddKnownObject(obj);
                }

                // Activate the new neighbor region.
                if (this is L2Player)
                    region.SetActive(true);
            }

            Region = newRegion;
        }

        private void OnClearing(L2Object target, bool deleteMe)
        {
            lock (KnownObjects)
                KnownObjects.Remove(target.ObjId);

            if (deleteMe && target is L2Player)
                target.SendPacket(new DeleteObject(ObjId));
        }

        public void SetVisible(bool val)
        {
            Visible = val;
            foreach (L2Object o in KnownObjects.Values)
                o.CanView(this);
        }

        private void CanView(L2Object target)
        {
            foreach (L2Object o in KnownObjects.Values)
                o.OnClearing(this, true);
        }

        public void AddKnownObject(L2Object obj, GameserverPacket pk, bool pkuse)
        {
            if (KnownObjects.ContainsKey(obj.ObjId))
                return;

            KnownObjects.Add(obj.ObjId, obj);

            if (!obj.Visible)
                return;

            if (pkuse)
                OnAddObject(obj, pk);
        }

        public void UpdateVisibleStatus()
        {
            foreach (L2Object o in KnownObjects.Values.Where(o => o.Visible))
                OnAddObject(o, null);
        }

        public void RemoveKnownObject(L2Object obj, bool update)
        {
            if (!KnownObjects.ContainsKey(obj.ObjId))
                return;

            OnRemObject(obj);

            lock (KnownObjects)
                KnownObjects.Remove(obj.ObjId);
        }

        public void Revalidate(L2Object obj)
        {
            if (KnownObjects.ContainsKey(obj.ObjId))
                return;

            KnownObjects.Add(obj.ObjId, obj);

            if (obj.Visible)
                OnAddObject(obj, null);
        }

        public bool IsInsideRadius(L2Object o, int radius, bool checkZ, bool strictCheck)
        {
            return IsInsideRadius(o.X, o.Y, o.Z, radius, checkZ, strictCheck);
        }

        public bool IsInsideRadius(int x, int y, int radius, bool strictCheck)
        {
            return IsInsideRadius(x, y, 0, radius, false, strictCheck);
        }

        public bool IsInsideRadius(int x, int y, int z, int radius, bool checkZ, bool strictCheck)
        {
            double dx = x - X;
            double dy = y - Y;
            double dz = z - Z;

            if (strictCheck)
            {
                if (checkZ)
                    return ((dx * dx) + (dy * dy) + (dz * dz)) < (radius * radius);

                return ((dx * dx) + (dy * dy)) < (radius * radius);
            }

            if (checkZ)
                return ((dx * dx) + (dy * dy) + (dz * dz)) <= (radius * radius);

            return ((dx * dx) + (dy * dy)) <= (radius * radius);
        }

        public SortedList<int, L2Zone> ActiveZones = new SortedList<int, L2Zone>();
        private bool _isInsidePeaceZone,
                     _isInsidePvpZone,
                     _isInsideWaterZone;
        //private bool _isInsideSSQZone = false;
        private const bool IsInsideSiegeZone = false;
        private const bool IsInsideSomeDungeon = false;

        public bool IsInDanger = false;

        public bool IsInSiege()
        {
            return IsInsideSiegeZone;
        }

        public bool IsInDungeon()
        {
            return IsInsideSomeDungeon;
        }

        public int LastCode = -1;
        private bool _forceSetPvp;
        public bool IsInCombat = false;

        public void SetForcedPvpZone(bool val)
        {
            _forceSetPvp = val;
            ValidateZoneCompass();
            ValidateBattleZones();
        }

        public virtual void ValidateZoneCompass()
        {
            if (_forceSetPvp)
            {
                if (LastCode != ExSetCompassZoneCode.Pvpzone)
                {
                    LastCode = ExSetCompassZoneCode.Pvpzone;
                    SendPacket(new ExSetCompassZoneCode(ExSetCompassZoneCode.Pvpzone));
                    return;
                }
            }

            int code;
            if (_isInsidePvpZone)
                code = ExSetCompassZoneCode.Pvpzone;
            else
                code = _isInsidePeaceZone ? ExSetCompassZoneCode.Peacezone : ExSetCompassZoneCode.Generalzone;

            if (code == 0)
                return;

            if ((LastCode != -1) && (LastCode != code))
            {
                LastCode = code;
                SendPacket(new ExSetCompassZoneCode(code));
            }
            else
            {
                LastCode = code;
                SendPacket(new ExSetCompassZoneCode(code));
            }
        }

        public void OnEnterZone(L2Zone z)
        {
            if (ActiveZones.ContainsKey(z.ZoneId))
                return;

            if (this is L2Player)
                ((L2Player)this).SendMessage($"entered zone {z.Name}");

            ActiveZones.Add(z.ZoneId, z);
            z.OnEnter(this);

            RevalidateZone(z);
            ValidateZoneCompass();
        }

        public void OnExitZone(L2Zone z, bool cls)
        {
            if (!ActiveZones.ContainsKey(z.ZoneId))
                return;

            lock (ActiveZones)
                ActiveZones.Remove(z.ZoneId);

            z.OnExit(this, cls);

            RevalidateZone(z);
            ValidateZoneCompass();
        }

        private void RevalidateZone(L2Zone z)
        {
            if (z is peace_zone)
                ValidatePeaceZones();
            else
            {
                if (z is battle_zone)
                    ValidateBattleZones();
                else
                {
                    if (z is water)
                        ValidateWaterZones();
                }
            }
        }

        public bool IsInBattle()
        {
            return _isInsidePvpZone;
        }

        public bool IsInPeace()
        {
            return !_isInsidePvpZone && _isInsidePeaceZone;
        }

        public bool IsInWater()
        {
            return _isInsideWaterZone;
        }

        public bool isInCombat()
        {
            return IsInCombat;
        }

        public void ValidatePeaceZones()
        {
            bool found = false,
                 old = _isInsidePeaceZone;
            if (ActiveZones.Values.OfType<peace_zone>().Any())
            {
                _isInsidePeaceZone = true;
                found = true;
            }

            if (!found)
                _isInsidePeaceZone = false;

            if (!old && _isInsidePeaceZone)
            {
                if (this is L2Player)
                    ((L2Player)this).SendSystemMessage(SystemMessage.SystemMessageId.EnterPeacefulZone);
            }
            else
            {
                if (!old || _isInsidePeaceZone)
                    return;

                if (this is L2Player)
                    ((L2Player)this).SendSystemMessage(SystemMessage.SystemMessageId.ExitPeacefulZone);
            }
        }

        public void ValidateBattleZones()
        {
            bool found = false,
                 old = _isInsidePvpZone;
            if (!_forceSetPvp)
            {
                if (ActiveZones.Values.OfType<battle_zone>().Any())
                {
                    _isInsidePvpZone = true;
                    found = true;
                }
            }
            else
            {
                old = false;
                _isInsidePvpZone = true;
                found = true;
            }

            if (!found)
                _isInsidePvpZone = false;

            if (!old && _isInsidePvpZone)
            {
                if (this is L2Player)
                    ((L2Player)this).SendSystemMessage(SystemMessage.SystemMessageId.EnteredCombatZone);
            }
            else
            {
                if (!old || _isInsidePvpZone)
                    return;

                if (this is L2Player)
                    ((L2Player)this).SendSystemMessage(SystemMessage.SystemMessageId.LeftCombatZone);
            }
        }

        public virtual void SpawnMe()
        {
            Region = L2World.Instance.GetRegion(new Location(X, Y, Z));

            L2World.Instance.AddObject(this);

            OnSpawn();
        }

        public void ValidateWaterZones()
        {
            //bool found = false;
            //foreach (L2Zone z in _activeZones.Values)
            //{
            //    if (z is water)
            //    {
            //        _isInsideWaterZone = true;
            //        found = true;
            //        break;
            //    }
            //}

            //if(!found)

            _isInsideWaterZone = (Z > -4779) && (Z < -3779);

            if (this is L2Player)
                ((L2Player)this).WaterTimer();
        }

        public void ValidateVisibleObjects(int x, int y, bool zones)
        {
            //int range = 4000;
            //int height = 1600;

            if (IsInSiege())
            {
                //range = 2600;
                //height = 1000;
            }

            //L2World.Instance.CheckToUpdate(this, x, y, range, height, true, zones);
        }

        public Timer RegenerationMethod_1S,
                     RegenUpdate;
        public int RegenUpdateInterval = 3000;

        public virtual void StartRegeneration()
        {
            if (RegenerationMethod_1S == null)
            {
                RegenerationMethod_1S = new Timer
                {
                    Interval = 1000
                };
                RegenerationMethod_1S.Elapsed += new ElapsedEventHandler(RegenTaskDone);
            }

            if (RegenUpdate == null)
            {
                RegenUpdate = new Timer
                {
                    Interval = RegenUpdateInterval
                };
                RegenUpdate.Elapsed += new ElapsedEventHandler(RegenUpdateTaskDone);
            }

            RegenerationMethod_1S.Enabled = true;
            RegenUpdate.Enabled = true;
        }

        public virtual void RegenTaskDone(object sender, ElapsedEventArgs e) { }

        public virtual void RegenUpdateTaskDone(object sender, ElapsedEventArgs e) { }

        public void StopRegeneration()
        {
            if (RegenerationMethod_1S != null)
                RegenerationMethod_1S.Enabled = false;

            if (RegenUpdate != null)
                RegenUpdate.Enabled = false;
        }

        public virtual double Radius => 11;

        public virtual double Height => 22;

        public virtual string AsString()
        {
            return $"L2Object: {ObjId}";
        }
    }
}