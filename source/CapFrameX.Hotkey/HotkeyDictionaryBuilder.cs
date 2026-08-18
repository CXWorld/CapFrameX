using CapFrameX.Contracts.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CapFrameX.Hotkey
{
    /// <summary>
    /// Translates the configured hotkey strings into the combination map <see cref="GlobalHotkeyHook"/>
    /// matches key presses against.
    ///
    /// Every call rebuilds the complete set from configuration, because hotkeys can shadow each
    /// other: two hotkeys sharing a trigger key need to know about each other to decide which
    /// modifier combinations either of them may claim. The registered actions are kept so a
    /// change to one hotkey can rebuild the others without their owners taking part.
    ///
    /// Rebuilding no longer touches the hook itself — it only swaps a snapshot. Re-registering
    /// is therefore cheap and, more importantly, cannot drop keystrokes.
    /// </summary>
    public static class HotkeyDictionaryBuilder
    {
        private static readonly object _syncRoot = new object();

        private static readonly Dictionary<HotkeyAction, Registration> _registeredActions
            = new Dictionary<HotkeyAction, Registration>();

        /// <summary>
        /// Diagnostics sink for the global hook: installation, re-arming and triggered hotkeys.
        /// The hook is a static component outside the DI container, so this is set once during
        /// startup. Without it, a hook that stops working leaves no trace at all.
        /// </summary>
        public static ILogger Logger
        {
            get => GlobalHotkeyHook.Logger;
            set => GlobalHotkeyHook.Logger = value ?? NullLogger.Instance;
        }

        public static void SetHotkey(IAppConfiguration appConfiguration, HotkeyAction hotkeyAction, Action actionToRegister)
        {
            lock (_syncRoot)
            {
                // The synchronization context is captured here, on the caller's thread (the WPF
                // dispatcher for every current caller), and the action is later posted back to
                // it. That keeps the actions running exactly where they used to run while the
                // hook procedure itself returns immediately — see GlobalHotkeyHook.
                _registeredActions[hotkeyAction] = new Registration(actionToRegister, SynchronizationContext.Current);

                GlobalHotkeyHook.SetRegistrations(BuildRegistrations(appConfiguration));
            }
        }

        /// <summary>
        /// Rebuilds the registered hotkey snapshot after configuration was changed outside a
        /// view model, for example through MCP. Existing actions and their synchronization
        /// contexts are retained.
        /// </summary>
        public static void Refresh(IAppConfiguration appConfiguration)
        {
            if (appConfiguration == null)
                throw new ArgumentNullException(nameof(appConfiguration));

            lock (_syncRoot)
            {
                // Before the owning view models have registered their actions there is nothing
                // to refresh. Their later SetHotkey calls will read the already-updated config.
                if (_registeredActions.Count == 0)
                    return;

                GlobalHotkeyHook.SetRegistrations(BuildRegistrations(appConfiguration));
            }
        }

        private static HotkeyRegistration[] BuildRegistrations(IAppConfiguration appConfiguration)
        {
            var actionList = new List<(HotkeyAction hotkeyAction, string key, string combination,
                Dictionary<string, Action> actionDictionary, SynchronizationContext context)>();

            AddToList(appConfiguration.CaptureHotKey, HotkeyAction.Capture, actionList);
            AddToList(appConfiguration.OverlayHotKey, HotkeyAction.Overlay, actionList);
            AddToList(appConfiguration.OverlayConfigHotKey, HotkeyAction.OverlayConfig, actionList);
            AddToList(appConfiguration.OverlayPositionHotkey, HotkeyAction.OverlayPosition, actionList);
            AddToList(appConfiguration.ResetHistoryHotkey, HotkeyAction.ResetHistory, actionList);
            AddToList(appConfiguration.ThreadAffinityHotkey, HotkeyAction.ThreadAffinity, actionList);
            AddToList(appConfiguration.ResetMetricsHotkey, HotkeyAction.ResetMetrics, actionList);

            foreach (var item in actionList)
            {
                RemoveInvalidCombinations(item.combination, item.actionDictionary,
                    actionList.Where(x => x.key == item.key)
                        .Where(x => x.actionDictionary != item.actionDictionary)
                        .Select(x => x.combination));
            }

            // A hotkey whose owner has not registered an action yet can never match. Leaving it
            // out keeps the hook's per-keystroke work proportional to what is actually in use.
            return actionList
                .Where(item => item.actionDictionary.Count > 0)
                .Select(item => new HotkeyRegistration(item.hotkeyAction, item.key, item.actionDictionary, item.context))
                .ToArray();
        }

        private static void AddToList(string hotkey, HotkeyAction hotkeyaction,
            List<(HotkeyAction hotkeyAction, string key, string combination,
                Dictionary<string, Action> actionDictionary, SynchronizationContext context)> actionList)
        {
            var hotkeySplit = hotkey.Split('+');
            var hotkeyChords = hotkeySplit.Take(hotkeySplit.Length - 1);
            // The stored name comes from the WPF Key enum, the hook reports a WinForms Keys name.
            // For keys that carry alias names the two only agree after canonicalization, and this
            // is the single place the trigger key enters — it feeds both the comparison key and
            // the combination strings built below. See HotkeyKeyName.
            var hotkeyTriggerKey = HotkeyKeyName.Canonicalize(hotkeySplit.Last());
            var registration = _registeredActions.TryGetValue(hotkeyaction, out var found) ? found : null;
            var hotkeyActions = registration != null
                ? BuildList(hotkeyTriggerKey, hotkeyChords, registration.Action)
                : new Dictionary<string, Action>();

            actionList.Add((hotkeyaction, hotkeyTriggerKey, hotkey, hotkeyActions, registration?.SynchronizationContext));
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

        private sealed class Registration
        {
            internal Registration(Action action, SynchronizationContext synchronizationContext)
            {
                Action = action;
                SynchronizationContext = synchronizationContext;
            }

            internal Action Action { get; }

            internal SynchronizationContext SynchronizationContext { get; }
        }
    }

    public enum HotkeyAction
    {
        ResetHistory,
        Capture,
        Overlay,
        OverlayConfig,
        OverlayPosition,
        ThreadAffinity,
        ResetMetrics
    }
}
