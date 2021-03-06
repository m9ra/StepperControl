﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

using System.Runtime.Serialization;

using ControllerCNC.Primitives;

namespace ControllerCNC.GUI
{
    [Serializable]
    internal class EntryPoint : PointProviderItem
    {
        /// <summary>
        /// Size of displayed entry point.
        /// </summary>
        internal readonly static double EntryPointVisualDiameter = 20;

        /// <inheritdoc/>
        internal override IEnumerable<Point4Dstep> CutPoints
        {
            get { return new[] { new Point4Dstep(PositionC1, PositionC2, PositionC1, PositionC2) };  }
        }

        internal EntryPoint()
            : base(new ReadableIdentifier("START"))
        {
            PositionC1 = 5000;
            PositionC2 = 5000;
            initialize();
        }

        internal EntryPoint(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            initialize();
        }

        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }

        /// <inheritdoc/>
        internal override void Build(WorkspacePanel workspace, List<Speed4Dstep> speedPoints, ItemJoin incommingJoin)
        {
            if (incommingJoin != null)
                throw new NotSupportedException("Incomming join point has to be empty for entry point.");

            foreach (var join in workspace.FindOutgoingJoins(this))
            {
                join.Build(workspace, speedPoints);
            }
        }

        /// <inheritdoc/>
        protected override object createContent()
        {
            var entryPoint = new Ellipse();
            entryPoint.Width = EntryPointVisualDiameter;
            entryPoint.Height = EntryPointVisualDiameter;
            entryPoint.RenderTransform = new TranslateTransform(-EntryPointVisualDiameter / 2, -EntryPointVisualDiameter / 2);

            var brush = new SolidColorBrush(Colors.Green);
            brush.Opacity = 1.0;
            entryPoint.Fill = brush;

            return entryPoint;
        }
    }
}
