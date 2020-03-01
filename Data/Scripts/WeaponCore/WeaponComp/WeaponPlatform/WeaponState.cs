﻿using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        public void PositionChanged(MyPositionComponentBase pComp)
        {
            try
            {
                if (_posChangedTick != Comp.Session.Tick)
                    UpdatePivotPos();

                _posChangedTick = Comp.Session.Tick;
            }
            catch (Exception ex) { Log.Line($"Exception in PositionChanged: {ex}"); }
        }

        public void UpdateParts(MyPositionComponentBase pComp)
        {
            if (_azimuthSubpartUpdateTick == Comp.Session.Tick) return;
            _azimuthSubpartUpdateTick = Comp.Session.Tick;

            var matrix = AzimuthPart.Entity.WorldMatrix;
            foreach (var part in AzimuthPart.Entity.Subparts)
            {
                if(!part.Key.Contains(System.AzimuthPartName.String))
                    part.Value.PositionComp.UpdateWorldMatrix(ref matrix);
            }
        }

        internal void EntPartClose(MyEntity obj)
        {
            obj.PositionComp.OnPositionChanged -= PositionChanged;
            obj.OnMarkForClose -= EntPartClose;

            if (Comp.Session.VanillaSubpartNames.Contains(System.AzimuthPartName.String) && Comp.Session.VanillaSubpartNames.Contains(System.ElevationPartName.String))
                obj.PositionComp.OnPositionChanged -= UpdateParts;
        }

        internal void EventTriggerStateChanged(EventTriggers state, bool active, HashSet<string> muzzles = null)
        {
            var session = Comp.Session;
            var canPlay = !session.DedicatedServer && session.SyncBufferedDistSqr >= Vector3D.DistanceSquared(MyAPIGateway.Session.Player.GetPosition(), MyPivotPos);

            switch (state)
            {
                case EventTriggers.StopFiring:
                case EventTriggers.PreFire:
                case EventTriggers.Firing:
                    if (AnimationsSet.ContainsKey(state))
                    {
                        var addToFiring = AnimationsSet.ContainsKey(EventTriggers.StopFiring) && state == EventTriggers.Firing;
                        uint delay = 0;
                        if (active)
                        {
                            if (state == EventTriggers.StopFiring)
                            {
                                Timings.ShootDelayTick = System.WeaponAnimationLengths[EventTriggers.StopFiring] + session.Tick;
                                if (LastEvent == EventTriggers.Firing || LastEvent == EventTriggers.PreFire)
                                {
                                    if (CurLgstAnimPlaying != null && CurLgstAnimPlaying.Running)
                                    {
                                        delay = CurLgstAnimPlaying.Reverse ? (uint)CurLgstAnimPlaying.CurrentMove : (uint)((CurLgstAnimPlaying.NumberOfMoves - 1) - CurLgstAnimPlaying.CurrentMove);
                                        Timings.ShootDelayTick += delay;
                                    }
                                }
                            }
                        }

                        for (int i = 0; i < AnimationsSet[state].Length; i++)
                        {
                            var animation = AnimationsSet[state][i];

                            if (active && !animation.Running && (animation.Muzzle == "Any" || muzzles != null && muzzles.Contains(animation.Muzzle)))
                            {
                                if (animation.TriggerOnce && animation.Triggered) continue;
                                animation.Triggered = true;

                                if (CurLgstAnimPlaying == null || CurLgstAnimPlaying.EventTrigger != state || animation.NumberOfMoves > CurLgstAnimPlaying.NumberOfMoves)
                                    CurLgstAnimPlaying = animation;

                                if (animation.Muzzle != "Any" && addToFiring) _muzzlesFiring.Add(animation.Muzzle);

                                animation.StartTick = session.Tick + animation.MotionDelay + delay;
                                Comp.Session.AnimationsToProcess.Add(animation);
                                animation.Running = true;
                                //animation.Paused = Comp.ResettingSubparts;
                                animation.CanPlay = canPlay;

                                if (animation.DoesLoop)
                                    animation.Looping = true;
                            }
                            else if (active && animation.DoesLoop)
                                animation.Looping = true;
                            else if (!active)
                            {
                                animation.Looping = false;
                                animation.Triggered = false;
                            }
                        }
                        if (active && state == EventTriggers.StopFiring)
                            _muzzlesFiring.Clear();
                    }
                    break;
                case EventTriggers.StopTracking:
                case EventTriggers.Tracking:
                    if (AnimationsSet.ContainsKey(state))
                    {
                        var oppositeEvnt = state == EventTriggers.Tracking ? EventTriggers.StopTracking : EventTriggers.Tracking;
                        //if (active) LastEvent = state;
                        for (int i = 0; i < AnimationsSet[state].Length; i++)
                        {
                            var animation = AnimationsSet[state][i];
                            if (active && !animation.Running)
                            {
                                if (animation.TriggerOnce && animation.Triggered) continue;
                                animation.Triggered = true;

                                if (CurLgstAnimPlaying == null || CurLgstAnimPlaying.EventTrigger != state || animation.NumberOfMoves > CurLgstAnimPlaying.NumberOfMoves)
                                    CurLgstAnimPlaying = animation;

                                PartAnimation animCheck;
                                animation.Running = true;
                                //animation.Paused = Comp.ResettingSubparts;
                                animation.CanPlay = canPlay;
                                if (AnimationLookup.TryGetValue(
                                    animation.EventIdLookup[oppositeEvnt], out animCheck) && animCheck.Running)
                                {
                                    animCheck.Reverse = true;

                                    if (!animation.DoesLoop)
                                        animation.Running = false;
                                    else
                                    {
                                        animation.StartTick = Comp.Session.Tick + (uint)animCheck.CurrentMove + animation.MotionDelay;
                                        Comp.Session.AnimationsToProcess.Add(animation);
                                    }
                                }
                                else
                                {
                                    Comp.Session.AnimationsToProcess.Add(animation);
                                    animation.StartTick = session.Tick + animation.MotionDelay;
                                }

                                if (animation.DoesLoop)
                                    animation.Looping = true;
                            }
                            else if (active && animation.DoesLoop)
                                animation.Looping = true;
                            else if (!active)
                            {
                                animation.Looping = false;
                                animation.Triggered = false;
                            }
                        }
                    }
                    break;
                case EventTriggers.TurnOn:
                case EventTriggers.TurnOff:
                    if (active && AnimationsSet.ContainsKey(state))
                    {
                        var oppositeEvnt = state == EventTriggers.TurnOff ? EventTriggers.TurnOn : EventTriggers.TurnOff;

                        if ((state == EventTriggers.TurnOn && !Comp.State.Value.Online) || state == EventTriggers.TurnOff && Comp.State.Value.Online) return;

                        //LastEvent = state;
                        for (int i = 0; i < AnimationsSet[state].Length; i++)
                        {
                            var animation = AnimationsSet[state][i];
                            if (!animation.Running)
                            {
                                if (CurLgstAnimPlaying == null || CurLgstAnimPlaying.EventTrigger != state || animation.NumberOfMoves > CurLgstAnimPlaying.NumberOfMoves)
                                    CurLgstAnimPlaying = animation;

                                PartAnimation animCheck;
                                animation.Running = true;
                                animation.CanPlay = true;
                                //animation.Paused = Comp.ResettingSubparts;
                                string eventName;
                                if (animation.EventIdLookup.TryGetValue(oppositeEvnt, out eventName) && AnimationLookup.TryGetValue(eventName, out animCheck))
                                {
                                    if (animCheck.Running)
                                    {
                                        animCheck.Reverse = true;
                                        animation.Running = false;
                                    }
                                    else
                                        session.ThreadedAnimations.Enqueue(animation);
                                }
                                else
                                    session.ThreadedAnimations.Enqueue(animation);

                                animation.StartTick = session.Tick + animation.MotionDelay;
                                if (state == EventTriggers.TurnOff) animation.StartTick += Timings.OffDelay;
                            }
                            else
                                animation.Reverse = false;
                        }
                    }
                    break;
                case EventTriggers.EmptyOnGameLoad:
                case EventTriggers.Overheated:
                case EventTriggers.OutOfAmmo:
                case EventTriggers.BurstReload:
                case EventTriggers.Reloading:
                    if (AnimationsSet.ContainsKey(state))
                    {
                        //if (active) LastEvent = state;
                        for (int i = 0; i < AnimationsSet[state].Length; i++)
                        {
                            var animation = AnimationsSet[state][i];
                            if (active && !animation.Running)
                            {
                                if (animation.TriggerOnce && animation.Triggered) continue;
                                animation.Triggered = true;

                                if (CurLgstAnimPlaying == null || CurLgstAnimPlaying.EventTrigger != state || animation.NumberOfMoves > CurLgstAnimPlaying.NumberOfMoves)
                                    CurLgstAnimPlaying = animation;

                                animation.StartTick = session.Tick + animation.MotionDelay;
                                session.ThreadedAnimations.Enqueue(animation);

                                animation.Running = true;
                                animation.CanPlay = canPlay;
                                //animation.Paused = Comp.ResettingSubparts;

                                if (animation.DoesLoop)
                                    animation.Looping = true;
                            }
                            else if (active && animation.DoesLoop)
                                animation.Looping = true;
                            else if (!active)
                            {
                                animation.Looping = false;
                                animation.Triggered = false;
                            }
                        }
                    }
                    break;
            }
            if(active)
                LastEvent = state;
        }

        internal void UpdateRequiredPower()
        {
            if (System.EnergyAmmo || System.IsHybrid)
            {
                var rofPerSecond = RateOfFire / MyEngineConstants.UPDATE_STEPS_PER_SECOND;
                RequiredPower = ((ShotEnergyCost * (rofPerSecond * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS)) * System.Values.HardPoint.Loading.BarrelsPerShot) * System.Values.HardPoint.Loading.TrajectilesPerBarrel;
            }
            else
                RequiredPower = Comp.IdlePower;
        }

        internal void UpdateShotEnergy()
        {
            var ewar = (int)System.Values.Ammo.AreaEffect.AreaEffect > 3;
            ShotEnergyCost = ewar ? System.Values.HardPoint.EnergyCost * AreaEffectDmg : System.Values.HardPoint.EnergyCost * BaseDamage;
        }

        internal void UpdateBarrelRotation()
        {
            const int loopCnt = 10;
            var interval = (3600f / System.BarrelSpinRate) * ((float)Math.PI / _numModelBarrels);
            var steps = (360f / _numModelBarrels) / interval;

            _ticksBeforeSpinUp = (uint)interval / loopCnt;
            for (int i = 0; i < loopCnt; i++)
            {

                var multi = (float)(i + 1) / loopCnt;
                var angle = MathHelper.ToRadians(steps * multi);

                switch (System.Values.HardPoint.RotateBarrelAxis)
                {

                    case 1:
                        BarrelRotationPerShot[i] = MuzzlePart.ToTransformation * Matrix.CreateRotationX(angle) * MuzzlePart.FromTransformation;
                        break;
                    case 2:
                        BarrelRotationPerShot[i] = MuzzlePart.ToTransformation * Matrix.CreateRotationY(angle) * MuzzlePart.FromTransformation;
                        break;
                    case 3:
                        BarrelRotationPerShot[i] = MuzzlePart.ToTransformation * Matrix.CreateRotationZ(angle) * MuzzlePart.FromTransformation;
                        break;
                }
            }
        }

        public void StartShooting()
        {
            CeaseFireDelayTick = Comp.Session.Tick;
            if (FiringEmitter != null) StartFiringSound();
            if (!IsShooting && !System.DesignatorWeapon)
            {
                EventTriggerStateChanged(EventTriggers.StopFiring, false);
                Comp.CurrentDps += Dps;
                if ((System.EnergyAmmo || System.IsHybrid) && !System.MustCharge && !Comp.UnlimitedPower && !DrawingPower)
                    DrawPower();

            }
            IsShooting = true;
        }

        public void StopShooting(bool avOnly = false, bool power = true)
        {
            StopFiringSound(false);
            StopPreFiringSound(false);
            CeaseFireDelayTick = uint.MaxValue;
            if (!power || avOnly) StopRotateSound();
            for (int i = 0; i < Muzzles.Length; i++)
            {
                var muzzle = Muzzles[i];
                muzzle.Av1Looping = false;
                muzzle.LastAv1Tick = Comp.Session.Tick;
                muzzle.Av2Looping = false;
                muzzle.LastAv2Tick = Comp.Session.Tick;

            }
            if (!avOnly)
            {
                _ticksUntilShoot = 0;
                Comp.State.Value.Weapons[WeaponId].SingleShotCounter = 0;
                PreFired = false;
                if (IsShooting && !System.DesignatorWeapon)
                {
                    EventTriggerStateChanged(EventTriggers.Firing, false);
                    EventTriggerStateChanged(EventTriggers.StopFiring, true, _muzzlesFiring);
                    Comp.CurrentDps = Comp.CurrentDps - Dps > 0 ? Comp.CurrentDps - Dps : 0;

                    if (!System.MustCharge && (System.EnergyAmmo || System.IsHybrid) && !Comp.UnlimitedPower && power && DrawingPower)
                        StopPowerDraw();
                    else if (System.MustCharge)
                    {
                        State.Sync.CurrentAmmo = 0;
                        Comp.State.Value.CurrentCharge -= State.Sync.CurrentCharge;
                        State.Sync.CurrentCharge = 0;
                        if (Comp.State.Value.Online)
                            StartReload();
                    }

                }
                IsShooting = false;
            }
        }

        public void DrawPower(bool adapt = false)
        {
            if (DrawingPower && !adapt) return;

            var useableDif = adapt ? OldUseablePower - UseablePower : -UseablePower;
            DrawingPower = true;
            //yes they are the right signs, weird math at play :P
            Comp.Ai.CurrentWeaponsDraw -= useableDif;
            Comp.SinkPower -= useableDif;
            Comp.Ai.GridAvailablePower += useableDif;
            Comp.MyCube.ResourceSink.Update();
        }

        public void StopPowerDraw()
        {
            if (!DrawingPower) return;
            DrawingPower = false;
            RequestedPower = false;
            Comp.Ai.RequestedWeaponsDraw -= RequiredPower;
            Comp.Ai.CurrentWeaponsDraw -= UseablePower;
            Comp.SinkPower -= UseablePower;
            Comp.Ai.GridAvailablePower += UseablePower;

            Timings.ChargeDelayTicks = 0;
            if (Comp.SinkPower < Comp.IdlePower) Comp.SinkPower = Comp.IdlePower;
            Comp.MyCube.ResourceSink.Update();
        }

        public void StartReload(bool reset = false)
        {            
            if (reset) State.Sync.Reloading = false;

            if (State.Sync.Reloading) return;

            FinishBurst = false;
            State.Sync.Reloading = true;

            if (Timings.AnimationDelayTick > Comp.Session.Tick && LastEvent != EventTriggers.Reloading)
            {
                Comp.Session.FutureEvents.Schedule(o => { StartReload(true); }, null, Timings.AnimationDelayTick - Comp.Session.Tick);
                return;
            }

            if (IsShooting)
                StopShooting();

            if ((State.Sync.CurrentMags == 0 && !System.EnergyAmmo && !Comp.Session.IsCreative))
            {
                if (!OutOfAmmo)
                {
                    EventTriggerStateChanged(EventTriggers.OutOfAmmo, true);
                    OutOfAmmo = true;
                }
                State.Sync.Reloading = false;
            }
            else
            {
                if (OutOfAmmo)
                {
                    EventTriggerStateChanged(EventTriggers.OutOfAmmo, false);
                    OutOfAmmo = false;
                }

                uint delay;
                if (System.WeaponAnimationLengths.TryGetValue(EventTriggers.Reloading, out delay))
                {
                    Timings.AnimationDelayTick = Comp.Session.Tick + delay;
                    EventTriggerStateChanged(EventTriggers.Reloading, true);
                }

                if (System.MustCharge && !Comp.Session.ChargingWeaponsCheck.Contains(this))
                    ChargeReload();
                else if (!System.MustCharge)
                {
                    CancelableReloadAction += Reloaded;
                    Comp.Session.FutureEvents.Schedule(CancelableReloadAction, null, (uint)System.ReloadTime);
                    Timings.ReloadedTick = (uint)System.ReloadTime + Comp.Session.Tick;
                }


                if (ReloadEmitter == null || ReloadEmitter.IsPlaying) return;
                ReloadEmitter.PlaySound(ReloadSound, true, false, false, false, false, false);

            }
        }

        public void ChargeReload()
        {
            var syncCharge = Timings.ChargeUntilTick > 0;
            if (!syncCharge)
            {
                var currDif = Comp.State.Value.CurrentCharge - State.Sync.CurrentCharge;
                Comp.State.Value.CurrentCharge = currDif > 0 ? currDif : 0;
                State.Sync.CurrentCharge = 0;
            }

            Comp.Session.ChargingWeapons.Add(this);
            Comp.Session.ChargingWeaponsCheck.Add(this);

            Comp.Ai.RequestedWeaponsDraw += RequiredPower;

            Timings.ChargeUntilTick = syncCharge ? Timings.ChargeUntilTick : (uint)System.ReloadTime + Comp.Session.Tick;
            Comp.Ai.OverPowered = Comp.Ai.RequestedWeaponsDraw > 0 && Comp.Ai.RequestedWeaponsDraw > Comp.Ai.GridMaxPower;
        }


        internal void CheckReload()
        {
            var hasMags = State.Sync.CurrentMags > 0;
            var chargeReload = System.MustCharge && (System.EnergyAmmo || hasMags);
            var standardReload = !System.MustCharge && !System.EnergyAmmo && hasMags;

            if (State.Sync.CurrentAmmo == 0 && (Comp.Session.IsCreative || chargeReload || standardReload))
                StartReload();
        }

        internal void GetAmmoClient()
        {
            State.Sync.CurrentMags = Comp.BlockInventory.GetItemAmount(System.AmmoDefId);
            CheckReload();
        }

        internal void Reloaded(object o = null)
        {
            State.Sync.Reloading = false;

            if (System.MustCharge)
            {
                State.Sync.CurrentAmmo = System.EnergyMagSize;
                Comp.State.Value.CurrentCharge = System.EnergyMagSize;
                State.Sync.CurrentCharge = System.EnergyMagSize;

                Timings.ChargeUntilTick = 0;
                Timings.ChargeDelayTicks = 0;
            }
            else
                CancelableReloadAction -= Reloaded;

            EventTriggerStateChanged(EventTriggers.Reloading, false);


            if (!System.HasBurstDelay)
                State.ShotsFired = 0;

            if (!System.EnergyAmmo && (State.Sync.CurrentMags > 0 || Comp.Session.IsCreative))
            {
                State.Sync.CurrentAmmo = System.MagazineDef.Capacity;
                if (!Comp.Session.IsClient && !Comp.Session.IsCreative)
                   Comp.BlockInventory.RemoveItemsOfType(1, System.AmmoDefId);
            }
        }

        public void StartPreFiringSound()
        {
            PreFiringEmitter?.PlaySound(PreFiringSound);
        }

        public void StopPreFiringSound(bool force)
        {
            PreFiringEmitter?.StopSound(force);
        }

        public void StartFiringSound()
        {
            FiringEmitter?.PlaySound(FiringSound);
        }

        public void StopFiringSound(bool force)
        {
            FiringEmitter?.StopSound(force);
        }

        public void StopReloadSound()
        {
            ReloadEmitter?.StopSound(true);
        }

        public void StopRotateSound()
        {
            RotateEmitter?.StopSound(true);
        }

        internal void WakeTargets()
        {
            LastTargetTick = Comp.Session.Tick;
            LoadId = Comp.Session.LoadAssigner();
            ShortLoadId = Comp.Session.ShortLoadAssigner();
        }
    }
}
