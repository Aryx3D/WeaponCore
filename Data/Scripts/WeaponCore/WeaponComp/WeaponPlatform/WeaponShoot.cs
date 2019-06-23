﻿using System;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Projectiles;
using  WeaponCore.Support;
namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal void Shoot()
        {
            var session = Comp.MyAi.MySession;
            var tick = session.Tick;

            if (ShotCounter == 0 && _newCycle) _rotationTime = 0;
            _newCycle = false;
            var targetLock = Target != null;
            if (ShotCounter++ >= _ticksPerShot - 1) ShotCounter = 0;
            
            var bps = WeaponType.TurretDef.BarrelsPerShot;
            var skipAhead = WeaponType.TurretDef.SkipBarrels;

            if (WeaponType.TurretDef.RotateBarrelAxis != 0) MovePart(-1 * bps);
            if (targetLock) _targetTick++;
            if (ShotCounter != 0) return;
            IsShooting();
            if (!WeaponSystem.EnergyAmmo) CurrentAmmo--;
            var endBarrel = _numOfBarrels - 1;
            if (_shotsInCycle++ == (_numOfBarrels - 1))
            {
                _shotsInCycle = 0;
                _newCycle = true;
            }

            if (targetLock && _targetTick > 59)
            {
                _targetTick = 0;
                if (!TrackingAi && !ValidTarget(this, Target))
                {
                    Log.Line("shootStep2: setting target null");
                    Target = null;
                    return;
                }
                IHitInfo hitInfo;
                MyAPIGateway.Physics.CastRay(Comp.MyPivotPos, Target.PositionComp.GetPosition(), out hitInfo, 15);
                if (hitInfo?.HitEntity == null || (hitInfo.HitEntity != Target && hitInfo.HitEntity != Target.Parent))
                {
                    Log.Line($"rayFail, setting target to null");
                    Target = null;
                    return;
                }
            }

            for (int i = 0; i < bps; i++)
            {
                var current = NextMuzzle;
                var muzzle = Muzzles[current];
                var lastTick = muzzle.LastUpdateTick;
                var recentMovement = lastTick >= _posChangedTick && lastTick - _posChangedTick < 10;
                if (recentMovement || _posChangedTick > lastTick)
                {
                    var dummy = Dummies[current];
                    var newInfo = dummy.Info;
                    muzzle.Direction = newInfo.Direction;
                    muzzle.Position = newInfo.Position;
                    muzzle.LastUpdateTick = tick;
                }
                muzzle.LastShot = tick;
                var deviatedAngle = WeaponType.TurretDef.DeviateShotAngle;
                if (deviatedAngle > 0)
                {
                    var dirMatrix = Matrix.CreateFromDir(muzzle.Direction);
                    var randomFloat1 = MyUtils.GetRandomFloat(-deviatedAngle, deviatedAngle);
                    var randomFloat2 = MyUtils.GetRandomFloat(0.0f, 6.283185f);

                    muzzle.DeviatedDir = Vector3.TransformNormal(
                        -new Vector3(MyMath.FastSin(randomFloat1) * MyMath.FastCos(randomFloat2),
                            MyMath.FastSin(randomFloat1) * MyMath.FastSin(randomFloat2),
                            MyMath.FastCos(randomFloat1)), dirMatrix);
                }
                else muzzle.DeviatedDir = muzzle.Direction;

                if (i == bps) NextMuzzle++;
                NextMuzzle = (NextMuzzle + (skipAhead + 1)) % (endBarrel + 1);
                lock (session.Projectiles.Wait[session.ProCounter])
                {
                    Projectile pro;
                    session.Projectiles.ProjectilePool[session.ProCounter].AllocateOrCreate(out pro);
                    pro.WeaponSystem = WeaponSystem;
                    pro.FiringCube = Comp.MyCube;
                    pro.Origin = muzzle.Position;
                    pro.PredictedTargetPos = TargetPos;
                    pro.Direction = muzzle.DeviatedDir;
                    pro.State = Projectile.ProjectileState.Start;
                    pro.Target = Target;
                    if (WeaponSystem.ModelId != -1)
                    {
                        MyEntity ent;
                        session.Projectiles.EntityPool[session.ProCounter][WeaponSystem.ModelId].AllocateOrCreate(out ent);
                        if (!ent.InScene)
                        {
                            ent.InScene = true;
                            ent.Render.AddRenderObjects();
                        }
                        pro.Entity = ent;
                    }
                }
                if (session.ProCounter++ >= session.Projectiles.Wait.Length - 1) session.ProCounter = 0;
            }
        }

        public void MovePart(int time)
        {
            var radiansPerShot = (2 * Math.PI / _numOfBarrels);
            var radians = radiansPerShot / _timePerShot;
            var axis = WeaponType.TurretDef.RotateBarrelAxis;
            MatrixD rotationMatrix;
            if (axis == 1) rotationMatrix = MatrixD.CreateRotationX(radians * _rotationTime);
            else if (axis == 2 ) rotationMatrix = MatrixD.CreateRotationY(radians * _rotationTime);
            else if (axis == 3) rotationMatrix = MatrixD.CreateRotationZ(radians * _rotationTime);
            else return;

            _rotationTime += time;
            rotationMatrix.Translation = _localTranslation;
            EntityPart.PositionComp.LocalMatrix = rotationMatrix;
        }

        internal MyParticleEffect MuzzleEffect1;
        internal MyParticleEffect MuzzleEffect2;

        private void StartShooting()
        {
            Log.Line($"starting sound");
        }

        private void IsShooting()
        {
            var vel = Comp.Physics.LinearVelocity;
            var muzzle = Muzzles[NextMuzzle];
            var pos = muzzle.Position;
            var matrix = MatrixD.CreateWorld(pos, EntityPart.WorldMatrix.Forward, EntityPart.Parent.WorldMatrix.Up);
            if (MuzzleEffect1 == null)
                MyParticlesManager.TryCreateParticleEffect("Smoke_LargeGunShot", ref matrix, ref pos, uint.MaxValue, out MuzzleEffect1);
            else if (MuzzleEffect1.IsEmittingStopped)
                MuzzleEffect1.Play();

            if (MuzzleEffect2 == null)
                MyParticlesManager.TryCreateParticleEffect("Muzzle_Flash_Large", ref matrix, ref pos, uint.MaxValue, out MuzzleEffect2);
            else if (MuzzleEffect2.IsEmittingStopped)
                MuzzleEffect2.Play();

            if (MuzzleEffect1 != null)
            {
                MuzzleEffect1.WorldMatrix = MatrixD.CreateWorld(pos, EntityPart.WorldMatrix.Forward, EntityPart.Parent.WorldMatrix.Up);
                MuzzleEffect1.Velocity = vel;
            }

            if (MuzzleEffect2 != null)
            {
                MuzzleEffect2.WorldMatrix = MatrixD.CreateWorld(pos, EntityPart.WorldMatrix.Forward, EntityPart.Parent.WorldMatrix.Up);
                MuzzleEffect2.Velocity = vel;
            }
        }

        private void EndShooting()
        {
            if (MuzzleEffect2 != null)
            {
                MuzzleEffect2.Stop(true);
                MuzzleEffect2 = null;
            }

            if (MuzzleEffect1 != null)
            {
                MuzzleEffect1.Stop(false);
                MuzzleEffect1 = null;
            }
        }
    }
}
