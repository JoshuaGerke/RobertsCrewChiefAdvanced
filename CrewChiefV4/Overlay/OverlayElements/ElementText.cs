using GameOverlay.Drawing;
using GameOverlay.PInvoke;
using System;
using System.Windows;
using System.Windows.Forms;

namespace CrewChiefV4.Overlay
{
    public class ElementText : OverlayElement
    {
        public string text = "";
        public TextAlign textAlign = TextAlign.Left | TextAlign.Top;
        public ElementText(Graphics gfx, string elementTitle, Font font, Rect rectangle,
            ColorScheme colorScheme, bool initialEnableState = true, TextAlign textAlign = TextAlign.Left | TextAlign.Top) :
            base(gfx, elementTitle, font, rectangle, colorScheme, initialEnableState)
        {
            
            this.text = elementTitle;
            this.textAlign = textAlign;
        }
        public override void drawElement()
        {
            if (!this.elementEnabled)
                return;
            System.Windows.Rect rect = getAbsolutePosition();
            if (OnElementDraw  == null)
            {
                if(textAlign.HasFlag(TextAlign.Left) && textAlign.HasFlag(TextAlign.Top))
                {
                    gfx.DrawText(font, secondaryBrush, (float)rect.X, (float)rect.Y, text);
                }
                else if (textAlign.HasFlag(TextAlign.Left) && textAlign.HasFlag(TextAlign.Center))
                {
                    System.Drawing.SizeF stringSize = font.MeasureString(text);
                    float textY = (((float)rect.Height - stringSize.Height) / 2) + (float)rect.Y;
                    gfx.DrawText(font, secondaryBrush, (float)rect.X, (float)textY, text);                    
                }                    
                else if(textAlign.HasFlag(TextAlign.CenterRect))
                {
                    gfx.DrawTextCenterInRect(font, secondaryBrush, rect, text);
                }                
            }           
            //gfx.DrawBox2D(base.secondaryBrush, base.primaryBrush, new Rectangle(base.rectangle), 1);

            //base.OnElementDraw?.Invoke(this, new OverlayElementDrawUpdate(gfx, new Rectangle(base.rectangle)));

            foreach (var child in children)
            {
                child.drawElement();
            }
            return;
        }

    }
}
