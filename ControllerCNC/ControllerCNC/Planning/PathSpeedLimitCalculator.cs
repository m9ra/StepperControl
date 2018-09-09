﻿using ControllerCNC.Machine;
using ControllerCNC.Primitives;
using GeometryCNC.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Planning
{
    class PathSpeedLimitCalculator
    {
        internal readonly ToolPathSegment ActiveSegment;

        internal bool NeedsMoreFollowingSegments { get; private set; }

        internal int FollowingSegmentCount => _edgeDistances.Count - 1;

        private readonly List<ToolPathSegment> _followingSegments = new List<ToolPathSegment>();

        private readonly List<double> _edgeDistances = new List<double>();

        private readonly List<double> _edgeLimits = new List<double>();

        private readonly double _timeGrain;

        private static readonly int[] _accelerationRanges;

        static PathSpeedLimitCalculator()
        {
            var acceleration = AccelerationBuilder.FromTo(Configuration.ReverseSafeSpeed, Configuration.MaxPlaneSpeed, Configuration.MaxPlaneAcceleration, 10000);
            var instruction = acceleration.ToInstruction();
            _accelerationRanges = instruction.GetStepTimings();
        }


        internal PathSpeedLimitCalculator(ToolPathSegment activeSegment, double timeGrain)
        {
            NeedsMoreFollowingSegments = true;
            ActiveSegment = activeSegment;

            _timeGrain = timeGrain;
            _edgeDistances.Add(0);
            _edgeLimits.Add(Configuration.ReverseSafeSpeed.ToMetric());
        }

        internal static double TransitionSpeedTo(double currentSpeed, double targetSpeed, double timeGrain)
        {
            if (currentSpeed == targetSpeed)
                return currentSpeed;

            var direction = Math.Sign(targetSpeed - currentSpeed);

            var i1 = GetAccelerationIndex(currentSpeed, direction >= 0);
            var i2 = GetAccelerationIndex(targetSpeed, direction >= 0);
            if (Math.Abs(i1 - i2) <= 1)
            {
                return targetSpeed;
            }

            var nextDeltaT = _accelerationRanges[i1 + direction];
            return Speed.FromDeltaT(nextDeltaT).ToMetric();
        }

        internal static int GetAccelerationIndex(double speed, bool isAcceleration)
        {
            if (speed == 0)
                speed = Configuration.ReverseSafeSpeed.ToMetric();

            var ticks = Speed.FromMilimetersPerSecond(speed).ToDeltaT();

            if (isAcceleration)
            {
                for (var i = 0; i < _accelerationRanges.Length; ++i)
                {
                    if (ticks >= _accelerationRanges[i])
                    {
                        return i;
                    }
                }

                return _accelerationRanges.Length;

            }
            else
            {
                for (var i = _accelerationRanges.Length - 1; i >= 0; --i)
                {
                    if (ticks <= _accelerationRanges[i])
                    {
                        return i;
                    }
                }

                return 0;
            }
        }

        internal static double GetAxisLimit(double r1, double r2)
        {
            var ar1 = Math.Abs(r1);
            var ar2 = Math.Abs(r2);
            var maxScaleFactor = Math.Max(ar1, ar2);
            var minScaleFactor = Math.Min(ar1, ar2);

            if (r1 == r2)
                return Configuration.MaxPlaneSpeed.ToMetric();

            var limit = Speed.FromDeltaT(Configuration.StartDeltaT).ToMetric() / maxScaleFactor;

            if (r1 * r2 < 0 || r1 * r2 == 0)
                // direction change
                return limit;

            for (var i = 0; i < _accelerationRanges.Length - 1; ++i)
            {
                //TODO binary search
                var ac1 = Speed.FromDeltaT(_accelerationRanges[i]).ToMetric();
                var ac2 = Speed.FromDeltaT(_accelerationRanges[i + 1]).ToMetric();

                var sp1 = ac1;
                var sp2 = ac1 / minScaleFactor;

                if (sp2 >= ac2)
                    break;

                limit = ac1 / maxScaleFactor;
            }

            return limit;
        }

        internal double GetLimit(double positionPercentage)
        {
            var activeSegmentRemainingLength = (ActiveSegment.End - ActiveSegment.Start).Length * (1.0 - positionPercentage);

            var lowestLimit = Speed.FromDeltaT(Configuration.FastestDeltaT).ToMetric();
            for (var i = 0; i < _edgeDistances.Count; ++i)
            {
                var distanceToEdge = _edgeDistances[i] + activeSegmentRemainingLength;
                var edgeLimit = _edgeLimits[i];

                var limit = accelerateFrom(edgeLimit, distanceToEdge);
                lowestLimit = Math.Min(lowestLimit, limit);
            }

            return lowestLimit;
        }

        internal void AddFollowingSegments(IEnumerable<ToolPathSegment> workSegments, Dictionary<ToolPathSegment, double> edgeLimits)
        {
            foreach (var segment in workSegments)
            {
                if (!NeedsMoreFollowingSegments)
                    break;

                var currentLimit = edgeLimits[segment];
                var currentDistance = segment.Length;

                var closingLimit = _edgeLimits.Last();
                var totalDistance = _edgeDistances.Last() + currentDistance;

                _edgeLimits[_edgeDistances.Count - 1] = currentLimit;
                _edgeDistances.Add(totalDistance);
                _edgeLimits.Add(closingLimit);

                var reachableSpeed = accelerateFrom(Configuration.ReverseSafeSpeed.ToMetric(), totalDistance);
                NeedsMoreFollowingSegments = reachableSpeed <= Configuration.MaxPlaneSpeed.ToMetric();
            }
        }

        private double accelerateFrom(double startingSpeed, double distance)
        {
            var maxSpeed = Configuration.MaxPlaneSpeed.ToMetric();

            var actualSpeed = startingSpeed;
            var actualDistance = 0.0;
            do
            {
                var grainDistance = _timeGrain * actualSpeed;
                actualSpeed = TransitionSpeedTo(actualSpeed, double.PositiveInfinity, _timeGrain);

                actualDistance += grainDistance;
            } while (actualDistance < distance);
            return actualSpeed;
        }
    }
}
