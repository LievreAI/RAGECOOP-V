﻿using System;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.NaturalMotion;
using GTA.UI;
using LemonUI.Elements;
using RageCoop.Core;
using Font = GTA.UI.Font;

namespace RageCoop.Client
{
    /// <summary>
    ///     ?
    /// </summary>
    public partial class SyncedPed : SyncedEntity
    {
        /// <summary>
        ///     Create a local entity (outgoing sync)
        /// </summary>
        /// <param name="p"></param>
        internal SyncedPed(Ped p)
        {
            ID = EntityPool.RequestNewID();
            p.CanWrithe = false;
            p.IsOnlyDamagedByPlayer = false;
            MainPed = p;
            OwnerID = LocalPlayerID;

            MainPed.SetConfigFlag((int)PedConfigFlags.CPED_CONFIG_FLAG_DisableHurt, true);
        }

        /// <summary>
        ///     Create an empty character with ID
        /// </summary>
        internal SyncedPed(int id)
        {
            ID = id;
            LastSynced = Ticked;
        }

        internal override void Update()
        {
            if (Owner == null)
            {
                OwnerID = OwnerID;
                return;
            }

            if (IsPlayer) RenderNameTag();

            // Check if all data available
            if (!IsReady) return;

            // Skip update if no new sync message has arrived.
            if (!NeedUpdate) return;

            if (MainPed == null || !MainPed.Exists())
                if (!CreateCharacter())
                    return;

            if (LastFullSynced >= LastUpdated)
            {
                if (MainPed != null && Model != MainPed.Model.Hash)
                    if (!CreateCharacter())
                        return;

                if (!Settings.ShowPlayerBlip && (byte)BlipColor != 255) BlipColor = (BlipColor)255;
                if ((byte)BlipColor == 255 && PedBlip != null)
                {
                    PedBlip.Delete();
                    PedBlip = null;
                }
                else if ((byte)BlipColor != 255 && PedBlip == null)
                {
                    PedBlip = MainPed.AddBlip();


                    PedBlip.Color = BlipColor;
                    PedBlip.Sprite = BlipSprite;
                    PedBlip.Scale = BlipScale;
                }

                if (PedBlip != null)
                {
                    if (PedBlip.Color != BlipColor) PedBlip.Color = BlipColor;
                    if (PedBlip.Sprite != BlipSprite) PedBlip.Sprite = BlipSprite;
                    if (IsPlayer) PedBlip.Name = Owner.Username;
                }

                if (!Clothes.SequenceEqual(_lastClothes)) SetClothes();

                CheckCurrentWeapon();
            }

            if (MainPed.IsDead)
            {
                if (Health > 0)
                {
                    if (IsPlayer)
                        MainPed.Resurrect();
                    else
                        SyncEvents.TriggerPedKilled(this);
                }
            }
            else if (IsPlayer && MainPed.Health != Health)
            {
                MainPed.Health = Health;

                if (Health <= 0 && !MainPed.IsDead)
                {
                    MainPed.IsInvincible = false;
                    MainPed.Kill();
                    return;
                }
            }

            if (Speed >= 4)
            {
                DisplayInVehicle();
            }
            else
            {
                if (MainPed.IsInVehicle())
                {
                    MainPed.Task.LeaveVehicle(LeaveVehicleFlags.WarpOut);
                    return;
                }

                DisplayOnFoot();
            }

            if (IsSpeaking)
            {
                if (Ticked - LastSpeakingTime < 10)
                {
                    DisplaySpeaking(true);
                }
                else
                {
                    DisplaySpeaking(false);

                    IsSpeaking = false;
                    LastSpeakingTime = 0;
                }
            }

            LastUpdated = Ticked;
        }

