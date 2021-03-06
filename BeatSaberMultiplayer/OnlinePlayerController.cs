using BeatSaberMultiplayer.Data;
using BeatSaberMultiplayer.Misc;
using BeatSaberMultiplayer.OverriddenClasses;
using BS_Utils.Gameplay;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeatSaberMultiplayer
{
    public class OnlinePlayerController : PlayerController
    {
        private const int _voipDelay = 1;

        public PlayerInfo playerInfo;
        public AvatarController avatar;
        public AudioSource voipSource;

        public OnlineBeatmapCallbackController beatmapCallbackController;
        public OnlineBeatmapSpawnController beatmapSpawnController;
        public OnlineAudioTimeController audioTimeController;

        public float avatarOffset;
        public bool noInterpolation = false;
        public bool destroyed = false;

        private PlayerUpdate _syncStartInfo;
        private PlayerUpdate _syncEndInfo;

        private AudioClip _voipClip;
        private int _lastVoipFragIndex;
        private int _voipWritePos;
        private int _silentFrames;
        private int _voipDelayCounter;

        public float syncDelay = 0f;
        private float lastSynchronizationTime = 0f;
        private float syncTime = 0f;
        private float lerpProgress = 0f;

        public void Start()
        {
#if DEBUG
            Plugin.log.Info($"Player controller created!");
#endif
            voipSource = gameObject.AddComponent<AudioSource>();

            _voipClip = AudioClip.Create("VoIP Clip", 161280, 1, 16000, false);
            voipSource.clip = _voipClip;
            voipSource.spatialize = Config.Instance.SpatialAudio;
            voipSource.loop = true;

            if (playerInfo != null)
            {
#if DEBUG
                Plugin.log.Info($"Starting player controller for {playerInfo.playerName}:{playerInfo.playerId}...");
#endif
                _syncStartInfo = playerInfo.updateInfo;
                _syncStartInfo = playerInfo.updateInfo;
            }

            if (SceneManager.GetActiveScene().name == "GameCore" && Config.Instance.ShowOtherPlayersBlocks && !Client.Instance.playerInfo.Equals(playerInfo) && !Config.Instance.SpectatorMode)
            {
                SpawnBeatmapControllers();
                SpawnSabers();
            }
        }

        void SpawnBeatmapControllers()
        {
            Plugin.log.Info("Creating beatmap controllers...");

            beatmapCallbackController = new GameObject("OnlineBeatmapCallbackController").AddComponent<OnlineBeatmapCallbackController>();
            Plugin.log.Info("Created beatmap callback controller!");
            beatmapCallbackController.Init(this);
            Plugin.log.Info("Initialized beatmap callback controller!");

            audioTimeController = new GameObject("OnlineAudioTimeController").AddComponent<OnlineAudioTimeController>();
            Plugin.log.Info("Created audio time controller!");
            audioTimeController.Init(this);
            Plugin.log.Info("Initialized audio time controller!");

            beatmapSpawnController = new GameObject("OnlineBeatmapSpawnController").AddComponent<OnlineBeatmapSpawnController>();
            Plugin.log.Info("Created beatmap spawn controller!");
            beatmapSpawnController.Init(this, beatmapCallbackController, audioTimeController);
            Plugin.log.Info("Initialized beatmap spawn controller!");
        }

        void SpawnSabers()
        {
            Plugin.log.Info("Spawning left saber...");
            _leftSaber = Instantiate(Resources.FindObjectsOfTypeAll<Saber>().First(x => x.name == "LeftSaber"), transform, false);
            _leftSaber.gameObject.name = "CustomLeftSaber";
            var leftController = _leftSaber.gameObject.AddComponent<OnlineVRController>();
            leftController.owner = this;
            _leftSaber.SetPrivateField("_vrController", leftController);

            var leftTrail = leftController.GetComponentInChildren<SaberWeaponTrail>();
            var colorManager = Resources.FindObjectsOfTypeAll<ColorManager>().First();
            leftTrail.SetPrivateField("_colorManager", colorManager);
            leftTrail.SetPrivateField("_saberTypeObject", leftController.GetComponentInChildren<SaberTypeObject>());

            Plugin.log.Info("Spawning right saber...");
            _rightSaber = Instantiate(Resources.FindObjectsOfTypeAll<Saber>().First(x => x.name == "RightSaber"), transform, false);
            _rightSaber.gameObject.name = "CustomRightSaber";
            var rightController = _rightSaber.gameObject.AddComponent<OnlineVRController>();
            rightController.owner = this;
            _rightSaber.SetPrivateField("_vrController", rightController);

            var rightTrail = rightController.GetComponentInChildren<SaberWeaponTrail>();
            rightTrail.SetPrivateField("_colorManager", colorManager);
            rightTrail.SetPrivateField("_saberTypeObject", rightController.GetComponentInChildren<SaberTypeObject>());


            Plugin.log.Info("Sabers spawned!");
        }

        public void SetSabers(Saber leftSaber, Saber rightSaber)
        {
            _leftSaber = leftSaber;
            _rightSaber = rightSaber;
        }

        public override void Update()
        {
            if (avatar != null)
            {
                avatar.SetPlayerInfo(playerInfo, avatarOffset, Client.Instance.playerInfo.Equals(playerInfo));
            }

            if (voipSource != null)
            {
                if(voipSource.timeSamples > _voipWritePos && !(voipSource.timeSamples > _voipClip.samples / 2 && _voipWritePos < _voipClip.samples / 2))
                {
                    voipSource.Stop();
                    Plugin.log.Warn("We read past received data!");
                    voipSource.timeSamples = 0;
                }

                if (voipSource.isPlaying)
                {
                    _silentFrames = 0;
                }
            }
            else
            {
                _silentFrames = 999;
            }

            if (_rightSaber != null)
            {
                _rightSaber.ManualUpdate();
            }

            if (_leftSaber != null)
            {
                _leftSaber.ManualUpdate();
            }
        }

        public void FixedUpdate()
        {
            if (playerInfo != null && playerInfo.updateInfo != default)
            {
                if (!noInterpolation)
                {
                    syncTime += Time.fixedDeltaTime;

                    lerpProgress = syncTime / syncDelay;

                    playerInfo.updateInfo.headPos = Vector3.Lerp(_syncStartInfo.headPos, _syncEndInfo.headPos, lerpProgress);
                    playerInfo.updateInfo.leftHandPos = Vector3.Lerp(_syncStartInfo.leftHandPos, _syncEndInfo.leftHandPos, lerpProgress);
                    playerInfo.updateInfo.rightHandPos = Vector3.Lerp(_syncStartInfo.rightHandPos, _syncEndInfo.rightHandPos, lerpProgress);

                    playerInfo.updateInfo.headRot = Quaternion.Lerp(_syncStartInfo.headRot, _syncEndInfo.headRot, lerpProgress);
                    playerInfo.updateInfo.leftHandRot = Quaternion.Lerp(_syncStartInfo.leftHandRot, _syncEndInfo.leftHandRot, lerpProgress);
                    playerInfo.updateInfo.rightHandRot = Quaternion.Lerp(_syncStartInfo.rightHandRot, _syncEndInfo.rightHandRot, lerpProgress);

                    if (_syncStartInfo.fullBodyTracking)
                    {
                        playerInfo.updateInfo.leftLegPos = Vector3.Lerp(_syncStartInfo.leftLegPos, _syncEndInfo.leftLegPos, lerpProgress);
                        playerInfo.updateInfo.rightLegPos = Vector3.Lerp(_syncStartInfo.rightLegPos, _syncEndInfo.rightLegPos, lerpProgress);
                        playerInfo.updateInfo.pelvisPos = Vector3.Lerp(_syncStartInfo.pelvisPos, _syncEndInfo.pelvisPos, lerpProgress);

                        playerInfo.updateInfo.leftLegRot = Quaternion.Lerp(_syncStartInfo.leftLegRot, _syncEndInfo.leftLegRot, lerpProgress);
                        playerInfo.updateInfo.rightLegRot = Quaternion.Lerp(_syncStartInfo.rightLegRot, _syncEndInfo.rightLegRot, lerpProgress);
                        playerInfo.updateInfo.pelvisRot = Quaternion.Lerp(_syncStartInfo.pelvisRot, _syncEndInfo.pelvisRot, lerpProgress);
                    }

                    float lerpedPlayerProgress = Mathf.Lerp(_syncStartInfo.playerProgress, _syncEndInfo.playerProgress, lerpProgress);

                    if(playerInfo.updateInfo.playerProgress < lerpedPlayerProgress && Mathf.Abs(playerInfo.updateInfo.playerProgress - lerpedPlayerProgress) < 0.5f)
                    {
                        playerInfo.updateInfo.playerProgress = lerpedPlayerProgress;
                    }
                    else
                    {
                        playerInfo.updateInfo.playerProgress = _syncStartInfo.playerProgress;
                    }
                }

                _overrideHeadPos = true;
                _overriddenHeadPos = playerInfo.updateInfo.headPos;
                _headPos = playerInfo.updateInfo.headPos + Vector3.right * avatarOffset;
                transform.position = _headPos;
            }
        }

        public void OnDestroy()
        {
#if DEBUG
            if(playerInfo == null)
                Plugin.log.Info("Destroying player controller!");
            else
                Plugin.log.Info($"Destroying player controller! Name: {playerInfo.playerName}, ID: {playerInfo.playerId}");
#endif
            destroyed = true;
            
            if (avatar != null)
            {
                Destroy(avatar.gameObject);
            }

            if (beatmapCallbackController != null && beatmapSpawnController != null && audioTimeController != null)
            {
                Destroy(beatmapCallbackController.gameObject, 2f);
                Destroy(audioTimeController.gameObject, 2f);
                Destroy(beatmapSpawnController.gameObject, 2f);
                beatmapSpawnController.PrepareForDestroy();
            }
        }

        public void UpdateInfo(PlayerUpdate newInfo)
        {
            if (playerInfo == null)
                return;

            if (noInterpolation)
            {
                playerInfo.updateInfo = newInfo;
                return;
            }
            
            _syncStartInfo = playerInfo.updateInfo;
            if (_syncStartInfo.IsRotNaN())
            {
                _syncStartInfo.headRot = Quaternion.identity;
                _syncStartInfo.leftHandRot = Quaternion.identity;
                _syncStartInfo.rightHandRot = Quaternion.identity;
                _syncStartInfo.leftLegRot = Quaternion.identity;
                _syncStartInfo.rightLegRot = Quaternion.identity;
                _syncStartInfo.pelvisRot = Quaternion.identity;
                Plugin.log.Warn("Start rotation is NaN!");
            }

            if (Mathf.Abs(playerInfo.updateInfo.playerProgress - newInfo.playerProgress) > 0.1f)
            {
                playerInfo.updateInfo.playerProgress = newInfo.playerProgress;
            }

            _syncEndInfo = newInfo;
            if (_syncEndInfo.IsRotNaN())
            {
                _syncEndInfo.headRot = Quaternion.identity;
                _syncEndInfo.leftHandRot = Quaternion.identity;
                _syncEndInfo.rightHandRot = Quaternion.identity;
                _syncEndInfo.leftLegRot = Quaternion.identity;
                _syncEndInfo.rightLegRot = Quaternion.identity;
                _syncEndInfo.pelvisRot = Quaternion.identity;
                Plugin.log.Warn("Target rotation is NaN!");
            }
            
            syncTime = 0;
            syncDelay = Time.time - lastSynchronizationTime;

            if(syncDelay > 0.5f)
            {
                syncDelay = 0.5f;
            }

            lastSynchronizationTime = Time.time;
        }

        public void NewUpdateReceived(PlayerUpdate value)
        {
            UpdateInfo(value);
            if (playerInfo != null)
            {
                playerInfo.updateInfo.playerNameColor = value.playerNameColor;
                playerInfo.updateInfo.playerState = value.playerState;

                playerInfo.updateInfo.fullBodyTracking = value.fullBodyTracking;
                playerInfo.updateInfo.playerScore = value.playerScore;
                playerInfo.updateInfo.playerCutBlocks = value.playerCutBlocks;
                playerInfo.updateInfo.playerComboBlocks = value.playerComboBlocks;
                playerInfo.updateInfo.playerTotalBlocks = value.playerTotalBlocks;
                playerInfo.updateInfo.playerEnergy = value.playerEnergy;
                playerInfo.updateInfo.playerLevelOptions = value.playerLevelOptions;
            }
        }

        public void SetAvatarState(bool enabled)
        {
            if(enabled && (object)avatar == null)
            {
                avatar = new GameObject("AvatarController").AddComponent<AvatarController>();
                avatar.SetPlayerInfo(playerInfo, avatarOffset, Client.Instance.playerInfo.Equals(playerInfo));
            }
            else if(!enabled && avatar != null)
            {
                Destroy(avatar.gameObject);
                avatar = null;
            }
        }

        public void VoIPUpdate()
        {
            _silentFrames++;
        }

#if DEBUG && VERBOSE
        int lastPlayPos;
        int lastFrame;
#endif

        public void PlayVoIPFragment(float[] data, int fragIndex)
        {
            if(voipSource != null && !InGameOnlineController.Instance.mutedPlayers.Contains(playerInfo.playerId))
            {
                if ((_lastVoipFragIndex + 1) != fragIndex || _silentFrames > 15)
                {
#if DEBUG && VERBOSE
                    Plugin.log.Info($"Starting from scratch! ((_lastVoipFragIndex + 1) != fragIndex): {(_lastVoipFragIndex + 1) != fragIndex}, (_silentFrames > 20): {_silentFrames > 20}, _lastVoipFragIndex: {_lastVoipFragIndex}, fragIndex: {fragIndex}");
#endif

                    _lastVoipFragIndex = fragIndex;

                    _voipClip.SetData(data, 0);
                    _voipWritePos = data.Length;

                    _silentFrames = 0;
                    _voipDelayCounter = 0;
                }
                else
                {
                    _lastVoipFragIndex = fragIndex;
                    
                    if (_voipWritePos + data.Length > _voipClip.samples)
                    {
                        if (_voipWritePos < _voipClip.samples)
                        {
                            _voipClip.SetData(data, _voipWritePos);

                            int remaining = data.Length - (_voipClip.samples - _voipWritePos);

                            float[] buffer = new float[remaining];

                            Buffer.BlockCopy(data, data.Length - remaining, buffer, 0, remaining);

                            _voipClip.SetData(buffer, 0);

                            _voipWritePos = remaining;
                        }
                        else
                        {
                            _voipClip.SetData(data, 0);
                            _voipWritePos = data.Length;
                        }
                    }
                    else
                    {
                        if (voipSource.timeSamples > _voipWritePos - 256 && !(voipSource.timeSamples > _voipClip.samples / 2 && _voipWritePos < _voipClip.samples / 2))
                        {
                            voipSource.timeSamples = _voipWritePos - (_voipClip.frequency / 15);
                        }
                        _voipClip.SetData(data, _voipWritePos);
                        _voipWritePos += data.Length;

                    }

#if DEBUG && VERBOSE
                    Plugin.log.Info($"New data ({data.Length}) at pos {_voipFrames} while playing at {voipSource.timeSamples}, Overlap: {voipSource.timeSamples > _voipFrames && !(voipSource.timeSamples > _voipClip.samples/2 && _voipFrames < _voipClip.samples / 2)}, Delay: {_voipFrames - voipSource.timeSamples}, Speed: {voipSource.timeSamples - lastPlayPos}, Frames: {Time.frameCount - lastFrame}");

                    lastPlayPos = voipSource.timeSamples;
                    lastFrame = Time.frameCount;
#endif

                    _voipDelayCounter++;

                    if (!voipSource.isPlaying && _voipDelayCounter >= _voipDelay)
                    {
                        voipSource.timeSamples = 0;
                        voipSource.Play();
                    }
                    _silentFrames = 0;
                }
            }
        }

        public void SetVoIPVolume(float newVolume)
        {
            if(voipSource != null)
            {
                voipSource.volume = newVolume;
            }
        }

        public void SetSpatialAudioState(bool spatialAudio)
        {
            if (voipSource != null)
            {
                voipSource.spatialize = spatialAudio;
            }
        }

        public bool IsTalking()
        {
            return _silentFrames < 20;
        }
    }
}
