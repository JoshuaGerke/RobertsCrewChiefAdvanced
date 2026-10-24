using System;
using System.Collections.Generic;
using GameOverlay.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CrewChiefV4.Overlay
{
    class ElementGroupBox : OverlayElement
    {
        bool outlined;
        public ElementGroupBox(Graphics gfx, string elementTitle, Font font, System.Windows.Rect rectangle,
            ColorScheme colorScheme, bool initialEnableState = true, bool outlined = true) :
            base(gfx, elementTitle, font, rectangle, colorScheme, initialEnableState)
        {
            //parent = this;
            this.outlined = outlined;
        }
        public override void drawElement()
        {
            if (!this.elementEnabled)
                return;
            System.Windows.Rect rect = getAbsolutePosition();
            /*if (parent != null && parent != this)
            {
                rect.Y += parent.rectangle.Y;
                rect.X += parent.rectangle.X;
            }*/
            if (outlined)
                gfx.DrawBox2D(base.secondaryBrush, base.primaryBrush, new Rectangle(rect), 1);
            else
                gfx.FillRectangle(base.primaryBrush, new Rectangle(rect));

            foreach (var child in children)
            {
                child.drawElement();
            }

            return;
        }
    }
}