        private void RenderNameTag()
        {
            if (!Owner.DisplayNameTag || !Settings.ShowPlayerNameTag || MainPed == null || !MainPed.IsVisible ||
                !MainPed.IsInRange(PlayerPosition, 40f)) return;

            var targetPos = MainPed.Bones[Bone.IKHead].Position + Vector3.WorldUp * 0.5f;
            Point toDraw = default;
            if (Util.WorldToScreen(targetPos, ref toDraw))
            {
                toDraw.Y -= 100;
                new ScaledText(toDraw, Owner.Username, 0.4f, Font.ChaletLondon)
                {
                    Outline = true,
                    Alignment = Alignment.Center,
                    Color = Owner.HasDirectConnection ? Color.FromArgb(179, 229, 252) : Color.White
                }.Draw();
            }
        }

        private bool CreateCharacter()
        {
            if (MainPed != null)
            {
                if (MainPed.Exists())
                {
                    // Log.Debug($"Removing ped {ID}. Reason:CreateCharacter");
                    MainPed.Kill();
                    MainPed.MarkAsNoLongerNeeded();
                    MainPed.Delete();
                }

                MainPed = null;
            }

            if (PedBlip != null && PedBlip.Exists())
            {
                PedBlip.Delete();
                PedBlip = null;
            }

            if (!Model.IsLoaded)
            {
                Model.Request();
                return false;
            }

            if ((MainPed = Util.CreatePed(Model, Position)) == null) return false;

            Model.MarkAsNoLongerNeeded();

            MainPed.BlockPermanentEvents = true;
            MainPed.CanWrithe = false;
            MainPed.CanBeDraggedOutOfVehicle = true;
            MainPed.IsOnlyDamagedByPlayer = false;
            MainPed.RelationshipGroup = SyncedPedsGroup;
            MainPed.IsFireProof = false;
            MainPed.IsExplosionProof = false;

            Call(SET_PED_DROPS_WEAPONS_WHEN_DEAD, MainPed.Handle, false);
            Call(SET_PED_CAN_BE_TARGETTED, MainPed.Handle, true);
            Call(SET_PED_CAN_BE_TARGETTED_BY_PLAYER, MainPed.Handle, Game.Player, true);
            Call(SET_PED_GET_OUT_UPSIDE_DOWN_VEHICLE, MainPed.Handle, false);
            Call(SET_CAN_ATTACK_FRIENDLY, MainPed.Handle, true, true);
            // Call(_SET_PED_CAN_PLAY_INJURED_ANIMS, false);
            Call(SET_PED_CAN_EVASIVE_DIVE, MainPed.Handle, false);

            MainPed.SetConfigFlag((int)PedConfigFlags.CPED_CONFIG_FLAG_DrownsInWater, false);
            MainPed.SetConfigFlag((int)PedConfigFlags.CPED_CONFIG_FLAG_DisableHurt, true);
            MainPed.SetConfigFlag((int)PedConfigFlags.CPED_CONFIG_FLAG_DisableExplosionReactions, true);
            MainPed.SetConfigFlag((int)PedConfigFlags.CPED_CONFIG_FLAG_AvoidTearGas, false);
            MainPed.SetConfigFlag((int)PedConfigFlags.CPED_CONFIG_FLAG_IgnoreBeingOnFire, true);
            MainPed.SetConfigFlag((int)PedConfigFlags.CPED_CONFIG_FLAG_DisableEvasiveDives, true);
            MainPed.SetConfigFlag((int)PedConfigFlags.CPED_CONFIG_FLAG_DisablePanicInVehicle, true);
            MainPed.SetConfigFlag((int)PedConfigFlags.CPED_CONFIG_FLAG_BlockNonTemporaryEvents, true);
            MainPed.SetConfigFlag((int)PedConfigFlags.CPED_CONFIG_FLAG_DisableShockingEvents, true);
            MainPed.SetConfigFlag((int)PedConfigFlags.CPED_CONFIG_FLAG_DisableHurt, true);

            SetClothes();

            if (IsPlayer) MainPed.IsInvincible = true;
            if (IsInvincible) MainPed.IsInvincible = true;

            lock (EntityPool.PedsLock)
            {
                // Add to EntityPool so this Character can be accessed by handle.
                EntityPool.Add(this);
            }

            return true;
        }

        private void SetClothes()
        {
            for (byte i = 0; i < 12; i++)
                Call(SET_PED_COMPONENT_VARIATION, MainPed.Handle, i, (int)Clothes[i],
                    (int)Clothes[i + 12], (int)Clothes[i + 24]);
            _lastClothes = Clothes;
        }


