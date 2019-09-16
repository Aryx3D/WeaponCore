﻿using System;
using System.Collections.Generic;
using ParallelTasks;
using VRage;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        internal void CreateAnimationSets(AnimationDefinition animations, WeaponSystem system, out Dictionary<Weapon.EventTriggers, HashSet<PartAnimation>> weaponAnimationSets, out Dictionary<string, MyTuple<string[], Color, bool, bool, float>?> weaponEmissivesSet, out Dictionary<string,Matrix?[]> weaponLinearMoveSet)
        {

            var allAnimationSet = new Dictionary<Weapon.EventTriggers, HashSet<PartAnimation>>();
            var allEmissivesSet = new Dictionary<string, MyTuple<string[], Color, bool, bool, float>?>();

            var wepAnimationSets = animations.WeaponAnimationSets;
            var wepEmissivesSet = animations.Emissives;

            weaponLinearMoveSet = new Dictionary<string,Matrix?[]>();

            var emissiveLookup = new Dictionary<string, WeaponEmissive>();

            if (wepEmissivesSet != null)
            {
                foreach (var emissive in wepEmissivesSet)
                    emissiveLookup.Add(emissive.emissiveName, emissive);
            }

            if (wepAnimationSets == null)
            {
                weaponAnimationSets = allAnimationSet;
                weaponEmissivesSet = allEmissivesSet;
                return;
            }
            foreach (var animationSet in wepAnimationSets)
            {
                for (int t = 0; t < animationSet.SubpartId.Length; t++)
                {
                    foreach (var moves in animationSet.EventMoveSets)
                    {
                        if (!allAnimationSet.ContainsKey(moves.Key))
                        {
                            allAnimationSet[moves.Key] = new HashSet<PartAnimation>();
                        }

                        List<Matrix?> moveSet = new List<Matrix?>();
                        List<Matrix?> rotationSet = new List<Matrix?>();
                        List<Matrix?> rotCenterSet = new List<Matrix?>();
                        List<string> rotCenterNameSet = new List<string>();
                        var id = $"{(int)moves.Key}{animationSet.SubpartId[t]}";
                        AnimationType[] typeSet = new[]
                        {
                            AnimationType.Movement,
                            AnimationType.ShowInstant,
                            AnimationType.HideInstant,
                            AnimationType.ShowFade,
                            AnimationType.HideFade,
                            AnimationType.Delay
                        };

                        var moveIndexer = new List<int[]>();
                        var currentEmissivePart = new List<int>();

                        for (int i = 0; i < moves.Value.Length; i++)
                        {
                            var move = moves.Value[i];

                            var hasEmissive = !string.IsNullOrEmpty(move.EmissiveName);

                            if (move.MovementType == RelMove.MoveType.Delay ||
                            move.MovementType == RelMove.MoveType.Show ||
                            move.MovementType == RelMove.MoveType.Hide)
                            {
                                moveSet.Add(null);
                                rotationSet.Add(null);
                                rotCenterSet.Add(null);
                                for (var j = 0; j < move.TicksToMove; j++)
                                {
                                    var type = 5;

                                    switch (move.MovementType)
                                    {
                                        case RelMove.MoveType.Delay:
                                            break;

                                        case RelMove.MoveType.Show:
                                            type = move.Fade ? 3 : 1;
                                            break;

                                        case RelMove.MoveType.Hide:
                                            type = move.Fade ? 4 : 2;
                                            break;
                                    }

                                    WeaponEmissive emissive;
                                    if (hasEmissive && emissiveLookup.TryGetValue(move.EmissiveName, out emissive))
                                    {
                                        createEmissiveStep(emissive, id + moveIndexer.Count, (float)j /  (move.TicksToMove - 1), ref allEmissivesSet, ref currentEmissivePart);
                                    }
                                    else
                                    {
                                        allEmissivesSet.Add(id + moveIndexer.Count, null);
                                        currentEmissivePart.Add(-1);
                                    }

                                    moveIndexer.Add(new[]
                                        {moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, type, currentEmissivePart.Count - 1});
                                }
                            }
                            else
                            {
                                if (!String.IsNullOrEmpty(move.CenterEmpty) &&
                                    (move.RotAroundCenter.x > 0 || move.RotAroundCenter.y > 0 ||
                                     move.RotAroundCenter.z > 0 || move.RotAroundCenter.x < 0 ||
                                     move.RotAroundCenter.y < 0 || move.RotAroundCenter.z < 0))
                                {
                                    rotCenterNameSet.Add(move.CenterEmpty);
                                    rotCenterSet.Add(CreateRotation(move.RotAroundCenter.x / move.TicksToMove,
                                        move.RotAroundCenter.y / move.TicksToMove,
                                        move.RotAroundCenter.z / move.TicksToMove));
                                }
                                else
                                {
                                    rotCenterNameSet.Add(null);
                                    rotCenterSet.Add(null);
                                }

                                if (move.Rotation.x > 0 || move.Rotation.y > 0 || move.Rotation.z > 0 ||
                                    move.Rotation.x < 0 || move.Rotation.y < 0 || move.Rotation.z < 0)
                                {
                                    rotationSet.Add(CreateRotation(move.Rotation.x / move.TicksToMove, move.Rotation.y / move.TicksToMove, move.Rotation.z / move.TicksToMove));
                                }
                                else
                                    rotationSet.Add(null);

                                if (move.LinearPoints != null && move.LinearPoints.Length > 0)
                                {
                                    double distance = 0;
                                    var tmpDirVec = new double[move.LinearPoints.Length][];

                                    for (int j = 0; j < move.LinearPoints.Length; j++)
                                    {
                                        var point = move.LinearPoints[j];

                                        var d = Math.Sqrt((point.x * point.x) + (point.y * point.y) +
                                                          (point.z * point.z));

                                        distance += d;

                                        var dv = new[] { d, point.x / d, point.y / d, point.z / d };

                                        tmpDirVec[j] = dv;
                                    }

                                    if (move.MovementType == RelMove.MoveType.ExpoDecay)
                                    {
                                        var traveled = 0d;

                                        var check = 1d;
                                        var rate = 0d;
                                        while (check > 0)
                                        {
                                            rate += 0.001;
                                            check = distance * Math.Pow(1 - rate, move.TicksToMove);
                                            if (check < 0.001) check = 0;

                                        }

                                        var vectorCount = 0;
                                        var remaining = 0d;
                                        var vecTotalMoved = 0d;
                                        rate = 1 - rate;

                                        for (int j = 0; j < move.TicksToMove; j++)
                                        {
                                            var step = distance * Math.Pow(rate, j + 1);
                                            if (step < 0.001) step = 0;

                                            var lastTraveled = traveled;
                                            traveled = distance - step;
                                            var changed = traveled - lastTraveled;

                                            changed += remaining;
                                            if (changed > tmpDirVec[vectorCount][0] - vecTotalMoved)
                                            {
                                                var origMove = changed;
                                                changed = changed - (tmpDirVec[vectorCount][0] - vecTotalMoved);
                                                remaining = origMove - changed;
                                                vecTotalMoved = 0;
                                            }
                                            else
                                            {
                                                vecTotalMoved += changed;
                                                remaining = 0;
                                            }


                                            var vector = new Vector3(tmpDirVec[vectorCount][1] * changed,
                                                tmpDirVec[vectorCount][2] * changed,
                                                tmpDirVec[vectorCount][3] * changed);

                                            var matrix = Matrix.CreateTranslation(vector);

                                            moveSet.Add(matrix);

                                            WeaponEmissive emissive;
                                            if (hasEmissive && emissiveLookup.TryGetValue(move.EmissiveName, out emissive))
                                            {
                                                createEmissiveStep(emissive, id + moveIndexer.Count, (float)j / (move.TicksToMove - 1), ref allEmissivesSet, ref currentEmissivePart);
                                            }
                                            else
                                            {
                                                allEmissivesSet.Add(id + moveIndexer.Count, null);
                                                currentEmissivePart.Add(-1);
                                            }

                                            moveIndexer.Add(new[]
                                                {moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, 0, currentEmissivePart.Count - 1});

                                            if (remaining > 0)
                                                vectorCount++;

                                        }
                                    }
                                    else if (move.MovementType == RelMove.MoveType.ExpoGrowth)
                                    {
                                        var traveled = 0d;

                                        var rate = 0d;
                                        var check = 0d;
                                        while (check < distance)
                                        {
                                            rate += 0.001;
                                            check = 0.001 * Math.Pow(1 + rate, move.TicksToMove);
                                        }

                                        var vectorCount = 0;
                                        var remaining = 0d;
                                        var vecTotalMoved = 0d;
                                        rate += 1;

                                        for (int j = 0; j < move.TicksToMove; j++)
                                        {
                                            var step = 0.001 * Math.Pow(rate, j + 1);
                                            if (step > distance) step = distance;

                                            var lastTraveled = traveled;
                                            traveled = step;
                                            var changed = traveled - lastTraveled;

                                            changed += remaining;
                                            if (changed > tmpDirVec[vectorCount][0] - vecTotalMoved)
                                            {
                                                var origMove = changed;
                                                changed = changed - (tmpDirVec[vectorCount][0] - vecTotalMoved);
                                                remaining = origMove - changed;
                                                vecTotalMoved = 0;
                                            }
                                            else
                                            {
                                                vecTotalMoved += changed;
                                                remaining = 0;
                                            }


                                            var vector = new Vector3(tmpDirVec[vectorCount][1] * changed,
                                                tmpDirVec[vectorCount][2] * changed,
                                                tmpDirVec[vectorCount][3] * changed);

                                            var matrix = Matrix.CreateTranslation(vector);

                                            moveSet.Add(matrix);

                                            WeaponEmissive emissive;
                                            if (hasEmissive && emissiveLookup.TryGetValue(move.EmissiveName, out emissive))
                                            {
                                                createEmissiveStep(emissive, id + moveIndexer.Count, (float)j / (move.TicksToMove - 1), ref allEmissivesSet, ref currentEmissivePart);
                                            }
                                            else
                                            {
                                                allEmissivesSet.Add(id + moveIndexer.Count, null);
                                                currentEmissivePart.Add(-1);
                                            }

                                            moveIndexer.Add(new[]
                                                {moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, 0, currentEmissivePart.Count - 1});

                                            if (remaining > 0)
                                                vectorCount++;
                                        }
                                    }
                                    else if (move.MovementType == RelMove.MoveType.Linear)
                                    {
                                        var distancePerTick = distance / move.TicksToMove;
                                        var vectorCount = 0;
                                        var remaining = 0d;
                                        var vecTotalMoved = 0d;

                                        for (int j = 0; j < move.TicksToMove; j++)
                                        {
                                            var changed = distancePerTick + remaining;
                                            if (changed > tmpDirVec[vectorCount][0] - vecTotalMoved)
                                            {
                                                var origMove = changed;
                                                changed = changed - (tmpDirVec[vectorCount][0] - vecTotalMoved);
                                                remaining = origMove - changed;
                                                vecTotalMoved = 0;
                                            }
                                            else
                                            {
                                                vecTotalMoved += changed;
                                                remaining = 0;
                                            }

                                            var vector = new Vector3(tmpDirVec[vectorCount][1] * changed,
                                                tmpDirVec[vectorCount][2] * changed,
                                                tmpDirVec[vectorCount][3] * changed);

                                            var matrix = Matrix.CreateTranslation(vector);

                                            moveSet.Add(matrix);

                                            WeaponEmissive emissive;
                                            if (hasEmissive && emissiveLookup.TryGetValue(move.EmissiveName, out emissive))
                                            {
                                                createEmissiveStep(emissive, id + moveIndexer.Count, (float)j / (move.TicksToMove - 1), ref allEmissivesSet, ref currentEmissivePart);
                                            }
                                            else
                                            {
                                                allEmissivesSet.Add(id + moveIndexer.Count, null);
                                                currentEmissivePart.Add(-1);
                                            }

                                            moveIndexer.Add(new[]
                                                {moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, 0, currentEmissivePart.Count - 1});

                                            if (remaining > 0)
                                                vectorCount++;
                                        }
                                    }
                                    else
                                    {
                                        moveSet.Add(null);

                                        for (int j = 0; j < move.TicksToMove; j++)
                                        {
                                            WeaponEmissive emissive;
                                            if (hasEmissive && emissiveLookup.TryGetValue(move.EmissiveName, out emissive))
                                            {
                                                createEmissiveStep(emissive, id + moveIndexer.Count, (float)j / (move.TicksToMove - 1), ref allEmissivesSet, ref currentEmissivePart);
                                            }
                                            else
                                            {
                                                allEmissivesSet.Add(id + moveIndexer.Count, null);
                                                currentEmissivePart.Add(-1);
                                            }

                                            moveIndexer.Add(new[]
                                                {moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, 0, allEmissivesSet.Count - 1});
                                        }
                                    }

                                }
                                else
                                {
                                    moveSet.Add(null);

                                    for (int j = 0; j < move.TicksToMove; j++)
                                    {
                                        WeaponEmissive emissive;
                                        if (hasEmissive && emissiveLookup.TryGetValue(move.EmissiveName, out emissive))
                                        {
                                            createEmissiveStep(emissive, id + moveIndexer.Count, (float)j / (move.TicksToMove - 1), ref allEmissivesSet, ref currentEmissivePart);
                                        }
                                        else
                                        {
                                            allEmissivesSet.Add(id + moveIndexer.Count, null);
                                            currentEmissivePart.Add(-1);
                                        }

                                        moveIndexer.Add(new[]
                                            {moveSet.Count - 1, rotationSet.Count - 1, rotCenterSet.Count - 1, 0, currentEmissivePart.Count - 1});
                                    }
                                }
                            }
                        }

                        var loop = false;
                        var reverse = false;

                        if (animationSet.Loop != null && animationSet.Loop.Contains(moves.Key))
                            loop = true;

                        if (animationSet.Reverse != null && animationSet.Reverse.Contains(moves.Key))
                            reverse = true;

                        var partAnim = new PartAnimation(id, rotationSet.ToArray(),
                            rotCenterSet.ToArray(), typeSet, currentEmissivePart.ToArray(), moveIndexer.ToArray(), animationSet.SubpartId[t], null, null,
                            animationSet.BarrelId, animationSet.StartupFireDelay, animationSet.AnimationDelays[moves.Key], system, loop, reverse);

                        weaponLinearMoveSet.Add(id, moveSet.ToArray());

                        partAnim.RotCenterNameSet = rotCenterNameSet.ToArray();
                        allAnimationSet[moves.Key].Add(partAnim);
                    }
                }
            }

            weaponAnimationSets = allAnimationSet;
            weaponEmissivesSet = allEmissivesSet;
            
        }

        internal Dictionary<Weapon.EventTriggers, HashSet<PartAnimation>> CreateWeaponAnimationSet(Dictionary<Weapon.EventTriggers, HashSet<PartAnimation>> systemAnimations, RecursiveSubparts parts)
        {
            var allAnimationSet = new Dictionary<Weapon.EventTriggers, HashSet<PartAnimation>>();
            WeaponSystem system = null;
            foreach (var animationSet in systemAnimations)
            {
                allAnimationSet.Add(animationSet.Key, new HashSet<PartAnimation>());
                foreach (var animation in animationSet.Value)
                {

                    if (system == null) system = animation.System;

                    MyEntity part;
                    parts.NameToEntity.TryGetValue(animation.SubpartId, out part);
                    var subpart = part as MyEntitySubpart;
                    if (subpart == null) continue;

                    var rotations = new Matrix?[animation.RotationSet.Length];
                    var rotCenters = new Matrix?[animation.RotCenterSet.Length];
                    animation.RotationSet.CopyTo(rotations, 0);
                    animation.RotCenterSet.CopyTo(rotCenters, 0);

                    var rotCenterNames = animation.RotCenterNameSet;

                    var partCenter = GetPartLocation("subpart_" + animation.SubpartId, subpart.Parent.Model);



                    if (partCenter != null)
                    {
                        for (int i = 0; i < rotations.Length; i++)
                        {
                            if (rotations[i] != null)
                                rotations[i] = Matrix.CreateTranslation(-(Vector3)partCenter) * (Matrix)rotations[i] *
                                               Matrix.CreateTranslation((Vector3)partCenter);
                        }
                    }

                    if (partCenter != null)
                    {
                        for (int i = 0; i < rotCenters.Length; i++)
                        {
                            if (rotCenters[i] != null && rotCenterNames != null)
                            {
                                var dummyCenter = GetPartLocation(rotCenterNames[i], subpart.Model);
                                if (dummyCenter != null)
                                    rotCenters[i] = Matrix.CreateTranslation(-(Vector3)(partCenter + dummyCenter)) * (Matrix)rotCenters[i] * Matrix.CreateTranslation((Vector3)(partCenter + dummyCenter));
                            }


                        }
                    }

                    allAnimationSet[animationSet.Key].Add(new PartAnimation(animation.AnimationId,rotations, rotCenters,
                        animation.TypeSet, animation.CurrentEmissivePart, animation.MoveToSetIndexer, animation.SubpartId, subpart, parts.Entity,
                        animation.Muzzle, animation.FireDelay, animation.MotionDelay, system, animation.DoesLoop,
                        animation.DoesReverse));
                }
            }

            try
            {
                foreach (var emissive in system.WeaponEmissiveSet)
                {
                    if (emissive.Value == null) continue;

                    foreach (var part in emissive.Value.Value.Item1)
                    {
                        parts.SetEmissiveParts(part, Color.Transparent, 0);
                    }
                }
            }
            catch (Exception e)
            {
                //cant check for emissives so may be null ref
            }

            return allAnimationSet;
        }

        internal Matrix CreateRotation(double x, double y, double z)
        {

            var rotation = MatrixD.Zero;

            if (x > 0 || x < 0)
                rotation = MatrixD.CreateRotationX(MathHelperD.ToRadians(x));

            if (y > 0 || y < 0)
                if (x > 0 || x < 0)
                    rotation *= MatrixD.CreateRotationY(MathHelperD.ToRadians(y));
                else
                    rotation = MatrixD.CreateRotationY(MathHelperD.ToRadians(y));

            if (z > 0 || z < 0)
                if (x > 0 || x < 0 || y > 0 || y < 0)
                    rotation *= MatrixD.CreateRotationZ(MathHelperD.ToRadians(z));
                else
                    rotation = MatrixD.CreateRotationZ(MathHelperD.ToRadians(z));

            return rotation;
        }

        internal void createEmissiveStep(WeaponEmissive emissive, string id, float progress, ref Dictionary<string, MyTuple<string[], Color, bool, bool, float>?> allEmissivesSet, ref List<int> currentEmissivePart)
        {
            var setColor = (Color)emissive.colors[0];

            if (emissive.colors.Length > 1)
            {
                if (progress < 1)
                {
                    float scaledTime = progress * (float) (emissive.colors.Length - 1);
                    Color lastColor = emissive.colors[(int) scaledTime];
                    Color nextColor = emissive.colors[(int) (scaledTime + 1f)];
                    float scaledProgress = (float) (scaledTime * progress);
                    setColor = Color.Lerp(lastColor, nextColor, scaledProgress);
                }
                else
                    setColor = emissive.colors[emissive.colors.Length - 1];
            }

            var intensity = MathHelper.Lerp(emissive.intensityRange[0],
                emissive.intensityRange[1], progress);

            var currPart =  (int)Math.Round(MathHelper.Lerp(0, emissive.emissivePartNames.Length - 1, progress));

            allEmissivesSet.Add(id, MyTuple.Create(emissive.emissivePartNames, setColor, emissive.cycleEmissivesParts, emissive.leavePreviousOn, intensity));
            currentEmissivePart.Add(currPart);
        }

        internal Vector3? GetPartLocation(string partName, IMyModel model)
        {
            Dictionary<string, IMyModelDummy> dummyList = new Dictionary<string, IMyModelDummy>();
            model.GetDummies(dummyList);

            IMyModelDummy dummy;
            if (dummyList.TryGetValue(partName, out dummy))
            {
                return dummy.Matrix.Translation;

            }

            return null;
        }

        internal void ProcessAnimations()
        {
            PartAnimation animation;
            while (animationsToProcess.TryDequeue(out animation))
            {

                //var data = new AnimationParallelData(ref animation);
                if (!animation.MainEnt.MarkedForClose && animation.MainEnt != null)
                {
                    if ((animation.DoesLoop && animation.Looping && !animation.PauseAnimation) || animation.MotionDelay <= 0 || animation.CurrentMove > 0 || (animation.MotionDelay > 0 && animation.StartTick <= Tick && animation.StartTick > 0 && animation.CurrentMove == 0))
                    {
                        //MyAPIGateway.Parallel.StartBackground(AnimateParts, DoAnimation, data);
                        AnimateParts(animation);
                        animation.StartTick = 0;
                    }
                    else if (animation.MotionDelay > 0 && animation.StartTick == 0)
                    {
                        animation.StartTick = Tick + animation.MotionDelay;
                        animationsToQueue.Enqueue(animation);
                    }
                    else
                    {
                        animationsToQueue.Enqueue(animation);
                    }

                }
            }
        }

        internal void ProcessAnimationQueue()
        {
            PartAnimation animation;
            while (animationsToQueue.TryDequeue(out animation))
            {
                if (!animation.MainEnt.MarkedForClose && animation.MainEnt != null)
                    animationsToProcess.Enqueue(animation);
            }
        }

        internal void AnimateParts(PartAnimation animation)
        {
            var localMatrix = animation.Part.PositionComp.LocalMatrix;
            MatrixD? rotation;
            MatrixD? rotAroundCenter;
            Vector3D translation;
            AnimationType animationType;
            PartAnimation.EmissiveState? currentEmissive;

            animation.GetCurrentMove(out translation, out rotation, out rotAroundCenter, out animationType, out currentEmissive);


            if (animation.Reverse)
            {
                if (animationType == AnimationType.Movement) localMatrix.Translation = localMatrix.Translation - translation;

                animation.Previous();
                if (animation.Previous(false) == animation.NumberOfMoves - 1)
                {
                    animation.Reverse = false;
                }
            }
            else
            {
                if (animationType == AnimationType.Movement) localMatrix.Translation = localMatrix.Translation + translation;

                animation.Next();
                if (animation.DoesReverse && animation.Next(false) == 0)
                {
                    animation.Reverse = true;
                }
            }

            if (rotation != null)
            {
                localMatrix *= animation.Reverse ? Matrix.Invert((Matrix)rotation) : (Matrix)rotation;
            }

            if (rotAroundCenter != null)
            {
                localMatrix *= animation.Reverse ? Matrix.Invert((Matrix)rotAroundCenter) : (Matrix)rotAroundCenter;
            }

            if (animationType == AnimationType.Movement)
            {
                animation.Part.PositionComp.SetLocalMatrix(ref localMatrix,
                    animation.MainEnt, true);
            }

            else if (animationType == AnimationType.ShowInstant || animationType == AnimationType.ShowFade)
            {
                animation.Part.Render.FadeIn = animationType == AnimationType.ShowFade;
                animation.Part.Render.AddRenderObjects();
            }
            else if (animationType == AnimationType.HideInstant || animationType == AnimationType.HideFade)
            {
                animation.Part.Render.FadeOut = animationType == AnimationType.HideFade;
                animation.Part.Render.RemoveRenderObjects();
            }

            if (currentEmissive != null)
            {
                var emissive = currentEmissive.Value;

                if (emissive.CycleParts)
                {
                    animation.Part.SetEmissiveParts(emissive.EmissiveParts[emissive.CurrentPart], emissive.CurrentColor,
                        emissive.CurrentIntensity);
                    if (!emissive.LeavePreviousOn)
                    {
                        var prev = emissive.CurrentPart - 1 >= 0 ? emissive.CurrentPart - 1 : emissive.EmissiveParts
                            .Length - 1;
                        animation.Part.SetEmissiveParts(emissive.EmissiveParts[prev],
                            Color.Transparent,
                            emissive.CurrentIntensity);
                    }
                }
                else
                {
                    for (int i = 0; i < emissive.EmissiveParts.Length; i++)
                    {
                        animation.Part.SetEmissiveParts(emissive.EmissiveParts[i], emissive.CurrentColor, emissive.CurrentIntensity);
                    }
                }
            }

            if (animation.Reverse || animation.Looping || animation.CurrentMove > 0)
            {
                animationsToQueue.Enqueue(animation);
            }
        }

        #region Threaded animation code

        internal void AnimateParts(WorkData data)
        {
            var animationData = data as AnimationParallelData;

            var localMatrix = animationData.Animation.Part.PositionComp.LocalMatrix;
            MatrixD? rotation;
            MatrixD? rotAroundCenter;
            Vector3D translation;
            AnimationType animationType;

            PartAnimation.EmissiveState? currentEmissive;

            animationData.Animation.GetCurrentMove(out translation, out rotation, out rotAroundCenter, out animationType, out currentEmissive);

            if (animationData.Animation.Reverse)
            {
                localMatrix.Translation = localMatrix.Translation - translation;

                animationData.Animation.Previous();
                if (animationData.Animation.Previous(false) == animationData.Animation.NumberOfMoves - 1)
                {
                    animationData.Animation.Reverse = false;
                }
            }
            else
            {
                localMatrix.Translation = localMatrix.Translation + translation;

                animationData.Animation.Next();
                if (animationData.Animation.DoesReverse && animationData.Animation.Next(false) == 0)
                {
                    animationData.Animation.Reverse = true;
                }
            }

            if (rotation != null)
            {
                localMatrix *= (Matrix)rotation;
            }

            if (rotAroundCenter != null)
            {
                localMatrix *= (Matrix)rotAroundCenter;
            }


            animationData.NewMatrix = localMatrix;
            animationData.Type = animationType;

        }

        internal void DoAnimation(WorkData data)
        {
            var animationData = data as AnimationParallelData;
            var animationType = animationData.Type;

            if (animationType == AnimationType.Movement)
            {
                animationData.Animation.Part.PositionComp.SetLocalMatrix(ref animationData.NewMatrix,
                    animationData.Animation.MainEnt, true);
            }

            else if (animationType == AnimationType.ShowInstant || animationType == AnimationType.ShowFade)
            {
                animationData.Animation.Part.Render.FadeIn = animationType == AnimationType.ShowFade;
                animationData.Animation.Part.Render.AddRenderObjects();
            }
            else if (animationType == AnimationType.HideInstant || animationType == AnimationType.HideFade)
            {
                animationData.Animation.Part.Render.FadeOut = animationType == AnimationType.HideFade;
                animationData.Animation.Part.Render.RemoveRenderObjects();
            }

            var animation = animationData.Animation;

            if (animation.Reverse || animation.DoesLoop || animation.CurrentMove > 0)
            {
                animationsToQueue.Enqueue(animationData.Animation);
            }

            //animationData.timer.Complete();
        }
        #endregion
    }

    public class AnimationParallelData : WorkData
    {
        internal PartAnimation Animation;
        internal Matrix NewMatrix;
        internal Session.AnimationType Type;

        public AnimationParallelData(ref PartAnimation animation)
        {
            Animation = animation;
        }
    }
}
