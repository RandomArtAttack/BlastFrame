using System;
using UnityEngine;
using BlastFrame.Core.Audio;

namespace BlastFrame.Core.Services
{
    /// <summary>Async additive scene transitions. Implemented by the SceneLoader service.</summary>
    public interface ISceneLoader
    {
        Awaitable LoadAdditiveAsync(string sceneName);
        Awaitable UnloadAsync(string sceneName);
        bool IsLoaded(string sceneName);
    }

    /// <summary>Plays audio cues. Systems never reference the AudioManager directly except via this.</summary>
    public interface IAudioManager
    {
        void Play(AudioCueSO cue);
        void PlayAt(AudioCueSO cue, Vector3 position);
        void PlayMusic(AudioCueSO cue);
        void SetVolume(string mixerParam, float linear01);
    }

    /// <summary>Reads/writes the meta currency wallet.</summary>
    public interface ICurrencyManager
    {
        int MetaCurrency { get; }
        void Add(int amount);
        bool TrySpend(int amount);
        event Action<int> OnCurrencyChanged;
    }

    /// <summary>Owns the active run: current level/room, difficulty, run-scoped powerups.</summary>
    public interface IRunManager
    {
        bool RunActive { get; }
        BlastFrame.Core.Difficulty Difficulty { get; }
        int CurrentLevelIndex { get; }
        int CurrentRoomIndex { get; }
        void StartRun(int levelIndex, BlastFrame.Core.Difficulty difficulty);
        void EndRun(bool died);
    }

    /// <summary>HQ permanent-upgrade purchasing.</summary>
    public interface IShopManager
    {
        bool IsOwned(string upgradeId);
        bool TryPurchase(string upgradeId);
    }

    /// <summary>Save/load of meta progression. SOs are never serialized — only ids.</summary>
    public interface ISaveManager
    {
        void Save();
        void Load();
        BlastFrame.Core.Save.SaveData Data { get; }
    }

    /// <summary>Spawns/returns pooled objects by pool id.</summary>
    public interface IPoolManager
    {
        GameObject Spawn(string poolId, Vector3 position, Quaternion rotation);
        void Despawn(GameObject instance);
    }

    /// <summary>Enum-driven game flow FSM. Orchestrates; holds no gameplay logic.</summary>
    public interface IGameStateMachine
    {
        BlastFrame.Core.GameState CurrentState { get; }
        void TransitionTo(BlastFrame.Core.GameState next);
        event Action<BlastFrame.Core.GameState> OnStateChanged;
    }
}
