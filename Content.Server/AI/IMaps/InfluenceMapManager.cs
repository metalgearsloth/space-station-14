using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.AI.IMaps
{

    public class GridLayerMapCollection
    {
        public const int MapHeightInTiles = 32;
        private Dictionary<TileRef, Dictionary<Type, InfluenceMap>> _mapLayerStored = new Dictionary<TileRef, Dictionary<Type, InfluenceMap>>();

        // 23:47
        public void AddMapLayer(Type layerId, TileRef tile, InfluenceMap influenceMap)
        {
            Dictionary<Type, InfluenceMap> layer;
            if (!_mapLayerStored.TryGetValue(tile, out layer))
            {
                _mapLayerStored[tile] = new Dictionary<Type, InfluenceMap> {{layerId, influenceMap}};
            } else if (!layer.ContainsKey(layerId))
            {
                layer[layerId] = influenceMap;
            }
        }

        public InfluenceMap CheckAndAddMapLayer(Type layerId, InfluenceMapType mapType, TileRef tile)
        {
            if (MapInBounds(tile))
            {
                var map = GetLayerMapCollection(mapType).GetMapLayer(layerId, tile);
                if (map == null)
                {
                    var thisIndex = (0, 0);
                    map = AddMapLayer(llayerId, mapType, tile);
                }

                return map;
            }

            return null;
        }
        
        // 24:15
        public IEnumerable<KeyValuePair<Type, InfluenceMap>> GetMapLayers(TileRef tile,
            Func<KeyValuePair<Type, InfluenceMap>, bool> filter = null)
        {
            Dictionary<Type, InfluenceMap> layer;
            if (_mapLayerStored.TryGetValue(tile, out layer))
            {
                foreach (var keyValuePair in layer)
                {
                    if (filter == null || filter(keyValuePair))
                    {
                        yield return keyValuePair;
                    }
                }
            }
        }

        // 26:39
        private IEnumerable<InfluenceMap> GetTouchedMaps(Type layerId, InfluenceMapType mapType, TileRef mapIndex, int radius, TileRef center)
        {
            // Okay so something I need to do: imapIndex increments by 0 / 1 for each cell so uhhh
            // Might need to normalise the TileRef X / Y indices
            
            // Returns a list of references to the maps that are touched by the templated based on location and radius
            // Check center and each corner
            var currentMap = CheckAndAddMapLayer(layerId, mapType, mapIndex);
            if (currentMap != null)
            {
                yield return currentMap;
            }
            
            // Check if we are overlapping in any direction
            // NorthWest
            if ((mapIndex.X - radius < 0) && center.Y + radius > MapHeightInTiles)
            {
                currentMap = CheckAndAddMapLayer(layerId, mapType,)
            }
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