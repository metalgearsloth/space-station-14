using System;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Server.AI.IMaps
{
    //See https://www.gdcvault.com/play/1025243/Spatial-Knowledge-Representation-through-Modular
    // 6:56 Falloff Rates
    public class Utils
    {
        
    }

    public class InfluenceMap
    {
        private Vector2 _anchorLocation;
        private float _cellSize;
        private int _height;
        private int _width;
        private float[,] _mapGrid;

        public InfluenceMap(int newWidth, int newHeight, float x = 0.0f, float y = 0.0f, int newCellSize = 1)
        {
            _width = newWidth;
            _height = newHeight;
            _anchorLocation = new Vector2(x, y);
            _cellSize = newCellSize;
            _mapGrid = new float[_width,_height];
        }

        public void AddValue(int x, int y, float value)
        {
            if (x >= 0 && x < _width && y >= 0 && y < _height)
            {
                var existing = _mapGrid[x, y];
                _mapGrid[x, y] = existing + value;
            }
        }

        public void SetCellValue(int x, int y, float value)
        {
            if (x >= 0 && x < _width && y >= 0 && y < _height)
            {
                _mapGrid[x, y] = value;
            }
        }

        public float GetCellValue(int x, int y)
        {
            return (x >= 0 && x < _width && y >= 0 && y < _height) ? _mapGrid[x, y] : 0.0f;
        }

        public void PropagateInfluence(int centerX, int centerY, int radius, InfluenceCurve propType, float magnitude)
        {
            int startX = centerX - (radius / 2);
            int startY = centerY - (radius / 2);
            int endX = centerX + (radius / 2);
            int endY = centerY + (radius / 2);

            int minX = Math.Max(0, startX);
            int maxX = Math.Min(endX, _width);
            int minY = Math.Max(0, startY);
            int maxY = Math.Min(endY, _height);

            for (var y = minY; y < maxY; y++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    float distance = 0.0f; // GetDistance(y, centerY, x, centerX);
                    _mapGrid[x, y] = 0.0f;  // PropValue(distance, propType);
                }
            }
        }

        public void PropagateInfluenceFromCenter(int radius, InfluenceCurve propType, float magnitude)
        {
            // IMap always adds 1 so there's an odd number of rows / columns
            var center = radius + 1;
            
            PropagateInfluence(center, center, radius, propType, magnitude);
        }

        // 14:53 ; used for combining maps
        public void AddMap(InfluenceMap source, int centerX, int centerY, float magnitude = 1.0f, int offsetX = 0,
            int offsetY = 0)
        {
            DebugTools.AssertNotNull(source);
            
            // Locate the upper left corner of where the new map is going to be located
            int startX = centerX + offsetX - (source._width / 2);
            int startY = centerY + offsetY - (source._height / 2);

            for (var y = 0; y < source._height; y++)
            {
                for (var x = 0; x < source._width; x++)
                {
                    int targetX = x + startX; // Location is offset based on center point we are placing it at
                    int targetY = y + startY;
                    
                    // Make sure we are addressing a legit cell in the target map
                    if (targetX >= 0 && targetX < _width && targetY >= 0 && targetY < _height)
                    {
                        _mapGrid[targetX, targetY] += source.GetCellValue(x, y) * magnitude;
                    }
                }
            }
        }
        
        // 15:18
        public void AddIntoMap(InfluenceMap targetMap, int centerX, int centerY, float magnitude = 1.0f,
            int offsetX = 0, int offsetY = 0)
        {
            DebugTools.AssertNotNull(targetMap);

            int startX = centerX + offsetX - (targetMap._width >> 1);
            int startY = centerY + offsetY - (targetMap._height >> 1);

            int negAdjX = 0;
            int negAdjY = 0;
            
            // because of the +/- coordinate scheme, we need to account for where we are looking on the imaps to avoid overlap
            if (_anchorLocation.X < 0.0f)
            {
                negAdjX -= 1;
            }

            if (_anchorLocation.Y < 0.0f)
            {
                negAdjY = -1;
            }

            var minX = Math.Max(0, negAdjX - startX);
            var maxX = Math.Min(targetMap._width, _width - startX + negAdjX);
            var minY = Math.Max(0, negAdjY - startY);
            var maxY = Math.Min(targetMap._height, _height - startY + negAdjY);

            for (int y = minY; y < maxY; y++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    int sourceX = x + startX - negAdjX;
                    int sourceY = y + startY - negAdjY;
                    
                    targetMap.AddValue(x, y, GetCellValue(sourceX, sourceY) * magnitude);
                }
            }
        }
    }
    
    // 11:55
    public struct InfluenceMapTemplate
    {
        public int Radius { get; }
        public InfluenceMapType TemplateType { get; }
        public InfluenceMap InfluenceMap { get; }

        public InfluenceMapTemplate(int radius, InfluenceMapType templateType, InfluenceMap influenceMap)
        {
            Radius = radius;
            TemplateType = templateType;
            InfluenceMap = influenceMap;
        }
    }

    public enum InfluenceMapType
    {
        Invalid = 0,
        Proximity,
    }

    public class InfluenceCurve
    {
        
    }
}