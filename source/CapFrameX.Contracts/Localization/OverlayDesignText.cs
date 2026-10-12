using System;

namespace CapFrameX.Contracts.Localization
{
    /// <summary>Keeps error arguments separate from translated UI text and user-owned names.</summary>
    public static class OverlayDesignText
    {
        private const string MessageKey = "CapFrameX.OverlayDesign.LocalizedMessage";

        public static T Error<T>(LocalizedText text, Func<string, T> create) where T : Exception
        {
            var error = create(text.Resolve());
            error.Data[MessageKey] = text;
            return error;
        }

        public static string ExceptionMessage(Exception error) => error.Data[MessageKey] switch
        {
            LocalizedText text => text.Resolve(),
            Func<string> resolve => resolve(),
            _ => error.Message
        };

        public static object Details(Exception error) => new ErrorDetails(error);

        private sealed class ErrorDetails
        {
            private readonly Exception _error;

            public ErrorDetails(Exception error) => _error = error;

            public override string ToString() => ExceptionMessage(_error);
        }
    }
}