        private void DisplayOnFoot()
        {
            if (IsInParachuteFreeFall)
            {
                MainPed.PositionNoOffset = Vector3.Lerp(MainPed.ReadPosition(), Position + Velocity, 0.5f);
                MainPed.Rotation = Rotation;

                if (!Call<bool>(IS_ENTITY_PLAYING_ANIM, MainPed.Handle, "skydive@base", "free_idle", 3))
                {
                    // Skip update if animation is not loaded
                    var dict = LoadAnim("skydive@base");
                    if (dict == null) return;
                    Call(TASK_PLAY_ANIM, MainPed.Handle, dict, "free_idle", 8f, 10f, -1, 0, -8f, 1, 1, 1);
                }

                return;
            }

            if (IsParachuteOpen)
            {
                if (ParachuteProp == null)
                {
                    Model model = 1740193300;
                    model.Request(1000);
                    if (model != null)
                    {
                        ParachuteProp = World.CreateProp(model, MainPed.ReadPosition(), MainPed.ReadRotation(), false,
                            false);
                        model.MarkAsNoLongerNeeded();
                        ParachuteProp.IsPositionFrozen = true;
                        ParachuteProp.IsCollisionEnabled = false;

                        ParachuteProp.AttachTo(MainPed.Bones[Bone.SkelSpine2], new Vector3(3.6f, 0f, 0f),
                            new Vector3(0f, 90f, 0f));
                    }

                    MainPed.Task.ClearAllImmediately();
                    MainPed.Task.ClearSecondary();
                }

                MainPed.PositionNoOffset = Vector3.Lerp(MainPed.ReadPosition(), Position + Velocity, 0.5f);
                MainPed.Rotation = Rotation;
                if (!Call<bool>(IS_ENTITY_PLAYING_ANIM, MainPed.Handle, "skydive@parachute@first_person",
                        "chute_idle_right", 3))
                {
                    var dict = LoadAnim("skydive@parachute@first_person");
                    if (dict == null) return;
                    Call(TASK_PLAY_ANIM, MainPed, dict, "chute_idle_right", 8f, 10f, -1, 0, -8f, 1, 1, 1);
                }

                return;
            }

            if (ParachuteProp != null)
            {
                if (ParachuteProp.Exists()) ParachuteProp.Delete();
                ParachuteProp = null;
            }

            if (IsOnLadder)
            {
                if (Velocity.Z < 0)
                {
                    var anim = Velocity.Z < -2f ? "slide_climb_down" : "climb_down";
                    if (_currentAnimation[1] != anim)
                    {
                        MainPed.Task.ClearAllImmediately();
                        _currentAnimation[1] = anim;
                    }

                    if (!Call<bool>(IS_ENTITY_PLAYING_ANIM, MainPed.Handle, "laddersbase", anim, 3))
                        MainPed.Task.PlayAnimation("laddersbase", anim, 8f, -1, AnimationFlags.Loop);
                }
                else
                {
                    if (Math.Abs(Velocity.Z) < 0.5)
                    {
                        if (_currentAnimation[1] != "base_left_hand_up")
                        {
                            MainPed.Task.ClearAllImmediately();
                            _currentAnimation[1] = "base_left_hand_up";
                        }

                        if (!Call<bool>(IS_ENTITY_PLAYING_ANIM, MainPed.Handle, "laddersbase",
                                "base_left_hand_up", 3))
                            MainPed.Task.PlayAnimation("laddersbase", "base_left_hand_up", 8f, -1, AnimationFlags.Loop);
                    }
                    else
                    {
                        if (_currentAnimation[1] != "climb_up")
                        {
                            MainPed.Task.ClearAllImmediately();
                            _currentAnimation[1] = "climb_up";
                        }

                        if (!Call<bool>(IS_ENTITY_PLAYING_ANIM, MainPed.Handle, "laddersbase", "climb_up",
                                3)) MainPed.Task.PlayAnimation("laddersbase", "climb_up", 8f, -1, AnimationFlags.Loop);
                    }
                }

                SmoothTransition();
                return;
            }

            if (MainPed.IsTaskActive(TaskType.CTaskGoToAndClimbLadder))
            {
                MainPed.Task.ClearAllImmediately();
                _currentAnimation[1] = "";
            }

            if (IsVaulting)
            {
                if (!MainPed.IsVaulting) MainPed.Task.Climb();

                SmoothTransition();

                return;
            }

            if (!IsVaulting && MainPed.IsVaulting) MainPed.Task.ClearAllImmediately();

            if (IsOnFire && !MainPed.IsOnFire)
                Call(START_ENTITY_FIRE, MainPed);
            else if (!IsOnFire && MainPed.IsOnFire) Call(STOP_ENTITY_FIRE, MainPed);

            if (IsJumping)
            {
                if (!_lastIsJumping)
                {
                    _lastIsJumping = true;
                    MainPed.Task.Jump();
                }

                SmoothTransition();
                return;
            }

            _lastIsJumping = false;

            if (IsRagdoll || Health == 0)
            {
                if (!MainPed.IsRagdoll) MainPed.Ragdoll();
                SmoothTransition();
                if (!_lastRagdoll)
                {
                    _lastRagdoll = true;
                    _lastRagdollTime = Ticked;
                }

                return;
            }

            if (MainPed.IsRagdoll)
            {
                if (Speed == 0)
                    MainPed.CancelRagdoll();
                else
                    MainPed.Task.ClearAllImmediately();
                _lastRagdoll = false;
                return;
            }

            if (IsReloading)
            {
                if (!MainPed.IsTaskActive(TaskType.CTaskReloadGun)) MainPed.Task.ReloadWeapon();
                /*
                if (!_isPlayingAnimation)
                {
                    string[] reloadingAnim = MainPed.GetReloadingAnimation();
                    if (reloadingAnim != null)
                    {
                        _isPlayingAnimation = true;
                        _currentAnimation = reloadingAnim;
                        MainPed.Task.PlayAnimation(_currentAnimation[0], _currentAnimation[1], 8f, -1, AnimationFlags.AllowRotation | AnimationFlags.UpperBodyOnly);
                    }
                }
                */
                SmoothTransition();
            }
            else if (IsInCover)
            {
                if (!_lastInCover) Call(TASK_STAY_IN_COVER, MainPed.Handle);

                _lastInCover = true;
                if (IsAiming)
                {
                    DisplayAiming();
                    _lastInCover = false;
                }
                else if (MainPed.IsInCover)
                {
                    SmoothTransition();
                }
            }
            else if (_lastInCover)
            {
                MainPed.Task.ClearAllImmediately();
                _lastInCover = false;
            }
            else if (IsAiming)
            {
                DisplayAiming();
            }
            else if (MainPed.IsShooting)
            {
                MainPed.Task.ClearAllImmediately();
            }
            else
            {
                WalkTo();
            }
        }

