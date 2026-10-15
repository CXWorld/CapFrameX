using System;
using System.Collections.Generic;

namespace CapFrameX.Mcp
{
    /// <summary>
    /// A tool result with explicit content blocks, for tools that return more than one JSON text,
    /// such as a rendered image together with its description. Other return values are serialized
    /// into a single text block.
    /// </summary>
    public sealed class McpToolResult
    {
        public IList<McpToolContentBlock> Content { get; } = new List<McpToolContentBlock>();

        public McpToolResult AddText(string text)
        {
            Content.Add(new McpToolContentBlock("text", text ?? string.Empty, null, null));
            return this;
        }

        public McpToolResult AddImage(byte[] data, string mimeType)
        {
            if (data == null || data.Length == 0) throw new ArgumentException("Image data is required.", nameof(data));
            Content.Add(new McpToolContentBlock("image", null, Convert.ToBase64String(data), mimeType ?? "image/png"));
            return this;
        }
    }

    public sealed class McpToolContentBlock
    {
        internal McpToolContentBlock(string type, string text, string data, string mimeType)
        {
            Type = type;
            Text = text;
            Data = data;
            MimeType = mimeType;
        }

        public string Type { get; }
        public string Text { get; }
        /// <summary>Base64 payload of an image block.</summary>
        public string Data { get; }
        public string MimeType { get; }
    }
}
