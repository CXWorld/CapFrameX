using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using CapFrameX.Contracts.Localization;

namespace CapFrameX.Test.Localization
{
    /// <summary>
    /// View models subscribe to <see cref="CxLang"/> for the lifetime of the app. MSTest runs every
    /// STA test on a thread of its own, so a view model left subscribed by one test would handle the
    /// next test's language switch and touch chart objects of a finished thread. Create this before
    /// the view model and dispose it at the end of the test to remove what the test subscribed.
    /// </summary>
    internal sealed class CxLangSubscriptions : IDisposable
    {
        private static readonly FieldInfo PropertyChangedField = typeof(CxLang).GetField(
            nameof(CxLang.PropertyChanged), BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo OverlayLanguageChangedField = typeof(CxLang).GetField(
            nameof(CxLang.OverlayLanguageChanged), BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly Delegate[] _propertyChanged = Handlers(PropertyChangedField);
        private readonly Delegate[] _overlayLanguageChanged = Handlers(OverlayLanguageChangedField);

        public void Dispose()
        {
            // WPF's weak event managers listen once per thread for all bindings and are left alone.
            foreach (var handler in Handlers(PropertyChangedField).Except(_propertyChanged))
            {
                if (!(handler.Target is WeakEventManager))
                    CxLang.Instance.PropertyChanged -= (PropertyChangedEventHandler)handler;
            }
            foreach (var handler in Handlers(OverlayLanguageChangedField).Except(_overlayLanguageChanged))
                CxLang.Instance.OverlayLanguageChanged -= (Action)handler;
        }

        private static Delegate[] Handlers(FieldInfo field)
            => ((Delegate)field.GetValue(CxLang.Instance))?.GetInvocationList() ?? Array.Empty<Delegate>();
    }
}
