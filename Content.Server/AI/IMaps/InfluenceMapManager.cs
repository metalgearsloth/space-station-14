using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Maths;

namespace Content.Server.AI.IMaps
{
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
                    break;
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