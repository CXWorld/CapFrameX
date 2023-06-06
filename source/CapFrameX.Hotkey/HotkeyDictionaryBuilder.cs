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

            AddToList(appConfiguration.CaptureHotKey, HotkeyAction.Capture, actionList);
            AddToList(appConfiguration.OverlayHotKey, HotkeyAction.Overlay, actionList);
            AddToList(appConfiguration.OverlayConfigHotKey, HotkeyAction.OverlayConfig, actionList);
            AddToList(appConfiguration.ResetHistoryHotkey, HotkeyAction.ResetHistory, actionList);
			AddToList(appConfiguration.ThreadAffinityHotkey, HotkeyAction.ThreadAffinity, actionList);

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

        private static void AddToList(string hotkey, HotkeyAction hotkeyaction, List<(string key, string combination, Dictionary<string, Action> actionDictionary)> actionList)
        {
            var hotkeySplit = hotkey.Split('+');
            var hotkeyChords = hotkeySplit.Take(hotkeySplit.Length - 1);
            var hotkeyTriggerKey = hotkeySplit.Last();
            var hotkeyActions = dict.TryGetValue(hotkeyaction, out var action) ? BuildList(hotkeyTriggerKey, hotkeyChords, action) : new Dictionary<string, Action>();
            actionList.Add((hotkeyTriggerKey, hotkey, hotkeyActions));
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
        OverlayConfig,
        ThreadAffinity,
		ResetMetrics
	}
}