        private void CheckCurrentWeapon()
        {
            if (MainPed.VehicleWeapon != VehicleWeapon) MainPed.VehicleWeapon = VehicleWeapon;
            var compChanged = WeaponComponents != null && WeaponComponents.Count != 0 && WeaponComponents != _lastWeaponComponents && !WeaponComponents.Compare(_lastWeaponComponents);
            if (_lastWeaponHash != CurrentWeapon || compChanged)
            {
                if (_lastWeaponHash == WeaponHash.Unarmed && WeaponObj?.Exists() == true)
                {
                    WeaponObj.Delete();
                }
                else
                {
                    var model = Call<uint>(GET_WEAPONTYPE_MODEL, CurrentWeapon);
                    if (!Call<bool>(HAS_MODEL_LOADED, model))
                    {
                        Call(REQUEST_MODEL, model);
                        return;
                    }
                    if (WeaponObj?.Exists() == true)
                        WeaponObj.Delete();
                    MainPed.Weapons.RemoveAll();
                    WeaponObj = Entity.FromHandle(Call<int>(CREATE_WEAPON_OBJECT, CurrentWeapon, -1, Position.X, Position.Y, Position.Z, true, 0, 0));
                }

                if (compChanged)
                {
                    foreach (var comp in WeaponComponents)
                    {
                        if (comp.Value)
                        {
                            Call(GIVE_WEAPON_COMPONENT_TO_WEAPON_OBJECT, WeaponObj.Handle, comp.Key);
                        }
                    }
                    _lastWeaponComponents = WeaponComponents;
                }
                Call(GIVE_WEAPON_OBJECT_TO_PED, WeaponObj.Handle, MainPed.Handle);
                _lastWeaponHash = CurrentWeapon;
            }

            if (Call<int>(GET_PED_WEAPON_TINT_INDEX, MainPed, CurrentWeapon) != WeaponTint)
                Call<int>(SET_PED_WEAPON_TINT_INDEX, MainPed, CurrentWeapon, WeaponTint);
        }

