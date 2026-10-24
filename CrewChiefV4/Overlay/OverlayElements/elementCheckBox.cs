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
    public class ElementCheckBox : OverlayElement
    {
        public bool enabled = false;
        public string subscriptionDataField;
        
        public ElementCheckBox(Graphics gfx, string elementTitle, Font font, Rect rectangle,
            ColorScheme colorScheme, string subscriptionDataField = "", bool initialEnabled = false) 
            : base(gfx, elementTitle, font, rectangle, colorScheme)
        {
            this.subscriptionDataField = subscriptionDataField;
            this.enabled = initialEnabled;
            this.includeFontRectInMouseOver = true;
        }
        public override void initialize()
        {
            if(enabled)
                this.OnElementLMButtonClicked?.Invoke(this, new OverlayElementClicked(gfx, enabled: enabled, costumTextId: ""));
        }
        public override void drawElement()
        {
            if (!this.elementEnabled)
                return;
            System.Windows.Rect rect = getAbsolutePosition();

            gfx.DrawBox2D(base.secondaryBrush, base.primaryBrush, new Rectangle(rect), mouseOver || selected ? 2 : 1);
            gfx.DrawText(base.font, 12, base.secondaryBrush, (float)rect.Right + 4, (float)rect.Y, base.title);

            if (enabled)
            {
                rect.Y += (rectangle.Height / 2);
                rect.X += (rectangle.Width / 2);
                gfx.DrawCrosshair(base.secondaryBrush, (float)rect.X, (float)rect.Y, (float)(rectangle.Width / 2), 1, CrosshairStyle.Diagonal);                
            }
            return;
        }
        public override bool OnWindowMessage(WindowMessage message, IntPtr wParam, IntPtr lParam)
        {
            //base.OnWindowMessage(message, wParam, lParam);
            if (selected && message == WindowMessage.Keydown && ((Keys)wParam == Keys.Return || (Keys)wParam == Keys.Enter || (Keys)wParam == Keys.Space))
            {
                enabled = !enabled;
                OnEnterKeyDown?.Invoke(this, new OverlayElementClicked(gfx, enabled: enabled, costumTextId: subscriptionDataField));
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
                    enabled = !enabled;
                    this.OnElementLMButtonClicked?.Invoke(this, new OverlayElementClicked(gfx, enabled: enabled, costumTextId: subscriptionDataField));
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
