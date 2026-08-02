using System.Text;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.Graphics;
using Robust.Shared.Graphics.RSI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.Clickable
{
    internal sealed partial class ClickMapManager : IClickMapManager, IPostInjectInit
    {
        private static readonly string[] IgnoreTexturePaths =
        {
            // These will probably never need click maps so skip em.
            "/Textures/Interface",
            "/Textures/LobbyScreens",
            "/Textures/Parallaxes",
            "/Textures/Logo",
        };

        private const float Threshold = 0.1f;
        private const int ClickRadius = 2;

        [Dependency] private IResourceCache _resourceCache = default!;

        [ViewVariables] private readonly Dictionary<Texture, ClickMap> _textureMaps = new();
        [ViewVariables] private readonly Dictionary<RSI, RsiClickMapData> _rsiMaps = new();

        public void PostInject()
        {
            _resourceCache.OnRawTextureLoaded += OnRawTextureLoaded;
            _resourceCache.OnRawTextureUnloaded += OnRawTextureUnloaded;
            _resourceCache.OnRsiLoaded += OnRsiLoaded;
            _resourceCache.OnRsiUnloaded += OnRsiUnloaded;
        }

        private void OnRawTextureUnloaded(Texture texture) => _textureMaps.Remove(texture);

        private void OnRsiUnloaded(RSI rsi) => _rsiMaps.Remove(rsi);

        private void OnRsiLoaded(RsiLoadedEventArgs obj)
        {
            if (obj.Atlas is Image<Rgba32> rgba)
            {
                var clickMap = ClickMap.FromImage(rgba, Threshold);
                if (clickMap.IsFullyTransparent)
                    _rsiMaps.Remove(obj.Resource.RSI);
                else
                    _rsiMaps[obj.Resource.RSI] = new RsiClickMapData(clickMap, obj.AtlasOffset);
            }
        }

        private void OnRawTextureLoaded(TextureLoadedEventArgs obj)
        {
            if (obj.Image is Image<Rgba32> rgba)
            {
                var pathStr = obj.Path.ToString();
                foreach (var path in IgnoreTexturePaths)
                {
                    if (pathStr.StartsWith(path, StringComparison.Ordinal))
                        return;
                }

                UpdateClickMap(_textureMaps, obj.Resource.Texture, ClickMap.FromImage(rgba, Threshold));
            }
        }

        public bool IsOccluding(Texture texture, Vector2i pos)
        {
            if (!_textureMaps.TryGetValue(texture, out var clickMap))
            {
                return false;
            }

            return SampleClickMap(clickMap, pos, clickMap.Size, Vector2i.Zero);
        }

        public bool IsOccluding(RSI rsi, RSI.StateId state, RsiDirection dir, int frame, Vector2i pos)
        {
            if (!_rsiMaps.TryGetValue(rsi, out var rsiData))
            {
                return false;
            }

            if (!rsi.TryGetState(state, out var rsiState))
            {
                return false;
            }

            var direction = (int) dir;
            if ((uint) direction >= (uint) rsiState.Icons.Length)
            {
                return false;
            }

            var frames = rsiState.Icons[direction];
            if ((uint) frame >= (uint) frames.Length || frames[frame] is not AtlasTexture atlasTexture)
            {
                return false;
            }

            return SampleClickMap(rsiData.ClickMap, pos, rsi.Size, (Vector2i) atlasTexture.SubRegion.TopLeft - rsiData.AtlasOffset);
        }

        private static void UpdateClickMap<TKey>(Dictionary<TKey, ClickMap> maps, TKey key, ClickMap clickMap)
            where TKey : class
        {
            if (clickMap.IsFullyTransparent)
                maps.Remove(key);
            else
                maps[key] = clickMap;
        }

        private static bool SampleClickMap(ClickMap map, Vector2i pos, Vector2i bounds, Vector2i offset)
        {
            var (width, height) = bounds;
            var (px, py) = pos;

            if (offset.X < 0 || offset.Y < 0 || offset.X + width > map.Width || offset.Y + height > map.Height)
                return false;

            for (var x = -ClickRadius; x <= ClickRadius; x++)
            {
                var ox = px + x;
                if (ox < 0 || ox >= width)
                {
                    continue;
                }

                for (var y = -ClickRadius; y <= ClickRadius; y++)
                {
                    var oy = py + y;

                    if (oy < 0 || oy >= height)
                    {
                        continue;
                    }

                    if (map.IsOccluded((ox, oy) + offset))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private readonly struct RsiClickMapData
        {
            public readonly ClickMap ClickMap;
            public readonly Vector2i AtlasOffset;

            public RsiClickMapData(ClickMap clickMap, Vector2i atlasOffset)
            {
                ClickMap = clickMap;
                AtlasOffset = atlasOffset;
            }
        }

        internal sealed class ClickMap
        {
            [ViewVariables] private readonly byte[]? _data;
            private readonly bool _uniformOcclusion;

            public int Width { get; }
            public int Height { get; }
            [ViewVariables] public Vector2i Size => (Width, Height);
            public bool IsFullyTransparent => _data == null && !_uniformOcclusion;

            public bool IsOccluded(int x, int y)
            {
                if (_data == null)
                    return _uniformOcclusion;

                var i = y * Width + x;
                return (_data[i / 8] & (1 << (i % 8))) != 0;
            }

            public bool IsOccluded(Vector2i vector)
            {
                var (x, y) = vector;
                return IsOccluded(x, y);
            }

            private ClickMap(byte[]? data, int width, int height, bool uniformOcclusion)
            {
                Width = width;
                Height = height;
                _data = data;
                _uniformOcclusion = uniformOcclusion;
            }

            public static ClickMap FromImage<T>(Image<T> image, float threshold) where T : unmanaged, IPixel<T>
            {
                var threshByte = (byte) (threshold * 255);
                var width = image.Width;
                var height = image.Height;
                var pixelSpan = image.GetPixelSpan();
                byte[]? data = null;
                var firstPixelOccluded = false;
                var hasFirstPixel = false;

                for (var i = 0; i < pixelSpan.Length; i++)
                {
                    Rgba32 rgba = default;
                    pixelSpan[i].ToRgba32(ref rgba);

                    var occluded = rgba.A >= threshByte;
                    if (!hasFirstPixel)
                    {
                        firstPixelOccluded = occluded;
                        hasFirstPixel = true;
                        continue;
                    }

                    if (data == null && occluded != firstPixelOccluded)
                    {
                        data = new byte[(pixelSpan.Length + 7) / 8];
                        if (firstPixelOccluded)
                            SetInitialOpaqueBits(data, i);
                    }

                    if (data != null && occluded)
                    {
                        data[i / 8] |= (byte) (1 << (i % 8));
                    }
                }

                return new ClickMap(data, width, height, firstPixelOccluded);
            }

            private static void SetInitialOpaqueBits(byte[] data, int count)
            {
                var fullBytes = count / 8;
                Array.Fill(data, byte.MaxValue, 0, fullBytes);

                var remainingBits = count % 8;
                if (remainingBits != 0)
                    data[fullBytes] = (byte) ((1 << remainingBits) - 1);
            }

            public string DumpText()
            {
                var sb = new StringBuilder();
                for (var y = 0; y < Height; y++)
                {
                    for (var x = 0; x < Width; x++)
                    {
                        sb.Append(IsOccluded(x, y) ? "1" : "0");
                    }

                    sb.AppendLine();
                }

                return sb.ToString();
            }
        }
    }

    public interface IClickMapManager
    {
        public bool IsOccluding(Texture texture, Vector2i pos);

        public bool IsOccluding(RSI rsi, RSI.StateId state, RsiDirection dir, int frame, Vector2i pos);
    }
}