        private void DisplayAiming()
        {
            if (Velocity == default)
                MainPed.Task.AimAt(AimCoords, 1000);
            else
                Call(TASK_GO_TO_COORD_WHILE_AIMING_AT_COORD, MainPed.Handle,
                    Position.X + Velocity.X, Position.Y + Velocity.Y, Position.Z + Velocity.Z,
                    AimCoords.X, AimCoords.Y, AimCoords.Z, 3f, false, 0x3F000000, 0x40800000, false, 512, false, 0);
            SmoothTransition();
        }

        private void WalkTo()
        {
            Call(SET_PED_STEALTH_MOVEMENT, MainPed, IsInStealthMode, 0);
            var predictPosition = Predict(Position) + Velocity;
            var range = predictPosition.DistanceToSquared(MainPed.ReadPosition());

            switch (Speed)
            {
                case 1:
                    if (!MainPed.IsWalking || range > 0.25f)
                    {
                        var nrange = range * 2;
                        if (nrange > 1.0f) nrange = 1.0f;

                        MainPed.Task.GoStraightTo(predictPosition);
                        Call(SET_PED_DESIRED_MOVE_BLEND_RATIO, MainPed.Handle, nrange);
                    }

                    _lastMoving = true;
                    break;
                case 2:
                    if (!MainPed.IsRunning || range > 0.50f)
                    {
                        MainPed.Task.RunTo(predictPosition, true);
                        Call(SET_PED_DESIRED_MOVE_BLEND_RATIO, MainPed.Handle, 1.0f);
                    }

                    _lastMoving = true;
                    break;
                case 3:
                    if (!MainPed.IsSprinting || range > 0.75f)
                    {
                        Call(TASK_GO_STRAIGHT_TO_COORD, MainPed.Handle, predictPosition.X,
                            predictPosition.Y, predictPosition.Z, 3.0f, -1, 0.0f, 0.0f);
                        Call(SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER, MainPed.Handle, 1.49f);
                        Call(SET_PED_DESIRED_MOVE_BLEND_RATIO, MainPed.Handle, 1.0f);
                    }

                    _lastMoving = true;
                    break;
                default:
                    if (_lastMoving)
                    {
                        MainPed.Task.StandStill(2000);
                        _lastMoving = false;
                    }

                    if (MainPed.IsTaskActive(TaskType.CTaskDiveToGround)) MainPed.Task.ClearAll();

                    break;
            }

            SmoothTransition();
        }

