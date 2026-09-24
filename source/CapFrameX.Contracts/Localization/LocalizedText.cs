using System;
using System.Collections.Generic;

namespace CapFrameX.Contracts.Localization
{
    /// <summary>
    /// A catalog key plus its format arguments. Services hand this out instead of finished
    /// English sentences, so the UI can show it in the current language (and re-show it
    /// after a language switch) without parsing English text.
    /// </summary>
    public sealed class LocalizedText
    {
        public LocalizedText(string key, params object[] args)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Args = args ?? Array.Empty<object>();
        }

        public string Key { get; }

        public IReadOnlyList<object> Args { get; }

        /// <summary>The text in the current interface language.</summary>
        public string Resolve()
        {
            var args = new object[Args.Count];
            for (int i = 0; i < args.Length; i++)
                args[i] = Args[i];
            return CxLang.Format(Key, args);
        }

        public override string ToString() => Resolve();
    }
}
