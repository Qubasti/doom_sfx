using BaseLib.Config;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Runs;

namespace doom_sfx.doom_sfxCode
{
    //You're recommended but not required to keep all your code in this package and all your assets in the doom_sfx folder.
    [ModInitializer(nameof(Initialize))]
    public partial class MainFile : Node
    {
        public const string ModId = "doom_sfx"; //At the moment, this is used only for the Logger and harmony names.
        public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

        public static AudioStreamMP3 ApplyDoomAudioLink; // variable for audio config n shit
        public static AudioStreamMP3 DeathDoomAudioLink;

       public static void Initialize()
{
    NormalStream = AudioStreamMP3.LoadFromFile(DoomSfxConfig.NormalAudioPath);
    BoostedStream = AudioStreamMP3.LoadFromFile(DoomSfxConfig.BoostedAudioPath);

    ModConfigRegistry.Register(ModId, new DoomSfxConfig());

    Harmony harmony = new(ModId);
    harmony.PatchAll();
}
    }

   internal class DoomSfxConfig : SimpleModConfig {
    public static bool Enabled { get; set; } = true;

    [ConfigSection("Settings")]
    [ConfigVisibleIfAttribute(nameof(Enabled))]
    public static bool DoThresholdSfx { get; set; } = true;

    [ConfigVisibleIfAttribute(nameof(_ThresholdEnabled))]
    [SliderRange(1, 100)]
    public static int ThresholdValue { get; set; } = 20;

    // added these new configs
    [ConfigSection("Audio Files")]
    [ConfigVisibleIfAttribute(nameof(Enabled))]
    public static string ApplyDoomAudioLink { get; set; } = "res://.godot/imported/doom.mp3-89fe55d2785ac93ed4da2ea3779df44f.mp3str";

    [ConfigVisibleIfAttribute(nameof(Enabled))]
    public static string DeathDoomAudioLink { get; set; } = "res://.godot/imported/doom_bass_boosted.mp3-800311521de7ada1a4dee6c1e3663817.mp3str";

    public static bool _ThresholdEnabled() {
        return Enabled && DoThresholdSfx;
    }
}


    [HarmonyPatch(typeof(Hook))]
    [HarmonyPatch(nameof(Hook.AfterActEntered))]
    class LoadSfx {
        static void Prefix(IRunState runState) {

        }
    }

    [HarmonyPatch(typeof(Hook))]
    [HarmonyPatch(nameof(Hook.AfterPowerAmountChanged))]
    class ThresholdPatch {
        static void Prefix(CombatState combatState, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource) {
            if (power.GetType() == typeof(DoomPower)) {
                MainFile.Logger.Info(amount.ToString() + " Doom applied!");

                if (DoomSfxConfig.Enabled && DoomSfxConfig.DoThresholdSfx) {
                    if (amount >= DoomSfxConfig.ThresholdValue) {
                        AudioStreamPlayer player = new AudioStreamPlayer();
                        NAudioManager.Instance!.AddChild(player);
                        player.Finished += player.QueueFree;
                        player.Stream = MainFile.NormalStream;
                        player.VolumeDb = -15;
                        player.Play();
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(DoomPower))]
    [HarmonyPatch(nameof(DoomPower.PlayVfx))]
    class DeathPatch {
        static void Prefix(Creature creature) {
            MainFile.Logger.Info(creature.ToString() + " died to Doom!");

            if (DoomSfxConfig.Enabled) {
                AudioStreamPlayer player = new AudioStreamPlayer();
                NAudioManager.Instance!.AddChild(player);
                player.Finished += player.QueueFree;
                player.VolumeDb = -14;

                if (DoomSfxConfig.DoThresholdSfx) {
                    player.Stream = MainFile.BoostedStream;
                } else {
                    player.Stream = MainFile.NormalStream;
                }

                player.Play();
            }
        }
    }
}