        private void SmoothTransition()
        {
            var localRagdoll = MainPed.IsRagdoll;
            var predicted = Predict(Position);
            var dist = predicted.DistanceTo(MainPed.ReadPosition());
            if (IsOff(dist))
            {
                MainPed.PositionNoOffset = predicted;
                return;
            }

            if (!(localRagdoll || MainPed.IsDead))
            {
                if (!IsAiming && !MainPed.IsGettingUp)
                {
                    var cur = MainPed.Heading;
                    var diff = Heading - cur;
                    if (diff > 180)
                        diff -= 360;
                    else if (diff < -180) diff += 360;

                    MainPed.Heading = cur + diff / 2;
                }

                MainPed.Velocity = Velocity + 5 * dist * (predicted - MainPed.ReadPosition());
            }
            else if (Ticked - _lastRagdollTime < 10)
            {
            }
            else if (IsRagdoll)
            {
                var helper = new ApplyImpulseHelper(MainPed);
                var head = MainPed.Bones[Bone.SkelHead];
                var rightFoot = MainPed.Bones[Bone.SkelRightFoot];
                var leftFoot = MainPed.Bones[Bone.SkelLeftFoot];
                Vector3 amount;
                // 20:head, 3:left foot, 6:right foot, 17:right hand, 

                amount = 20 * (Predict(HeadPosition) - head.Position);
                if (amount.Length() > 50) amount = amount.Normalized * 50;
                helper.EqualizeAmount = 1;
                helper.PartIndex = 20;
                helper.Impulse = amount;
                helper.Start();
                helper.Stop();

                amount = 20 * (Predict(RightFootPosition) - rightFoot.Position);
                if (amount.Length() > 50) amount = amount.Normalized * 50;
                helper.EqualizeAmount = 1;
                helper.PartIndex = 6;
                helper.Impulse = amount;
                helper.Start();
                helper.Stop();

                amount = 20 * (Predict(LeftFootPosition) - leftFoot.Position);
                if (amount.Length() > 50) amount = amount.Normalized * 50;
                helper.EqualizeAmount = 1;
                helper.PartIndex = 3;
                helper.Impulse = amount;
                helper.Start();
                helper.Stop();
            }
            else
            {
                // localRagdoll
                var force = Velocity - MainPed.Velocity + 5 * dist * (predicted - MainPed.ReadPosition());
                if (force.Length() > 20) force = force.Normalized * 20;
                MainPed.ApplyWorldForceCenterOfMass(force, ForceType.InternalImpulse, true);
            }
        }

        private void DisplayInVehicle()
        {
            if (CurrentVehicle?.MainVehicle == null) return;
            switch (Speed)
            {
                // In vehicle
                case 4:
                    if (MainPed.CurrentVehicle != CurrentVehicle.MainVehicle || MainPed.SeatIndex != Seat ||
                        (!MainPed.IsSittingInVehicle() && !MainPed.IsBeingJacked))
                        MainPed.SetIntoVehicle(CurrentVehicle.MainVehicle, Seat);
                    if (MainPed.IsOnTurretSeat())
                        Call(TASK_VEHICLE_AIM_AT_COORD, MainPed.Handle, AimCoords.X, AimCoords.Y,
                            AimCoords.Z);

                    // Drive-by
                    if (VehicleWeapon == VehicleWeaponHash.Invalid)
                    {
                        if (IsAiming)
                        {
                            Call(SET_DRIVEBY_TASK_TARGET, MainPed, 0, 0, AimCoords.X, AimCoords.Y,
                                AimCoords.Z);
                            if (!_lastDriveBy)
                            {
                                _lastDriveBy = true;
                                Call(TASK_DRIVE_BY, MainPed, 0, 0, AimCoords.X, AimCoords.Y, AimCoords.Z,
                                    100000, 100, 1, FiringPattern.SingleShot);
                            }
                        }
                        else if (_lastDriveBy || MainPed.IsTaskActive(TaskType.CTaskAimGunVehicleDriveBy))
                        {
                            MainPed.Task.ClearAll();
                            _lastDriveBy = false;
                        }
                    }
                    break;

                // Entering vehicle
                case 5:
                    if (MainPed.VehicleTryingToEnter != CurrentVehicle.MainVehicle ||
                        MainPed.GetSeatTryingToEnter() != Seat)
                        MainPed.Task.EnterVehicle(CurrentVehicle.MainVehicle, Seat, -1, 5,
                            EnterVehicleFlags.JackAnyone);
                    break;

                // Leaving vehicle
                case 6:
                    if (!MainPed.IsTaskActive(TaskType.CTaskExitVehicle))
                        MainPed.Task.LeaveVehicle(CurrentVehicle.Velocity.LengthSquared() > 5 * 5
                            ? LeaveVehicleFlags.BailOut
                            : LeaveVehicleFlags.None);
                    break;
            }
        }
    }
}