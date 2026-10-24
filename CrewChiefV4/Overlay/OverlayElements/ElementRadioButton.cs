using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using GameOverlay.Drawing;
using GameOverlay.PInvoke;


namespace CrewChiefV4.Overlay
{
    class ElementRadioButton : OverlayElement
    {
        public bool enabled = false;
        private string costumCommand = null;
        public ElementRadioButton(Graphics gfx, string elementTitle, Font font, Rect rectangle,
            ColorScheme colorScheme, bool isChecked = false, string costumCommand = null) :
            base(gfx, elementTitle, font, rectangle, colorScheme)
        {
            this.elementEnabled = true;
            this.costumCommand = costumCommand;
            this.enabled = isChecked;
            this.includeFontRectInMouseOver = true;
        }
        public override void initialize()
        {
            if (enabled)
                this.OnElementLMButtonClicked?.Invoke(this, new OverlayElementClicked(gfx, enabled, costumCommand));
        }
        public override void drawElement()
        {
             if (!this.elementEnabled)
                 return;
            Rect rect = getAbsolutePosition();

            float X = (float)rect.X + (float)(rect.Width / 2);
            float Y = (float)rect.Y + (float)(rect.Height / 2);
            gfx.DrawCircle(base.secondaryBrush, X, Y, (float)(rect.Width / 2), mouseOver || selected ? 2 : 1);
            gfx.FillCircle(primaryBrush, X, Y, (float)(rect.Width / 2) - 1);
            if (enabled)
            {
                gfx.DrawCircle(base.secondaryBrush, X, Y, 1, 5);
            }
            gfx.DrawText(base.font, 12, base.secondaryBrush, (float)rect.Right + 4, (float)rect.Y, base.title);
        }
        public override bool OnWindowMessage(WindowMessage message, IntPtr wParam, IntPtr lParam)
        {
            //base.OnWindowMessage(message, wParam, lParam);
            if (selected && message == WindowMessage.Keydown && ((Keys)wParam == Keys.Return || (Keys)wParam == Keys.Enter || (Keys)wParam == Keys.Space))
            {
                if (!enabled)
                {
                    enabled = !enabled;
                    if (parent != null)
                    {
                        foreach (ElementRadioButton child in parent.children.Where(c => c.GetType() == typeof(ElementRadioButton) && c != this))
                        {
                            child.enabled = !enabled;
                        }
                    }
                    OnEnterKeyDown?.Invoke(this, new OverlayElementClicked(gfx, enabled, costumCommand));
                    return true;
                }
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
                    if (!enabled)
                    {
                        enabled = !enabled;
                        if (parent != null)
                        {
                            foreach (ElementRadioButton child in parent.children.Where(c => c.GetType() == typeof(ElementRadioButton) && c != this))
                            {
                                child.enabled = !enabled;
                            }
                        }
                        this.OnElementLMButtonClicked?.Invoke(this, new OverlayElementClicked(gfx, enabled, costumCommand));
                    }                        
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
