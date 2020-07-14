using System;
using Robust.Shared.Map;
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
        private GridCoordinates _gridCoordinates;
        
        // TODO: I don't this can be a discrete MapIndices... Because the influencemap gets reused
        //private Vector2 _anchorLocation;
        // World location of the map (center?)
        private readonly Vector2 _anchorLocation;
        // Size of the clel in world units
        private float _cellSize; // Could just be ints for tiles?
        private int _height;
        private int _width;
        // Actual contents
        private float[,] _mapGrid;

        public InfluenceMap(int newWidth, int newHeight, Vector2 anchorLocation = default, float newCellSize = 1)
        {
            _gridCoordinates = GridCoordinates.InvalidGrid;
            _anchorLocation = anchorLocation;
            _width = newWidth;
            _height = newHeight;
            _cellSize = newCellSize;
            _mapGrid = new float[_width,_height];
        }

        private bool InBounds(float x, float y)
        {
            return x >= _anchorLocation.X && 
                   y >= _anchorLocation.Y && 
                   x < _anchorLocation.X + _width && 
                   y < _anchorLocation.Y + _height;
        }

        public void AddValue(float x, float y, float value)
        {
            if (!InBounds(x, y))
            {
                return;
            }
            
            var xOffset = indices.X - _anchorLocation.X;
            var yOffset = indices.Y - _anchorLocation.Y;

            _mapGrid[xOffset, yOffset] += value;
        }

        public void SetCellValue(float x, float y, float value)
        {
            if (!InBounds(x, y))
            {
                return;
            }
            
            var xOffset = indices.X - _center.X;
            var yOffset = indices.Y - _center.Y;
            
            _mapGrid[xOffset, yOffset] = value;
        }

        public float GetCellValue(float x, float y)
        {
            if (!InBounds(x, y))
            {
                return 0.0f;
            }
            
            var xOffset = indices.X - _center.X;
            var yOffset = indices.Y - _center.Y;
            
            return _mapGrid[xOffset, yOffset];
        }

        public void PropagateInfluence(int centerX, int centerY, int radius, InfluenceCurve propType, float magnitude = 1.0f)
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
                    var distance = GetDistance(x, y, centerX, centerY);
                    _mapGrid[x, y] = PropValue(distance, propType) * magnitude;
                }
            }
        }

        private float PropValue(float distance, InfluenceCurve curve)
        {
            return 1.0f;
        }

        private static float GetDistance(MapIndices source, MapIndices target)
        {
            var xDiff = Math.Abs(source.X - target.X);
            var yDiff = Math.Abs(source.Y - target.Y);
            return MathF.Sqrt(xDiff ^ 2 + yDiff ^ 2);
        }

        public void PropagateInfluenceFromCenter(int radius, InfluenceCurve propType, float magnitude)
        {
            PropagateInfluence(_anchorLocation.X, _anchorLocation.Y, radius, propType, magnitude);
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