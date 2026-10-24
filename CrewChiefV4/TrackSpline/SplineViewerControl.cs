using CrewChiefV4.ACE.aceData;

using System;
using System.Drawing;
using System.Windows.Forms;

namespace CrewChiefV4.TrackSpline
{
    public class SplineViewerControl : Control
    {
        public TrackSpline Spline { get; set; }

        public bool DrawNodes = false;
        public bool DrawDirection = true;

        private float _minX, _maxX, _minZ, _maxZ;
        private bool _boundsValid;

        public SplineViewerControl()
        {
            this.DoubleBuffered = true;
            this.ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.Clear(Color.Black);

            if (Spline == null || Spline.nodes == null || Spline.nodes.Count < 2)
                return;

            if (!_boundsValid)
                ComputeBounds();

            SetSectors(0.33f, 0.66f);
            DrawSpline(e.Graphics);
        }

        private void ComputeBounds()
        {
            _minX = float.MaxValue;
            _maxX = float.MinValue;
            _minZ = float.MaxValue;
            _maxZ = float.MinValue;

            for (int i = 0; i < Spline.nodes.Count; i++)
            {
                var p = Spline.nodes[i].pos;

                if (p.x < _minX) _minX = p.x;
                if (p.x > _maxX) _maxX = p.x;
                if (p.z < _minZ) _minZ = p.z;
                if (p.z > _maxZ) _maxZ = p.z;
            }

            _boundsValid = true;
        }

        private float _s1End = -1f;
        private float _s2End = -1f;
        private float _startFinish = 0f;
        public void SetSectors(float s1End, float s2End)
        {
            _s1End = s1End;
            _s2End = s2End;
            Invalidate();
        }

        private Color GetSectorColor(float t)
        {
            if (_s1End < 0 || _s2End < 0)
                return Color.White;

            if (t < _s1End)
                return Color.LimeGreen;   // Sector 1
            else if (t < _s2End)
                return Color.Orange;      // Sector 2
            else
                return Color.DeepSkyBlue; // Sector 3
        }

        public void SetStartFinish(float t)
        {
            _startFinish = t;
            Invalidate();
        }
        private bool CrossesStartFinish(float t0, float t1)
        {
            if (t1 < t0) // wrap segment
                t1 += 1f;

            float sf = _startFinish;

            if (sf < t0) sf += 1f;

            return sf >= t0 && sf <= t1;
        }

        private void DrawStartFinish(Graphics g, PointF p1, PointF p2)
        {
            float dx = p2.X - p1.X;
            float dy = p2.Y - p1.Y;

            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f)
                return;

            // Normalize track direction
            dx /= len;
            dy /= len;

            // Perpendicular vector (90 degrees)
            float px = -dy;
            float py = dx;

            // Midpoint of spline segment
            float mx = (p1.X + p2.X) * 0.5f;
            float my = (p1.Y + p2.Y) * 0.5f;

            // Half width of finish line
            float halfWidth = 12f;

            PointF a = new PointF(
                mx + px * halfWidth,
                my + py * halfWidth
            );

            PointF b = new PointF(
                mx - px * halfWidth,
                my - py * halfWidth
            );

            using (Pen pen = new Pen(Color.White, 3f))
            {
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawLine(pen, a, b);
            }
        }

        private void DrawSpline(Graphics g)
        {
            int count = Spline.nodes.Count;

            float width = this.ClientSize.Width;
            float height = this.ClientSize.Height;

            float scaleX = width / (_maxX - _minX);
            float scaleZ = height / (_maxZ - _minZ);

            float scale = Math.Min(scaleX, scaleZ) * 0.9f; // margin

            float offsetX = (width - (_maxX - _minX) * scale) * 0.5f;
            float offsetZ = (height - (_maxZ - _minZ) * scale) * 0.5f;

            bool drawnStartFinish = false;

            Pen splinePen = Pens.White;
            Brush nodeBrush = Brushes.Red;

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1 < count) ? i + 1 : 0;

                var n0 = Spline.nodes[i];
                var n1 = Spline.nodes[next];

                PointF p1 = WorldToScreen(n0.pos, scale, offsetX, offsetZ);
                PointF p2 = WorldToScreen(n1.pos, scale, offsetX, offsetZ);

                float tMid = (n0.t + n1.t) * 0.5f;

                Color color = GetSectorColor(tMid);

                using (Pen pen = new Pen(color, 2f))
                {
                    g.DrawLine(pen, p1, p2);
                }

                // Start/finish marker
                if (!drawnStartFinish && CrossesStartFinish(n0.t, n1.t))
                {
                    DrawStartFinish(g, p1, p2);
                    drawnStartFinish = true;
                }

                if (DrawNodes)
                    g.FillEllipse(Brushes.White, p1.X - 2, p1.Y - 2, 4, 4);
            }
            
            //for (int i = 0; i < count; i++)
            //{
            //    int next = (i + 1 < count) ? i + 1 : 0;

            //    PointF p1 = WorldToScreen(Spline.nodes[i].pos, scale, offsetX, offsetZ);
            //    PointF p2 = WorldToScreen(Spline.nodes[next].pos, scale, offsetX, offsetZ);

            //    g.DrawLine(splinePen, p1, p2);

            //    if (DrawDirection)
            //    {
            //        DrawArrow(g, p1, p2);
            //    }

            //    if (DrawNodes)
            //    {
            //        g.FillEllipse(nodeBrush, p1.X - 2, p1.Y - 2, 4, 4);
            //    }
            //}
        }

        private PointF WorldToScreen(aceVec3 p, float scale, float offsetX, float offsetZ)
        {
            // Using X/Z plane (Z inverted for screen Y)
            float x = (p.x - _minX) * scale + offsetX;
            float y = (p.z - _minZ) * scale + offsetZ;

            return new PointF(x, y);
        }

        private void DrawArrow(Graphics g, PointF from, PointF to)
        {
            float dx = to.X - from.X;
            float dy = to.Y - from.Y;

            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) return;

            dx /= len;
            dy /= len;

            float size = 6f;

            PointF left = new PointF(
                to.X - dx * size - dy * size * 0.5f,
                to.Y - dy * size + dx * size * 0.5f
            );

            PointF right = new PointF(
                to.X - dx * size + dy * size * 0.5f,
                to.Y - dy * size - dx * size * 0.5f
            );

            g.DrawLine(Pens.Gray, to, left);
            g.DrawLine(Pens.Gray, to, right);
        }

        public void RefreshSpline()
        {
            _boundsValid = false;
            this.Invalidate();
        }
    }
}