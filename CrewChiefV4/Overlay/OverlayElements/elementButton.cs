using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using GameOverlay.Drawing;
using GameOverlay.PInvoke;
using GameOverlay.Windows;

namespace CrewChiefV4.Overlay
{
    public class ElementButton : OverlayElement
    {        
        public ElementButton(Graphics gfx, string elementTitle, Font font, System.Windows.Rect rectangle,
            ColorScheme colorScheme) :
            base( gfx, elementTitle, font, rectangle, colorScheme)
        {         
        }

        public override void drawElement()
        {
            if (!this.elementEnabled)
                return;
            System.Windows.Rect rect = getAbsolutePosition();
            if (title == "ButtonClose" && (mouseOver || selected))
            {

                gfx.DrawBox2D(base.primaryBrush, base.secondaryBrush, new Rectangle(rect), 1);
                gfx.DrawCrosshair(base.primaryBrush, (float)rectangle.X + 7, (float)rectangle.Y + 7, 7, 1, CrosshairStyle.Diagonal);

            }
            else if (title == "ButtonClose")
            {
                gfx.DrawBox2D(base.secondaryBrush, base.primaryBrush, new Rectangle(rect), 1);
                gfx.DrawCrosshair(base.secondaryBrush, (float)rectangle.X + 7, (float)rectangle.Y + 7, 7, 1, CrosshairStyle.Diagonal);
            }
            else
            {
                gfx.DrawBox2D(base.secondaryBrush, base.primaryBrush, new Rectangle(rect), mouseOver || selected ? 2 : 1);
                gfx.DrawTextCenterInRect(font, secondaryBrush, rect, title);
            }
            return;
        }
        public override bool OnWindowMessage(WindowMessage message, IntPtr wParam, IntPtr lParam)
        {
            if (selected && message == WindowMessage.Keydown && ((Keys)wParam == Keys.Return || (Keys)wParam == Keys.Enter || (Keys)wParam == Keys.Space))
            {
                OnEnterKeyDown?.Invoke(this, new OverlayElementClicked(gfx));
                return true;
            }
            if (mouseOver)
            {
                if (message == WindowMessage.Lbuttondown)
                {
                    mousePressed = true;
                }
                if (message == WindowMessage.Lbuttonup && mousePressed)
                {
                    mousePressed = false;
                    this.OnElementLMButtonClicked?.Invoke(this, new OverlayElementClicked(gfx));
                }
                return true;
            }
            else
            {
                mousePressed = false;
            }
            return false;
        }
    }
}
