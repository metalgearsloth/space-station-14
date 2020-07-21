using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.AI.IMaps
{
    public sealed class InfluenceMapLayer
    {
        public const int ChunkSize = 16;
        public InfluenceMapType MapType;

        public InfluenceMapLayer(InfluenceMapType mapType)
        {
            MapType = mapType;
        }
    }

    /// <summary>
    /// A single chunk may contain multiple influencemaps (e.g. 1 per faction)
    /// so this will store all of the relevant ones per chunk.
    /// </summary>
    public sealed class InfluenceMapCollection
    {
        private Dictionary<InfluenceMapType, InfluenceMapLayer> _influenceMaps = 
            new Dictionary<InfluenceMapType, InfluenceMapLayer>();

        // TODO: May need a filter function that can pull out multiple map types
        public InfluenceMapLayer GetOrCreateMap(InfluenceMapType mapType)
        {
            if (_influenceMaps.TryGetValue(mapType, out var map))
            {
                return map;
            }
            
            map = new InfluenceMapLayer(mapType);
            _influenceMaps[mapType] = map;

            return map;
        }
    }
    
    public sealed class InfluenceMapSystem : EntitySystem
    {
        private readonly Dictionary<GridId, Dictionary<MapIndices, InfluenceMapCollection>> _influenceMaps = 
                     new Dictionary<GridId, Dictionary<MapIndices, InfluenceMapCollection>>();

        public InfluenceMapCollection GetOrCreateMapCollection(TileRef tile)
        {
            var originIndices = new MapIndices(
                tile.GridIndices.X / InfluenceMapLayer.ChunkSize, 
                tile.GridIndices.Y / InfluenceMapLayer.ChunkSize);

            if (!_influenceMaps.TryGetValue(tile.GridIndex, out var layerMapCollections))
            {
                layerMapCollections = new Dictionary<MapIndices, InfluenceMapCollection>();
                _influenceMaps[tile.GridIndex] = layerMapCollections;
            }

            if (!layerMapCollections.TryGetValue(originIndices, out var collection))
            {
                collection = new InfluenceMapCollection();
                _influenceMaps[tile.GridIndex][originIndices] = collection;
            }

            return collection;
        }
        
        public InfluenceMapLayer GetOrCreateMap(InfluenceMapType mapType, TileRef tile)
        {
            var collection = GetOrCreateMapCollection(tile);
            return collection.GetOrCreateMap(mapType);
        }

        /// <summary>
        /// If an entity is near the border of a chunk they may need multiple influencemapcollections
        /// </summary>
        /// <returns></returns>
        public IEnumerable<InfluenceMapLayer> GetRelevantLayerMaps(InfluenceMapType mapType, TileRef tile, int radius)
        {
            yield return GetOrCreateMapCollection(tile).GetOrCreateMap(mapType);
            
            // TODO: Get neighbors
            throw new NotImplementedException();
        }
    }

    // 29:34 ways of processing influence
    // Remove last influence if they've changed meaningfully
    // Add their current influence
    // Can be timesliced
    // More or less what the pathfinder's doing I think
    //
    
    // IMO we just queue MoveEvents and then every second we'll tick through (using latest position)
    
    
    // KR talk
    // https://www.gdcvault.com/play/1267/(307)-Beyond-Behavior-An-Introduction
    
    
    // 18:30 level of confidence in data
    // Could use Perceived Target positions (not in talk but could have TimeStamp of last-seen targets in a State)
    // 28:35 shared stuff
    // 31 end of first half
    // Confidence
    // Salience
    // Prediction
    
    // Have a "PerceivedEntityPosition" state that has a timestamp and GridCoordinates; gets updated if entity in LOS
    // Seek Behavior goes to entity's last known position and tries to search nearby.
    // Could also open nearby lockers


    // InfluenceMaps talk
    // https://www.gdcvault.com/play/1014498/Lay-of-the-Land-Smarter
    

    public class InfluenceMapManager
    {
        // TODO: Could probably just dump these in a dictionary and use reflection
        private List<InfluenceMapTemplate> _proximityTemplates = new List<InfluenceMapTemplate>();
        
        // 16:10 for the actual IMapManager
        private float _cellSize;
        
        private int _numFactions;

        private int _mapWidthInCells;
        private int _mapHeightInCells;
        // Number of influence maps across the world map
        private int _mapCountWidth;
        private int _mapCountHeight;

        // How often do we want to update this Imap
        private float _updateFrequency;

        // width of world (Grid?)
        private float _realmWidthInMeters;
        private float _realmHeightInMeters;

        private Vector2 _anchorPoint;
        
        // 12:20
        public void Initialize()
        {
            InitializeProxMapTemplates(12, 1);
        }
        
        // 16:31 helper functions
        
        // 17:34
        // Imap Templates
        // private List<IMapTemplate> proxMapTemplates; // Templates of various sizes of proximity maps
        
        // Tactical Imaps
        // private LayerMapCollection proxMaps // Proximity Maps (per faction)
        
        // private readonly List<EntityNode> imapEntityNodes;
        
        // LayerMaps -> Stored in a dictionary, identified by its ID. Value is Imapobject
        
        // Multiple Layers of Maps (LayerMapCollection)

        // 12:52
        private void InitializeProxMapTemplates(int maxRadius, int increments = 1)
        {
            for (int radius = 1; radius < maxRadius; radius += increments)
            {
                int size = 2 * radius + 1; // adding 1 ensures odd number of rows / columns so there is a "center" row and column
                var newMap = new InfluenceMap(size, size);
                newMap.PropagateInfluenceFromCenter(radius, new InfluenceCurve(), 1.0f);
                var template = new InfluenceMapTemplate(radius, InfluenceMapType.Proximity, newMap);
                _proximityTemplates.Add(template);
            }
        }

        private InfluenceMap GetTemplate(InfluenceMapType templateType, int radius)
        {
            switch (templateType)
            {
                case InfluenceMapType.Invalid:
                    throw new InvalidOperationException();
                case InfluenceMapType.Proximity:
                    foreach (var template in _proximityTemplates)
                    {
                        if (radius <= template.Radius)
                        {
                            return template.InfluenceMap;
                        }
                    }

                    return _proximityTemplates.Last().InfluenceMap;
                default:
                    throw new ArgumentOutOfRangeException(nameof(templateType), templateType, null);
            }
            
            throw new InvalidOperationException();
        }
    }
}