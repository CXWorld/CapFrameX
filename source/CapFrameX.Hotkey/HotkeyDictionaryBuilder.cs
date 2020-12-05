using CapFrameX.Contracts.Configuration;
using Gma.System.MouseKeyHook;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Hotkey
{

    public static class HotkeyDictionaryBuilder
    {
        private static Dictionary<HotkeyAction, Action> dict = new Dictionary<HotkeyAction, Action>();
        private static List<IKeyboardMouseEvents> registeredEvents = new List<IKeyboardMouseEvents>();

        public static void SetHotkey(IAppConfiguration appConfiguration, HotkeyAction hotkeyAction, Action actionToRegister)
        {
            // save action to reconstruct later
            dict[hotkeyAction] = actionToRegister;

            foreach (var registeredEvent in registeredEvents)
            {
                registeredEvent.Dispose();
            }

            var actionList = new List<(string key, string combination, Dictionary<string, Action> actionDictionary)>();
            // building list
            var captureHotkeySplit = appConfiguration.CaptureHotKey.Split('+');
            var captureHotkeyChords = captureHotkeySplit.Take(captureHotkeySplit.Length - 1);
            var captureHotkeyTriggerKey = captureHotkeySplit.Last();
            var captureHotkeyActions = dict.TryGetValue(HotkeyAction.Capture, out var action) ? BuildList(captureHotkeyTriggerKey, captureHotkeyChords, action) : new Dictionary<string, Action>();
            actionList.Add((captureHotkeyTriggerKey, appConfiguration.CaptureHotKey, captureHotkeyActions));


            var overlayHotkeySplit = appConfiguration.OverlayHotKey.Split('+');
            var overlayHotkeyChords = overlayHotkeySplit.Take(overlayHotkeySplit.Length - 1);
            var overlayHotkeyTriggerKey = overlayHotkeySplit.Last();
            var overlayHotkeyActions = dict.TryGetValue(HotkeyAction.Overlay, out var action1) ? BuildList(overlayHotkeyTriggerKey, overlayHotkeyChords, action1) : new Dictionary<string, Action>();
            actionList.Add((overlayHotkeyTriggerKey, appConfiguration.OverlayHotKey, overlayHotkeyActions));

            var configHotkeySplit = appConfiguration.OverlayConfigHotKey.Split('+');
            var configHotkeyChords = configHotkeySplit.Take(configHotkeySplit.Length - 1);
            var configHotkeyTriggerKey = configHotkeySplit.Last();
            var configHotkeyActions = dict.TryGetValue(HotkeyAction.OverlayConfig, out var action2) ? BuildList(configHotkeyTriggerKey, configHotkeyChords, action2) : new Dictionary<string, Action>();
            actionList.Add((configHotkeyTriggerKey, appConfiguration.OverlayConfigHotKey, configHotkeyActions));

            var resetHistoryHotkeySplit = appConfiguration.ResetHistoryHotkey.Split('+');
            var resetHistoryHotkeyChords = resetHistoryHotkeySplit.Take(resetHistoryHotkeySplit.Length - 1);
            var resetHistoryHotkeyTriggerKey = resetHistoryHotkeySplit.Last();
            var resetHistoryHotkeyActions = dict.TryGetValue(HotkeyAction.ResetHistory, out var action3) ? BuildList(resetHistoryHotkeyTriggerKey, resetHistoryHotkeyChords, action3) : new Dictionary<string, Action>();
            actionList.Add((resetHistoryHotkeyTriggerKey, appConfiguration.ResetHistoryHotkey, resetHistoryHotkeyActions));

            foreach (var item in actionList)
            {
                RemoveInvalidCombinations(item.combination, item.actionDictionary, actionList.Where(x => x.key == item.key).Where(x => x.actionDictionary != item.actionDictionary).Select(x => x.combination));
            }

            foreach (var item in actionList)
            {
                var hook = Hook.GlobalEvents();
                hook.OnCXCombination(item.key, item.actionDictionary);
                registeredEvents.Add(hook);
            }
        }

        private static Dictionary<string, Action> BuildList(string key, IEnumerable<string> chords, Action action)
        {
            var dict = new Dictionary<string, Action>();

            if (chords.Count() == 2)
            { 
                dict[chords.ElementAt(0) + "+" + chords.ElementAt(1) + "+" + key] = action;
                dict["Control+Shift+Alt+" + key] = action;
            }
            else
            {
                if (!chords.Any())
                {
                    dict[key] = action;
                    chords = new string[] { "Control", "Shift", "Alt" };
                }

                foreach (var chord in chords)
                {
                    switch (chord)
                    {
                        case "Control":
                            dict["Control+" + key] = action;
                            dict["Control+Shift+" + key] = action;
                            dict["Control+Alt+" + key] = action;
                            dict["Control+Shift+Alt+" + key] = action;
                            break;
                        case "Shift":
                            dict["Shift+" + key] = action;
                            dict["Shift+Alt+" + key] = action;
                            dict["Control+Shift+" + key] = action;
                            dict["Control+Shift+Alt+" + key] = action;
                            break;
                        case "Alt":
                            dict["Alt+" + key] = action;
                            dict["Control+Alt+" + key] = action;
                            dict["Shift+Alt+" + key] = action;
                            dict["Control+Shift+Alt+" + key] = action;
                            break;
                    }
                }
            }
            return dict;
        }

        private static void RemoveInvalidCombinations(string hotkey, Dictionary<string, Action> toBeModified, IEnumerable<string> othersWithSameTriggerkey)
        {
            if (othersWithSameTriggerkey.Any())
            {
                var chordsOfActualHotkey = GetChordsFromCombination(hotkey);
                foreach (var other in othersWithSameTriggerkey)
                {
                    var chordsOfOther = GetChordsFromCombination(other);
                    var intersectingChords = chordsOfOther.Intersect(chordsOfActualHotkey);
                    var chordsToRemove = chordsOfOther.Where(c => !intersectingChords.Contains(c));

                    if (chordsToRemove.Count() == 2)
                    {
                        foreach (var keyToRemove in toBeModified.Keys.Where(k => k.Contains(chordsToRemove.ElementAt(0)) && k.Contains(chordsToRemove.ElementAt(1))).ToArray())
                        {
                                toBeModified.Remove(keyToRemove);
                        }
                    }
                    else if (chordsToRemove.Count() == 1)
                    {
                        foreach (var keyToRemove in toBeModified.Keys.Where(k => k.Contains(chordsToRemove.ElementAt(0))).ToArray())
                        {
                            toBeModified.Remove(keyToRemove);
                        }
                    }
                }
            }

            IEnumerable<string> GetChordsFromCombination(string combination)
            {
                var splitted = combination.Split('+');
                var chords = splitted.Take(splitted.Length - 1);
                return chords;
            }
        }
    }

    public enum HotkeyAction
    {
        ResetHistory,
        Capture,
        Overlay,
        OverlayConfig
    }
}
